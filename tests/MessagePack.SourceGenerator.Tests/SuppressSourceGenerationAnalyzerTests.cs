// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.Analyzers.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = CSharpCodeFixVerifier<MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer, MessagePack.Analyzers.CodeFixes.MessagePackCodeFixProvider>;

/// <summary>
/// Verifies that the analyzer keeps validating <c>[MessagePackObject(SuppressSourceGeneration = true)]</c> types
/// (https://github.com/MessagePack-CSharp/MessagePack-CSharp/issues/2234).
/// Opting out of source generation only skips generation; the type is still serialized at run time, so its contract must still be checked.
/// </summary>
public class SuppressSourceGenerationAnalyzerTests
{
    private const string Preamble = @"
using MessagePack;
";

    [Fact]
    public async Task MemberWithoutKeyIsReported()
    {
        string input = Preamble + @"
[MessagePackObject(SuppressSourceGeneration = true)]
public class Foo
{
    public string {|MsgPack004:Member|} { get; set; }
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task AllowPrivateRequiredIsReported()
    {
        string input = Preamble + @"
[{|MsgPack015:MessagePackObject(SuppressSourceGeneration = true)|}]
public class Foo
{
    [Key(0)]
    private string member;
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task MemberTypeMustBeMessagePackObjectIsReported()
    {
        string input = Preamble + @"
public class Other
{
}

[MessagePackObject(SuppressSourceGeneration = true)]
public class Foo
{
    [Key(0)]
    public Other {|MsgPack003:Member|} { get; set; }
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task DuplicateIntKeysAreReportedWithoutAnalyzerFailure()
    {
        string input = Preamble + @"
[MessagePackObject(SuppressSourceGeneration = true)]
public class Foo
{
    [Key(0)]
    public string Member1 { get; set; }

    {|MsgPack005:[Key(0)]
    public string Member2 { get; set; }|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task DuplicateStringKeysAreReportedWithoutAnalyzerFailure()
    {
        string input = Preamble + @"
[MessagePackObject(SuppressSourceGeneration = true)]
public class Foo
{
    [Key(""a"")]
    public string Member1 { get; set; }

    {|MsgPack005:[Key(""a"")]
    public string Member2 { get; set; }|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task MixedKeyKindsAreReportedWithoutAnalyzerFailure()
    {
        string input = Preamble + @"
[MessagePackObject(SuppressSourceGeneration = true)]
public class Foo
{
    [Key(0)]
    public string Member1 { get; set; }

    {|MsgPack005:[Key(""Member2"")]
    public string Member2 { get; set; }|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task DuplicateKeysAreReportedWithoutAnalyzerFailureForGeneratedTypes()
    {
        string input = Preamble + @"
[MessagePackObject]
public class Foo
{
    [Key(0)]
    public string Member1 { get; set; }

    {|MsgPack005:[Key(0)]
    public string Member2 { get; set; }|}
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task SourceGenerationOnlyDiagnosticsAreNotReportedWhenSuppressed()
    {
        // The partial-class requirement (MsgPack011) and the AOT-only checks (MsgPack016, MsgPack017)
        // only matter when a formatter is generated, so they are not reported for a suppressed type.
        string input = Preamble + @"
[MessagePackObject(AllowPrivate = true, SuppressSourceGeneration = true)]
public class Foo
{
    [Key(0)]
    private string member;

    [Key(1)]
    public string Prop1 { get; init; } = ""some default"";

    [Key(2)]
    public string Prop2 { get; set; }
}
";

        await new VerifyCS.Test
        {
            TestCode = input,
        }.RunAsync();
    }
}
