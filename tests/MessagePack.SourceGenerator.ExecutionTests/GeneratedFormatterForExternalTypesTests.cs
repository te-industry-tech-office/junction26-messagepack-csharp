// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ExternalTypes.Library;

public class GeneratedFormatterForExternalTypesTests
{
    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithResolver(CompositeResolver.Create(GeneratedMessagePackResolver.Instance, StandardResolver.Instance));

    [Fact]
    public void ContractlessClassRoundTrips()
    {
        var person = new ExternalPerson { Id = 7, Label = "seven" };

        byte[] bytes = MessagePackSerializer.Serialize(person, Options);
        ExternalPerson result = MessagePackSerializer.Deserialize<ExternalPerson>(bytes, Options);

        Assert.Equal(7, result.Id);
        Assert.Equal("seven", result.Label);
    }

    [Fact]
    public void ContractlessClassUsesStringKeysForPublicReadWriteMembers()
    {
        byte[] bytes = MessagePackSerializer.Serialize(new ExternalPerson { Id = 1, Label = "x" }, Options);

        // A fixmap with two entries: Id and Label. The read-only Doubled member is not serialized.
        Assert.Equal(0x82, bytes[0]);
        string json = MessagePackSerializer.ConvertToJson(bytes, Options);
        Assert.Contains("\"Id\":1", json);
        Assert.Contains("\"Label\":\"x\"", json);
        Assert.DoesNotContain("Doubled", json);
    }

    [Fact]
    public void ContractlessClassNullRoundTrips()
    {
        byte[] bytes = MessagePackSerializer.Serialize(default(ExternalPerson), Options);
        Assert.Null(MessagePackSerializer.Deserialize<ExternalPerson>(bytes, Options));
    }

    [Fact]
    public void ContractlessStructRoundTrips()
    {
        var point = new ExternalPoint { X = 3, Y = -4 };

        ExternalPoint result = MessagePackSerializer.Deserialize<ExternalPoint>(MessagePackSerializer.Serialize(point, Options), Options);

        Assert.Equal(3, result.X);
        Assert.Equal(-4, result.Y);
    }

    [Fact]
    public void SurrogateControlsKeysAndMembership()
    {
        var widget = new ExternalWidget { Id = 1, Name = "w", Internal = 99 };

        byte[] bytes = MessagePackSerializer.Serialize(widget, Options);

        // The surrogate uses integer keys 3 and 7, so the array has MaxKey + 1 = 8 slots.
        var reader = new MessagePackReader(bytes);
        Assert.Equal(8, reader.ReadArrayHeader());

        ExternalWidget result = MessagePackSerializer.Deserialize<ExternalWidget>(bytes, Options);
        Assert.Equal(1, result.Id);
        Assert.Equal("w", result.Name);
        Assert.Equal(0, result.Internal);
    }

    [Fact]
    public void MessagePackObjectCanContainExternalTypes()
    {
        var value = new ContainsExternalPerson
        {
            Person = new ExternalPerson { Id = 5, Label = "five" },
            Widget = new ExternalWidget { Id = 6, Name = "six" },
        };

        ContainsExternalPerson result = MessagePackSerializer.Deserialize<ContainsExternalPerson>(MessagePackSerializer.Serialize(value, Options), Options);

        Assert.Equal(5, result.Person.Id);
        Assert.Equal("five", result.Person.Label);
        Assert.Equal(6, result.Widget.Id);
        Assert.Equal("six", result.Widget.Name);
    }

    [Fact]
    public void GeneratedFormattersAreRegisteredInGeneratedResolver()
    {
        Assert.IsType<ExternalPersonFormatter>(GeneratedMessagePackResolver.Instance.GetFormatter<ExternalPerson>());
        Assert.IsType<ExternalPointFormatter>(GeneratedMessagePackResolver.Instance.GetFormatter<ExternalPoint>());
        Assert.IsType<ExternalWidgetFormatter>(GeneratedMessagePackResolver.Instance.GetFormatter<ExternalWidget>());
    }
}
