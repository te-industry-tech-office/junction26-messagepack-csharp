# MsgPack020 Generated formatter must implement IMessagePackFormatter<T>

A class marked with `[GeneratedMessagePackFormatter]` identifies the data type to serialize by its `IMessagePackFormatter<T>` interface. The class must implement exactly one such interface.

## Examples of patterns that are flagged by this analyzer

```cs
[GeneratedMessagePackFormatter]
public partial class MyObjectFormatter // Diagnostic reported here: no IMessagePackFormatter<T>
{
}
```

## Typical fix

Implement `IMessagePackFormatter<T>` for the type to be serialized:

```cs
[GeneratedMessagePackFormatter]
public partial class MyObjectFormatter : IMessagePackFormatter<ProjectA.MyObject>
{
}
```
