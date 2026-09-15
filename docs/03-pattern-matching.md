# 3. Pattern matching: which patterns unwrap to `Value`

*Verified on SDK `11.0.100-rc.1.26425.128`, 2026-09-11. Probes: `QLang`, `QLang2`.*
*One rule changed after RC 1's compiler was cut; that row is marked and needs a re-run at RC 2.*

Most patterns applied to a union test its `Value`, which is what makes `approval is Rejected r`
read naturally. But it is decided pattern by pattern, and the exceptions are not intuitive.

| Pattern | Applies to |
|---|---|
| **Type / declaration** (`is Rejected r`) | `Value`. On RC 1 the union instance is tested first, then `Value` (see below). Under the current spec, `Value` only |
| Constant (`is null`, `is 10`), relational (`is > 9`) | `Value` |
| **Property / positional *without* a type** (`is { Name: … }`) | **the union itself, no unwrap** |
| Property / positional *with* a type (`is Rejected { … }`) | `Value` |
| `var`, discard `_`, list patterns | the union itself |
| `not` | passes its input through, so it never narrows |

```csharp
approval is Rejected r                // Value: the ordinary case
approval is { Rejector: … }           // CS0117: no type portion, so applied to the union itself
approval is Rejected { Rejector: … }  // OK: type portion present, unwraps to Value
if (GetApproval() is var a)           // `a` is the union, not the unwrapped case
GetApproval() switch
{
    not null and var v => …,          // `v` is still the union: `not null` returned its input
}
```

As compiled on RC 1:

- `is null` and `is not null` test `Value`.
- `is > 10` and `is 15` unwrap.
- `is { Approver: _ }` is `CS0117: 'FourEyesApproval' does not contain a definition for 'Approver'`.
- `is var` and `not null and var x` both capture the union itself.
- A discard compiles against a union and matches it (`u switch { _ => … }`).
- **A list pattern does not unwrap.** On `union OneOrMore<T>(T, IEnumerable<T>)`, `o is [1, 2]` is
  `CS8985: List patterns may not be used for a value of type 'OneOrMore<int>'` (plus `CS0021`),
  even though the `IEnumerable<T>` case would support one.

## `is <the union's own type>`: always true on RC 1, a compile error under the current spec

- **RC 1** implements the "Try-Both" rule: a type pattern tests the union instance first and
  `Value` second. `real is FourEyesApproval` and `default(FourEyesApproval) is FourEyesApproval`
  are both `true`, and the compiler says so: `warning CS0183: The given expression is always of the
  provided ('FourEyesApproval') type`.
- **The design changed after RC 1's compiler was cut.** LDM 2026-08-12 dropped Try-Both. The spec
  (dotnet/csharplang#10302) and the compiler (dotnet/roslyn#84897) both landed on `main` on
  2026-08-18. A type pattern on a union now tests **only `Value`**, and it is an error when no case
  type is pattern-compatible with the pattern's type: `pet is Pet` becomes `error CS8121: An
  expression of type 'Pet' cannot be handled by a pattern of type 'Pet'`. That message shape comes
  from the PR's tests; it has not been compiled here yet. **Re-run `QLang` at RC 2.**

Either way, `is <union type>` is useless as a has-a-value test. Use `approval.Value is not null`
(or `approval is not null`, since `is null` tests `Value`). `is IUnion { Value: null }` works as a
generic runtime check only on an `object` or unconstrained `T` input, where it is an ordinary type
test. On a union-typed input it falls under the new rule and does not compile unless a case type
implements `IUnion`.

The same shift applies to **interfaces declared on the union**. A union that declares
`union GenUnion<T>(T, string) : IMarker` makes `u is IMarker` always true on RC 1 (`CS0183` again,
because the instance is tested first). Under the current spec the union's own interfaces no longer
count: the pattern is true only when the held case implements `IMarker`, and it is an error when
no case type can (`p is IPet ip // error` in the spec). An unconstrained `T` case may keep it
compiling. **Re-probe at RC 2.**

**The language reference is wrong under both rules.** It says *"a pattern like `pet is Pet`
typically doesn't match"*. On RC 1 it always matched; under the current spec it does not compile.
Code that relies on RC 1's `true` fails as a **build error** at RC 2, not as a silent behaviour
change. Reported as part of dotnet/docs#55951 ([10](10-documentation-and-upstream.md)).
