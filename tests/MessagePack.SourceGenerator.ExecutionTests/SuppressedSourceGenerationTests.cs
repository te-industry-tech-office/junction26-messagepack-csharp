// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/// <summary>
/// A type that opts out of source generation (https://github.com/MessagePack-CSharp/MessagePack-CSharp/issues/2234).
/// It must still serialize correctly through the run-time (dynamic) resolver.
/// </summary>
[MessagePackObject(SuppressSourceGeneration = true)]
public class SuppressedSourceGenerationType
{
    [Key(0)]
    public int Id { get; set; }

    [Key(1)]
    public string Name { get; set; } = string.Empty;
}

public class SuppressedSourceGenerationTests
{
    [Fact]
    public void NoFormatterIsGeneratedForSuppressedType()
    {
        Assert.Null(GeneratedMessagePackResolver.Instance.GetFormatter<SuppressedSourceGenerationType>());
    }

    [Fact]
    public void SuppressedTypeRoundTripsThroughDynamicResolver()
    {
        var value = new SuppressedSourceGenerationType { Id = 7, Name = "seven" };

        byte[] bytes = MessagePackSerializer.Serialize(value, MessagePackSerializerOptions.Standard);
        SuppressedSourceGenerationType result = MessagePackSerializer.Deserialize<SuppressedSourceGenerationType>(bytes, MessagePackSerializerOptions.Standard);

        Assert.Equal(7, result.Id);
        Assert.Equal("seven", result.Name);
    }
}
