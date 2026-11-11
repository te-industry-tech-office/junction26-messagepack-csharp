// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator.Tests;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = CSharpSourceGeneratorVerifier<MessagePack.SourceGenerator.MessagePackGenerator>;

/// <summary>
/// Tests for <c>[GeneratedMessagePackFormatter]</c>, which lets the generator write a formatter for a type from another assembly (https://github.com/MessagePack-CSharp/MessagePack-CSharp/issues/2129).
/// </summary>
public class GeneratedMessagePackFormatterTests
{
    private readonly ITestOutputHelper testOutputHelper;

    public GeneratedMessagePackFormatterTests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    // Formatter members are written by hand in the diagnostic tests, so the compiler accepts the source
    // even when the generator declines to write them.
    private const string Members = """
            public void Serialize(ref MessagePackWriter writer, MyType value, MessagePackSerializerOptions options) => throw new System.NotImplementedException();

            public MyType Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => throw new System.NotImplementedException();
        """;

    [Fact]
    public async Task FormatterMustBePartial()
    {
        string testSource = /* lang=c#-test */ $$"""
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }
            }

            [GeneratedMessagePackFormatter]
            public class {|MsgPack019:MyFormatter|} : IMessagePackFormatter<MyType>
            {
            {{Members}}
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { testSource },
            },
        }.RunDefaultAsync(this.testOutputHelper);
    }

    [Fact]
    public async Task FormatterMustImplementFormatterOfT()
    {
        string testSource = /* lang=c#-test */ """
            using MessagePack;

            [GeneratedMessagePackFormatter]
            public partial class {|MsgPack020:MyFormatter|}
            {
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { testSource },
            },
        }.RunDefaultAsync(this.testOutputHelper);
    }

    [Fact]
    public async Task FormattedTypeMustNotBeAbstract()
    {
        string testSource = /* lang=c#-test */ $$"""
            using MessagePack;
            using MessagePack.Formatters;

            public abstract class MyType
            {
            }

            [GeneratedMessagePackFormatter]
            public partial class {|MsgPack021:MyFormatter|} : IMessagePackFormatter<MyType>
            {
            {{Members}}
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { testSource },
            },
        }.RunDefaultAsync(this.testOutputHelper);
    }

    [Fact]
    public async Task SurrogateMemberMustMatchFormattedType()
    {
        string testSource = /* lang=c#-test */ $$"""
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }
            }

            public class MySurrogate
            {
                [Key(0)]
                public int {|MsgPack022:Missing|} { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(MySurrogate))]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            {{Members}}
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { testSource },
            },
        }.RunDefaultAsync(this.testOutputHelper);
    }

    [Fact]
    public async Task SurrogateMemberMustHaveMatchingType()
    {
        string testSource = /* lang=c#-test */ $$"""
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }
            }

            public class MySurrogate
            {
                [Key(0)]
                public string {|MsgPack022:Id|} { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(MySurrogate))]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            {{Members}}
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { testSource },
            },
        }.RunDefaultAsync(this.testOutputHelper);
    }

    [Fact]
    public async Task SurrogateKeysMustNotMixStringAndInt()
    {
        string testSource = /* lang=c#-test */ $$"""
            using MessagePack;
            using MessagePack.Formatters;

            public class MyType
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }

            public class MySurrogate
            {
                [Key(0)]
                public int Id { get; set; }

                [Key("Name")]
                public string {|#0:Name|} { get; set; }
            }

            [GeneratedMessagePackFormatter(SurrogateType = typeof(MySurrogate))]
            public partial class MyFormatter : IMessagePackFormatter<MyType>
            {
            {{Members}}
            }
            """;

        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { testSource },
            },
            ExpectedDiagnostics = { DiagnosticResult.CompilerError("MsgPack005").WithLocation(0) },
        }.RunDefaultAsync(this.testOutputHelper);
    }
}
