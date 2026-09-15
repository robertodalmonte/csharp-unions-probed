# 1. What the compiler generates

*Verified on SDK `11.0.100-rc.1.26425.128` (.NET 11 RC 1). Probes: `QLang`, `QLang2`.*

`union` is a contextual keyword. The compiler lowers the declaration to a `struct`:

```csharp
public union FourEyesApproval(NotRequired, PendingApproval, PartlyApproved, FullyApproved, Rejected);
```

becomes, in effect,

```csharp
[Union] public struct FourEyesApproval : IUnion   // System.Runtime.CompilerServices
{
    public FourEyesApproval(NotRequired value) => Value = value;
    public FourEyesApproval(PendingApproval value) => Value = value;
    // ... one constructor + one implicit conversion per case type
    public object? Value { get; }
}
```

`UnionAttribute` and `IUnion` live in the runtime since Preview 5. On `net11.0`, `LangVersion`
resolves to `15.0` by default, so `<LangVersion>preview</LangVersion>` is not needed for either
`union` or `closed`. RC 1 carries a go-live licence; unions ship in .NET 11 GA (November 2026).

## Five consequences

1. **The contents are a single `object?`.** Value-type case types get boxed on every assignment.
   Keep cases `record class`, not `record struct`; a struct case buys nothing here. A hand-written
   `[Union]` type can avoid the boxing (see [07](07-hand-written-unions.md)).
2. **Each case type gets a free implicit conversion.** `return new Rejected(id);` from a method
   typed `FourEyesApproval` compiles, and so does `new Transfer(id, from, to, new PendingApproval())`.
3. **`default(FourEyesApproval)` is legal and has `Value == null`.** A union is a struct, so there
   is no "unconstructed" state, only a null-valued one. No compiler diagnostic ever points at it.
   This is the biggest single trap; it has its own page ([02](02-the-null-valued-union.md)).
4. **Most patterns apply to `Value`, but not all.** `approval switch { Rejected r => … }` tests the
   contents; `is { … }` without a type portion does not unwrap. The per-pattern rules are in
   [03](03-pattern-matching.md).
5. **No generated equality, cloning or deconstruction.** Unlike `record`, a union answers "which
   case?" and nothing else. Do not assume `==` on two unions means what you want.

## The conversion is not a standard implicit conversion

Two consequences, both compiled:

- **It cannot chain.** No user-defined conversion feeding a union conversion, and no union into
  another union: both are `CS0029`.
- **There is no explicit union conversion.** An explicit conversion to a case type does not give
  you one to the union: `CS0030`.

A user-defined `implicit operator` on the union **shadows the generated conversion, silently**.
With `public static implicit operator Temp(Celsius c) => new Fahrenheit(…)` declared on
`union Temp(Celsius, Fahrenheit)`, `Temp t = new Celsius(100)` holds `Fahrenheit { V = 212 }`, with
no diagnostic.

## Declaration rules

- The grammar allows `partial`, struct modifiers, type parameters with constraints, and an
  interface list. `public partial union Result<T>(T, Error) : IResult where T : notnull { … }` is
  well-formed and compiles.
- A body may add methods and computed properties, but **no instance fields, no auto-properties, no
  field-like events** (`CS9373`), and **no public single-parameter constructor** (`CS9374`; that
  slot belongs to the generated creation member). Any explicit constructor must delegate through
  `this(...)` to a generated one (`CS9375`).
- Case types may be anything convertible to `object`: interfaces, type parameters, nullable types,
  other unions. They may **overlap** (`union OneOrMore<T>(T, IEnumerable<T>)` is a spec example).
  The compiler errors only if a *conversion* is ambiguous, never on the declaration. (The
  ambiguous-conversion error itself has not been compiled here.)

## Roslyn sees the cases

`ITypeSymbol.UnionCaseTypes` is the supported way for an analyzer or source generator to enumerate
a union's cases. See [09](09-roslyn.md).
