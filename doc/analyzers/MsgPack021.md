# MsgPack021 Generated formatter data type not supported

The source generator writes formatters for non-generic, non-abstract classes and structs that the formatter can access. The data type named by the formatter's `IMessagePackFormatter<T>` interface must meet these requirements.

## Examples of patterns that are flagged by this analyzer

```cs
public abstract class MyBase { }

[GeneratedMessagePackFormatter]
public partial class MyBaseFormatter : IMessagePackFormatter<MyBase> // Diagnostic reported here: abstract
{
}

[GeneratedMessagePackFormatter]
public partial class ListFormatter : IMessagePackFormatter<List<int>> // Diagnostic reported here: generic
{
}
```

## Typical fix

Use a concrete, non-generic class or struct as the data type. For an abstract base type, use [`[Union]`](../../README.md#union) with the derived types instead.
