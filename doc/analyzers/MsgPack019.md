# MsgPack019 Generated formatter must be partial

A class marked with `[GeneratedMessagePackFormatter]` is the target of source generation. The generator writes the body of the formatter into the class, so the class must be declared `partial`.

## Examples of patterns that are flagged by this analyzer

```cs
[GeneratedMessagePackFormatter]
public class MyObjectFormatter : IMessagePackFormatter<ProjectA.MyObject> // Diagnostic reported here: not partial
{
    // ...
}
```

## Typical fix

Declare the class `partial` and leave its body empty:

```cs
[GeneratedMessagePackFormatter]
public partial class MyObjectFormatter : IMessagePackFormatter<ProjectA.MyObject>
{
}
```

Any nesting types that contain the formatter must also be declared `partial`.
