// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator;
using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

/// <summary>
/// Verifies that every type emitted by the source generator is decorated with <c>ExcludeFromCodeCoverage</c>
/// (see https://github.com/MessagePack-CSharp/MessagePack-CSharp/issues/2297).
/// </summary>
public class CodeCoverageExclusionTests
{
    private const string ExcludeAttribute = "[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]";

    // The attribute is not available on netstandard2.0 or .NET Framework, so it is only emitted where it exists.
    private const string Guard = "#if !NETSTANDARD2_0 && !NETFRAMEWORK";

    private const string TestSource = """
        using MessagePack;

        [MessagePackObject]
        public class MyObject
        {
            [Key(0)]
            public int Value { get; set; }
        }

        [MessagePackObject(keyAsPropertyName: true)]
        public class MyStringKeyObject
        {
            public string Name { get; set; }
        }

        public enum MyEnum
        {
            A,
            B,
        }

        [Union(0, typeof(MyUnionMember))]
        public interface IMyUnion
        {
        }

        [MessagePackObject]
        public class MyUnionMember : IMyUnion
        {
            [Key(0)]
            public int Value { get; set; }
        }

        namespace MessagePack
        {
            [GeneratedMessagePackResolver]
            partial class GeneratedMessagePackResolver
            {
            }
        }
        """;

    [Fact]
    public async Task AllGeneratedTypesAreExcludedFromCodeCoverage()
    {
        IReadOnlyList<(string Path, string Text)> generated = await RunGeneratorAsync(TestSource);

        // Formatters for objects (both integer and string keyed), the enum and union, plus the resolver.
        string paths = string.Join(Environment.NewLine, generated.Select(g => g.Path));
        Assert.True(generated.Count >= 5, $"Expected generated formatters and resolver, got:{Environment.NewLine}{paths}");

        foreach ((string path, string text) in generated)
        {
            Assert.True(
                text.Contains(ExcludeAttribute),
                $"Generated file '{path}' is missing {ExcludeAttribute}.");
        }
    }

    [Fact]
    public async Task ExcludeAttributeIsGuardedForTargetsWhereItIsUnavailable()
    {
        IReadOnlyList<(string Path, string Text)> generated = await RunGeneratorAsync(TestSource);

        foreach ((string path, string text) in generated)
        {
            string normalized = text.Replace("\r\n", "\n");

            // Every occurrence of the attribute must be wrapped by the guard, so netstandard2.0 consumers still compile.
            int attributeCount = CountOccurrences(normalized, ExcludeAttribute);
            int guardedCount = CountOccurrences(normalized, $"{Guard}\n\t{ExcludeAttribute}\n#endif\n")
                + CountOccurrences(normalized, $"{Guard}\n{ExcludeAttribute}\n#endif\n");
            Assert.True(attributeCount > 0, $"Generated file '{path}' has no {ExcludeAttribute}.");
            Assert.Equal(attributeCount, guardedCount);
        }
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static async Task<IReadOnlyList<(string Path, string Text)>> RunGeneratorAsync(string source)
    {
        ImmutableArray<MetadataReference> frameworkReferences = await ReferencesHelper.DefaultTargetFrameworkReferences.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "CodeCoverageExclusionTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references: frameworkReferences.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new MessagePackGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        GeneratorDriverRunResult result = driver.GetRunResult();

        return result.GeneratedTrees
            .Select(tree => (tree.FilePath, tree.GetText().ToString()))
            .ToList();
    }
}
