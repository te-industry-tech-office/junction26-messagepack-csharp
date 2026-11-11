// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static MessagePack.SourceGenerator.Constants;
using AnalyzerOptions = MessagePack.SourceGenerator.CodeAnalysis.AnalyzerOptions;

namespace MessagePack.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public partial class MessagePackGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<AnalyzerOptions> options = GeneratorUtilities.GetAnalyzerOption(context);

        var messagePackObjectTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            $"{AttributeNamespace}.{MessagePackObjectAttributeName}",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (context, _) => (ITypeSymbol)context.TargetSymbol);

        var unionTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            $"{AttributeNamespace}.{MessagePackUnionAttributeName}",
            predicate: static (node, _) => node is InterfaceDeclarationSyntax,
            transform: static (context, _) => (ITypeSymbol)context.TargetSymbol);

        var generatedFormatters = context.SyntaxProvider.ForAttributeWithMetadataName(
            $"{AttributeNamespace}.{GeneratedMessagePackFormatterAttributeName}",
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (context, _) => new GeneratedFormatterRequest(
                (INamedTypeSymbol)context.TargetSymbol,
                context.Attributes[0].NamedArguments.FirstOrDefault(kvp => kvp.Key == GeneratedMessagePackFormatterSurrogatePropertyName).Value.Value as INamedTypeSymbol));

        var combined =
            messagePackObjectTypes.Collect().Combine(unionTypes.Collect()).Combine(generatedFormatters.Collect());

        var source = combined
            .Combine(context.CompilationProvider)
            .Combine(options)
            .Select(static (s, ct) =>
            {
                AnalyzerOptions options = s.Right;

                if (!ReferenceSymbols.TryCreate(s.Left.Right, out ReferenceSymbols? referenceSymbols) || referenceSymbols.MessagePackFormatter is null)
                {
                    return default;
                }

                List<FullModel> modelPerType = new();
                void Collect(ITypeSymbol typeSymbol)
                {
                    if (TypeCollector.Collect(s.Left.Right, options, referenceSymbols, reportAnalyzerDiagnostic: null, typeSymbol, ct) is FullModel model)
                    {
                        modelPerType.Add(model);
                    }
                }

                foreach (var typeSymbol in s.Left.Left.Left.Left)
                {
                    Collect(typeSymbol);
                }

                foreach (var typeSymbol in s.Left.Left.Left.Right)
                {
                    Collect(typeSymbol);
                }

                foreach (GeneratedFormatterRequest request in s.Left.Left.Right)
                {
                    if (GeneratedFormatterCollector.Collect(s.Left.Right, options, referenceSymbols, reportDiagnostic: null, request.Formatter, request.Surrogate, ct) is FullModel generatedModel)
                    {
                        modelPerType.Add(generatedModel);
                    }
                }

                if (!options.KnownFormatters.IsEmpty)
                {
                    var customFormatterInfos = FullModel.Empty.CustomFormatterInfos.Union(
                        from known in options.KnownFormatters
                        where known.InaccessibleDescriptor is null
                        from formatted in known.FormattableTypes
                        where !options.GetCollidingFormatterDataTypes(known.Name).Contains(formatted) // skip formatters with colliding types to avoid non-deterministic code generation
                        select new CustomFormatterRegisterInfo
                        {
                            Formatter = known.Name,
                            DataType = formatted.Name,
                            FormattableDataType = formatted,
                            CustomFormatter = known,
                        });
                    modelPerType.Add(FullModel.Empty with { CustomFormatterInfos = customFormatterInfos, Options = options });
                }

                return FullModel.Combine(modelPerType.ToImmutableArray());
            });

        var splittedSources = source
            .SelectMany(static (s, ct) =>
            {
                if (s is null)
                {
                    return ImmutableArray<FullModel>.Empty;
                }

                var models = new List<FullModel>();
                models.AddRange(s.ArrayFormatterInfos.Select(i => FullModel.Empty with { Options = s.Options, ArrayFormatterInfos = ImmutableSortedSet.Create(i) }));
                models.AddRange(s.ObjectInfos.Select(i => FullModel.Empty with { Options = s.Options, ObjectInfos = ImmutableSortedSet.Create(i) }));
                models.AddRange(s.EnumInfos.Select(i => FullModel.Empty with { Options = s.Options, EnumInfos = ImmutableSortedSet.Create(i) }));
                models.AddRange(s.UnionInfos.Select(i => FullModel.Empty with { Options = s.Options, UnionInfos = ImmutableSortedSet.Create(i) }));

                return models.ToImmutableArray();
            });

        // Diagnostics for [GeneratedMessagePackFormatter] classes are reported separately from the models,
        // so that a rejected formatter produces a diagnostic and no partial output.
        var generatedFormatterDiagnostics = generatedFormatters
            .Combine(context.CompilationProvider)
            .Combine(options)
            .SelectMany(static (s, ct) =>
            {
                List<Diagnostic> diagnostics = new();
                if (ReferenceSymbols.TryCreate(s.Left.Right, out ReferenceSymbols? referenceSymbols))
                {
                    GeneratedFormatterCollector.Collect(s.Left.Right, s.Right, referenceSymbols, diagnostics.Add, s.Left.Left.Formatter, s.Left.Left.Surrogate, ct);
                }

                return diagnostics.ToImmutableArray();
            });

        context.RegisterSourceOutput(generatedFormatterDiagnostics, static (context, diagnostic) => context.ReportDiagnostic(diagnostic));

        context.RegisterSourceOutput(source, static (context, source) =>
        {
            if (source is { Options.IsGeneratingSource: true })
            {
                GenerateResolver(new GeneratorContext(context), source);
            }
        });

        context.RegisterSourceOutput(splittedSources, static (context, source) =>
        {
            if (source is { Options.IsGeneratingSource: true })
            {
                Generate(new GeneratorContext(context), source);
            }
        });
    }

    private class Comparer : IEqualityComparer<(TypeDeclarationSyntax, Compilation)>
    {
        public static readonly Comparer Instance = new Comparer();

        public bool Equals((TypeDeclarationSyntax, Compilation) x, (TypeDeclarationSyntax, Compilation) y)
        {
            return x.Item1.Equals(y.Item1);
        }

        public int GetHashCode((TypeDeclarationSyntax, Compilation) obj)
        {
            return obj.Item1.GetHashCode();
        }
    }

    private class GeneratorContext : IGeneratorContext
    {
        private SourceProductionContext context;

        public GeneratorContext(SourceProductionContext context)
        {
            this.context = context;
        }

        public CancellationToken CancellationToken => context.CancellationToken;

        public void AddSource(string hintName, string source) => context.AddSource(hintName, source);

        public void ReportDiagnostic(Diagnostic diagnostic) => context.ReportDiagnostic(diagnostic);
    }
}
