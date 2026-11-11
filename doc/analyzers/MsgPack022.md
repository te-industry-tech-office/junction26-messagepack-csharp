# MsgPack022 Surrogate member does not match the formatted type

When `[GeneratedMessagePackFormatter(SurrogateType = typeof(...))]` is used, each keyed member of the surrogate must match a public, read-write instance member of the formatted type, with the same name and type.

## Examples of patterns that are flagged by this analyzer

```cs
// ProjectA.MyObject has `public int Id { get; set; }` and no `Missing` member.
public class MyObjectSurrogate
{
    [Key(0)]
    public int Missing { get; set; } // Diagnostic reported here
}

[GeneratedMessagePackFormatter(SurrogateType = typeof(MyObjectSurrogate))]
public partial class MyObjectFormatter : IMessagePackFormatter<ProjectA.MyObject>
{
}
```

## Typical fix

Rename the surrogate member to match a public, read-write member of the formatted type, and make sure the types are identical:

```cs
public class MyObjectSurrogate
{
    [Key(0)]
    public int Id { get; set; }
}
```
