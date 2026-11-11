# MsgPack004 Attribute public members of MessagePack objects

Public members of `MessagePackObjectAttribute`-attributed types require either `KeyAttribute` or `IgnoreMemberAttribute`.

## Examples of patterns that are flagged by this analyzer

```cs
[MessagePackObject]
public class C {
    [Key(0)]
    public int A { get; set; }

    public int B { get; set; } // MsgPack004
}
```

## Typical fix

Indicate that the unattributed member should be serialized:

```cs
[MessagePackObject]
public class C {
    [Key(0)]
    public int A { get; set; }

    [Key(1)]
    public int B { get; set; }
}
```

An automated code fix is available for this.

## Alternative fix

If the unattributed member should _not_ be serialized, apply the `[IgnoreMember]` attribute:

```cs
[MessagePackObject]
public class C {
    [Key(0)]
    public int A { get; set; }

    [IgnoreMember]
    public int B { get; set; }
}
```

## Types from referenced assemblies

This analyzer only reports on members declared in the project being compiled.
Members of a type defined in a referenced assembly are reported by the project that defines that type, so an unattributed public member of such a type must be fixed in its defining project.
