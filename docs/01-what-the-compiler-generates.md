# 1. What the compiler generates

*Verified on SDK `11.0.100-rc.1.26425.128` (.NET 11 RC 1). Re-run on SDK `11.0.100-rc.2.26504.105` (.NET 11 RC 2) on 2026-10-14: the
declaration errors (`CS9373`, `CS9374`, `CS9375`) and the conversion and `TEST_*` errors reproduce; nothing on this
page changed. Probes: `QLang`, `QLang2`.*

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
  The compiler errors only if a *conversion* is ambiguous, never on the declaration. Compiled on
  RC 2 (`QConv`, 2026-10-22): the ordinary better-conversion rules break most ties silently.
  `union L(IEnumerable<int>, IList<int>) = new List<int>()`, `union BD(Base, Derived) = new Derived()`
  (in either declaration order) and `union LD(long, double) = 5` all compile and pick the more
  specific case (`int` goes to `long`, a `float` to `double`). Only two unrelated interfaces
  implemented by one class are ambiguous: `union Sib(IFoo, IBar) = new Both()` is
  **`CS0457: Ambiguous user defined conversions 'Sib.Sib(IFoo)' and 'Sib.Sib(IBar)'`**, reported at
  the conversion site, never on the declaration. `Value` holds only the object, so for overlapping
  cases nothing at runtime says which case was chosen. `null` into a union is `CS0037` (it is a
  struct).

## Target typing and inference

Probed in `QConv` on RC 2. A union converts from a case wherever a target type is known:
`U u = flag ? new A() : new B()`, switch-expression arms, lambda returns, `U[] = [new A(), new B()]`,
`List<U>` with `Add`, `async Task<U>` and `async ValueTask<U>` returns, `Task.FromResult<U>(new A())`,
`Id<U>(new A())`, and `U? = new A()` (a `Nullable<U>`). It does **not** convert where a type must be
inferred or where the generic type has no conversion:

| Code | Result |
|---|---|
| `Task<U> t = Task.FromResult(new A())` | `CS0029`: infers `Task<A>`; always write `Task.FromResult<U>(…)` |
| `Task<U> t = taskOfA` | `CS0029` |
| `IEnumerable<U> e = enumerableOfA` | `CS0266` |
| `var v = flag ? new A() : new B()` | `CS0173`: no natural type |
| `Take<T>(T a, T b)` called with an `A` and a `B` | `CS0411` |
| `(U)someObject` | **compiles**, then `InvalidCastException` at runtime: it is a plain unbox of a struct, not a union conversion |

## Roslyn sees the cases

`ITypeSymbol.UnionCaseTypes` is the supported way for an analyzer or source generator to enumerate
a union's cases. See [09](09-roslyn.md).
