// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using MessagePack.SourceGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessagePack.SourceGenerator.CodeAnalysis;

/// <summary>
/// Builds the serialization model for a partial formatter marked with <c>[GeneratedMessagePackFormatter]</c>.
/// The formatted type may come from an assembly that carries no MessagePack attributes, so its members are read
/// in contractless mode, or through the keys declared on an optional surrogate type.
/// </summary>
internal static class GeneratedFormatterCollector
{
    private static readonly SymbolDisplayFormat BinaryWriteTypeFormat = new(
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly);

    /// <summary>
    /// Creates the model for one generated formatter.
    /// </summary>
    /// <param name="compilation">The compilation that contains the formatter.</param>
    /// <param name="options">The analyzer options for the compilation.</param>
    /// <param name="referenceSymbols">The MessagePack reference symbols.</param>
    /// <param name="reportDiagnostic">Receives diagnostics about the formatter. May be <see langword="null"/>.</param>
    /// <param name="formatter">The user-declared partial formatter class.</param>
    /// <param name="surrogate">The surrogate type, or <see langword="null"/> for contractless mode.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The model, or <see langword="null"/> if the formatter cannot be generated.</returns>
    internal static FullModel? Collect(
        Compilation compilation,
        AnalyzerOptions options,
        ReferenceSymbols referenceSymbols,
        Action<Diagnostic>? reportDiagnostic,
        INamedTypeSymbol formatter,
        INamedTypeSymbol? surrogate,
        CancellationToken cancellationToken)
    {
        Location location = formatter.Locations.FirstOrDefault() ?? Location.None;
        string formatterName = formatter.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (!IsPartial(formatter, cancellationToken))
        {
            reportDiagnostic?.Invoke(Diagnostic.Create(MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustBePartial, location, formatterName));
            return null;
        }

        INamedTypeSymbol? openFormatterInterface = compilation.GetTypeByMetadataName($"{Constants.IMessagePackFormatterInterfaceNamespace}.{Constants.IMessagePackFormatterInterfaceName}`1");
        ImmutableArray<INamedTypeSymbol> formatterInterfaces = formatter.AllInterfaces
            .Where(i => i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, openFormatterInterface))
            .ToImmutableArray();
        if (formatterInterfaces.Length != 1)
        {
            reportDiagnostic?.Invoke(Diagnostic.Create(MsgPack00xMessagePackAnalyzer.GeneratedFormatterMustImplementFormatter, location, formatterName));
            return null;
        }

        ITypeSymbol dataTypeSymbol = formatterInterfaces[0].TypeArguments[0];
        if (dataTypeSymbol is not INamedTypeSymbol { IsGenericType: false, IsAbstract: false } type
            || type.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || !compilation.IsSymbolAccessibleWithin(type, formatter))
        {
            reportDiagnostic?.Invoke(Diagnostic.Create(
                MsgPack00xMessagePackAnalyzer.GeneratedFormatterDataTypeNotSupported,
                location,
                formatterName,
                dataTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            return null;
        }

        bool isClass = type.TypeKind == TypeKind.Class;
        if (isClass && !type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithin(c, formatter)))
        {
            reportDiagnostic?.Invoke(Diagnostic.Create(MsgPack00xMessagePackAnalyzer.NoDeserializingConstructor, location));
            return null;
        }

        bool hasCallbackReceiver = type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, referenceSymbols.IMessagePackSerializationCallbackReceiver));
        bool needsCast = hasCallbackReceiver && type.GetMembers().OfType<IMethodSymbol>().Any(m =>
            m.ExplicitInterfaceImplementations.Length > 0 && (m.Name == "OnBeforeSerialize" || m.Name == "OnAfterDeserialize"));

        IReadOnlyList<MemberSerializationInfo>? members;
        bool isIntKey;
        if (surrogate is null)
        {
            members = CreateContractlessMembers(type);
            isIntKey = false;
        }
        else
        {
            members = CreateSurrogateMembers(type, surrogate, referenceSymbols, reportDiagnostic, location, out isIntKey);
        }

        if (members is null)
        {
            return null;
        }

        ObjectSerializationInfo info = ObjectSerializationInfo.Create(
            type,
            isClass: isClass,
            nestedFormatterRequired: false,
            genericTypeParameters: Array.Empty<GenericTypeParameterInfo>(),
            constructorParameters: Array.Empty<MemberSerializationInfo>(),
            isIntKey: isIntKey,
            members: members.ToArray(),
            hasIMessagePackSerializationCallbackReceiver: hasCallbackReceiver,
            needsCastOnAfter: needsCast,
            needsCastOnBefore: needsCast,
            resolverOptions: options.Generator.Resolver) with
        {
            Formatter = new QualifiedNamedTypeName(formatter),
            IsUserDeclaredFormatter = true,
        };

        return FullModel.Empty with
        {
            ObjectInfos = ImmutableSortedSet.Create(info),
            Options = options,
        };
    }

    /// <summary>
    /// Contractless mode: every public, read-write instance property and field is serialized under its name as a string key.
    /// </summary>
    private static IReadOnlyList<MemberSerializationInfo> CreateContractlessMembers(INamedTypeSymbol type)
    {
        List<ISymbol> targets = GetInstanceMembers(type).Where(IsPublicReadWrite).OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
        List<MemberSerializationInfo> members = new(targets.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            members.Add(CreateMember(targets[i], intKey: i, stringKey: targets[i].Name));
        }

        return members;
    }

    /// <summary>
    /// Surrogate mode: keys and membership come from the surrogate's <c>[Key]</c> and <c>[IgnoreMember]</c> annotations.
    /// Each keyed surrogate member is matched by name, and type, to a public read-write member of the formatted type.
    /// </summary>
    private static IReadOnlyList<MemberSerializationInfo>? CreateSurrogateMembers(
        INamedTypeSymbol type,
        INamedTypeSymbol surrogate,
        ReferenceSymbols referenceSymbols,
        Action<Diagnostic>? reportDiagnostic,
        Location location,
        out bool isIntKey)
    {
        isIntKey = false;
        bool? keyKindIsInt = null;
        HashSet<object> claimedKeys = new();
        List<MemberSerializationInfo> members = new();
        List<ISymbol> targetMembers = GetInstanceMembers(type);

        foreach (ISymbol surrogateMember in GetInstanceMembers(surrogate))
        {
            ImmutableArray<AttributeData> attributes = surrogateMember.GetAttributes();
            if (attributes.Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, referenceSymbols.IgnoreAttribute)))
            {
                continue;
            }

            AttributeData? keyAttribute = attributes.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, referenceSymbols.KeyAttribute));
            object? key = keyAttribute is { ConstructorArguments.Length: 1 } ? keyAttribute.ConstructorArguments[0].Value : null;
            if (key is not (int or string))
            {
                reportDiagnostic?.Invoke(Diagnostic.Create(
                    MsgPack00xMessagePackAnalyzer.MemberNeedsKey,
                    surrogateMember.Locations.FirstOrDefault() ?? location,
                    surrogate.Name,
                    surrogateMember.Name));
                return null;
            }

            bool memberIsIntKey = key is int;
            keyKindIsInt ??= memberIsIntKey;
            if (keyKindIsInt != memberIsIntKey)
            {
                reportDiagnostic?.Invoke(Diagnostic.Create(MsgPack00xMessagePackAnalyzer.DoNotMixStringAndIntKeys, surrogateMember.Locations.FirstOrDefault() ?? location));
                return null;
            }

            ISymbol? target = targetMembers.FirstOrDefault(m => m.Name == surrogateMember.Name);
            if (target is null || !IsPublicReadWrite(target) || !SymbolEqualityComparer.Default.Equals(GetMemberType(target), GetMemberType(surrogateMember)))
            {
                reportDiagnostic?.Invoke(Diagnostic.Create(
                    MsgPack00xMessagePackAnalyzer.SurrogateMemberNotFound,
                    surrogateMember.Locations.FirstOrDefault() ?? location,
                    surrogateMember.Name,
                    type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                return null;
            }

            if (!claimedKeys.Add(key))
            {
                reportDiagnostic?.Invoke(Diagnostic.Create(MsgPack00xMessagePackAnalyzer.KeysMustBeUnique, surrogateMember.Locations.FirstOrDefault() ?? location));
                return null;
            }

            members.Add(key is int intKey
                ? CreateMember(target, intKey: intKey, stringKey: target.Name)
                : CreateMember(target, intKey: members.Count, stringKey: (string)key));
        }

        isIntKey = keyKindIsInt ?? false;
        return members;
    }

    private static MemberSerializationInfo CreateMember(ISymbol target, int intKey, string stringKey)
    {
        ITypeSymbol memberType = GetMemberType(target);
        bool isProperty = target is IPropertySymbol;
        bool isInitOnly = target is IPropertySymbol { SetMethod.IsInitOnly: true };
        bool isRequired = target switch
        {
            IPropertySymbol property => property.IsRequired,
            IFieldSymbol field => field.IsRequired,
            _ => false,
        };

        return new MemberSerializationInfo(
            IsProperty: isProperty,
            IsWritable: true,
            IsReadable: true,
            IsInitOnly: isInitOnly,
            IsRequired: isRequired,
            IntKey: intKey,
            StringKey: stringKey,
            Name: target.Name,
            Type: memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            ShortTypeName: memberType.ToDisplayString(BinaryWriteTypeFormat),
            CustomFormatter: null)
        {
            // Members of the formatted type are never colliding with a base-type member of the same name here,
            // because GetInstanceMembers keeps only the most derived member of each name.
            DeclaringType = null,
        };
    }

    /// <summary>
    /// Enumerates the instance fields and properties of a type and its base types, keeping only the most derived member of each name.
    /// </summary>
    private static List<ISymbol> GetInstanceMembers(INamedTypeSymbol type)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<ISymbol> result = new();
        for (INamedTypeSymbol? current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers())
            {
                if (member.IsStatic || member.IsImplicitlyDeclared || member is not (IPropertySymbol or IFieldSymbol))
                {
                    continue;
                }

                if (member is IPropertySymbol { IsIndexer: true })
                {
                    continue;
                }

                if (seen.Add(member.Name))
                {
                    result.Add(member);
                }
            }
        }

        return result;
    }

    private static bool IsPublicReadWrite(ISymbol member) => member switch
    {
        IPropertySymbol property => property.DeclaredAccessibility == Accessibility.Public
            && property.GetMethod is { DeclaredAccessibility: Accessibility.Public }
            && property.SetMethod is { DeclaredAccessibility: Accessibility.Public },
        IFieldSymbol field => field.DeclaredAccessibility == Accessibility.Public && !field.IsReadOnly && !field.IsConst,
        _ => false,
    };

    private static ITypeSymbol GetMemberType(ISymbol member) => member switch
    {
        IPropertySymbol property => property.Type,
        IFieldSymbol field => field.Type,
        _ => throw new ArgumentOutOfRangeException(nameof(member)),
    };

    private static bool IsPartial(INamedTypeSymbol symbol, CancellationToken cancellationToken)
    {
        return symbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax(cancellationToken))
            .OfType<ClassDeclarationSyntax>()
            .Any(c => c.Modifiers.Any(SyntaxKind.PartialKeyword));
    }
}
