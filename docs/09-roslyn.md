# 9. Roslyn: enumerating a union's cases from an analyzer or generator

*Verified through the SDK's own Roslyn (`Microsoft.CodeAnalysis` 5.11, the compiler in SDK
`11.0.100-rc.1.26425.128`), 2026-09-11. Probe: `QRoslyn`.*

The compiler uses the final `CSharp15` language-version name, and `ITypeSymbol.UnionCaseTypes`
exposes a union's cases. That is the supported way to reason about a union in a source generator
or analyzer, not re-parsing the declaration.

Output of the probe:

```
Microsoft.CodeAnalysis 5.11.0.0; LanguageVersion names with 15: CSharp15
compile errors: 0
FourEyesApproval: TypeKind=Struct, UnionCaseTypes.IsDefault=False, [NotRequired, PendingApproval, PartlyApproved]
IntOrString: TypeKind=Struct, UnionCaseTypes.IsDefault=False, [int, string]
Shape: TypeKind=Struct, UnionCaseTypes.IsDefault=False, [Circle, Square]
Plain: TypeKind=Class, UnionCaseTypes.IsDefault=False, []
```

Three things worth knowing:

- It lists the cases of compiler-generated **and** hand-written `[Union]` types (`Shape` is the
  language reference's hand-written sample).
- For an ordinary type it returns an **empty** array, not a default one, so `IsDefault` is not the
  test for "is this a union"; `Length > 0` (or the `[Union]` attribute) is.
- A NuGet `Microsoft.CodeAnalysis` 4.x has none of this. The probe references the compiler that
  ships inside the SDK, through `$(NetCoreRoot)sdk\$(NETCoreSdkVersion)\Roslyn\bincore\`, which is
  fine for a probe and wrong for a shipped analyzer. An analyzer needs the 5.x package once it is
  published.
