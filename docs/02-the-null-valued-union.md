# 2. The null-valued union

*Verified on SDK `11.0.100-rc.1.26425.128`, 2026-09-11. Probes: `QLang`, `QLang2`
(`AnalysisLevel=latest-all`), `QEf` for the EF Core part.*

`union` lowers to a struct, so every field, array slot and `default(T)` in a generic gives you a
union whose `Value` is `null`. There is no error and no exception at the point of creation. It
detonates later, inside whatever `switch` first meets it.

```csharp
FourEyesApproval a = default;      // legal; a.Value is null
var slots = new FourEyesApproval[3]; // three null-valued unions
```

## The compiler never asks for a `null` arm

By design, the default null state of `Value` is "not null" unless a case type is itself nullable.
The spec's Nullability section says so, the language reference says so, and RC 1 implements it.
(The spec also carries a resolved open question approving the opposite. That resolution is stale:
the compiler team closed the corresponding report as not planned, dotnet/roslyn#85054, 2026-09-08,
saying the speclet reflects the current design.)

Compiled on RC 1, including under `<AnalysisLevel>latest-all</AnalysisLevel>`:

- A switch covering all five `FourEyesApproval` cases with **no** `null` arm compiles with **no
  warning at all**. That holds whether the union is a parameter, a local assigned `default`, an
  array slot from `new FourEyesApproval[1]`, a field that is never assigned, a generic union, a
  union with an interface case, one with value-type-only cases, a struct field, or code inside a
  `#nullable disable` region. Passed `default(FourEyesApproval)`, the switch throws
  `SwitchExpressionException` at runtime. The language reference's own `Pet pet = default` sample
  implies a warning; there is none.
- `CS8655` (*the pattern 'null' is not covered*) appears in exactly two situations: when a case
  type is itself nullable (`union MaybeText(string?, int)`), or when the same method has already
  tested the union with `is null`, because flow analysis then learns "maybe null". A canary in the
  same build confirms the warning machinery works.
- A `null` arm that is present draws no "unreachable" warning either.

So the null-valued union is invisible to the compiler in both directions. Promoting `CS8655` to an
error does not help, because it never fires for non-nullable case types. **The `null` arm is a
discipline, not something the build will enforce.**

## Write the `null` arm anyway, and never `_ => throw`

A tempting habit is to end every `switch` with `_ => throw`. Over a union that is the one pattern
to avoid: add a case tomorrow and the compiler stays silent while call sites throw at runtime. You
paid for a union and opted out of its only guarantee.

```csharp
// closed set, exhaustive, no wildcard. A new case => CS8509 at this site.
public string ToLabel(FourEyesApproval approval) => approval switch
{
    NotRequired      => "Not required",
    PendingApproval  => "Pending approval",
    PartlyApproved p => $"Approved {p.Approver}",
    FullyApproved f  => $"Approved {f.Approver1}, {f.Approver2}",
    Rejected r       => $"Rejected {r.Rejector}",
    null             => throw new InvalidOperationException("Uninitialised approval."),
};
```

The `null` arm is not a wildcard. It covers exactly the null-valued hole and nothing else.
Verified: with the `null` arm present and one case missing, the switch still gets
`CS8509 … the pattern 'Rejected' is not covered`. A `_ => throw` would swallow that.

Note that `CS8509` is a **warning**. A normal project configuration ignores it, and the program dies
at the first unmatched value. Promote it, and its two siblings, to errors:

```xml
<WarningsAsErrors>$(WarningsAsErrors);CS8509;CS8655;CS8846</WarningsAsErrors>
```

`CS8846` covers the case where the only arm that could match has a `when` clause. See
[06](06-closed-hierarchies.md) for the same three codes on `closed` hierarchies.

The one legitimate `_ => throw` is at a **parse boundary**: rebuilding the union from a string or
an int that came out of a database or a wire. There the input is not a closed set, so a defensive
throw is correct. Distinguish switching over the *union* (closed, `null` arm at most) from switching
over a *stored discriminator* (open, defensive default).

## Where it bites

- **`new Transfer[n]`**, or any collection pre-sized before population.
- **Generic helpers** with `T state = default;` accumulators.
- **An entity built with `default` reaching EF Core's change tracker.** `Add`, `Attach` and `Update`
  of `new Transfer(…, default)` each throw at the call, not at `SaveChanges`, with whatever the
  bridge getter's `null` arm says (see [08](08-ef-core.md)). That is the arm doing its job.
- **The EF placeholder constructor is less exposed than it looks.** On EF Core
  `11.0.0-rc.1.26425.128` / SQLite, a placeholder that seeds `default(FourEyesApproval)` still
  materializes correctly, because the bridge setter runs before the first snapshot. The entity
  stays `Unchanged` through `DetectChanges`, and the same holds for `AsNoTracking()` and `FindAsync`.
  Seed a neutral case anyway; it costs nothing.

## Rules

- Seed a **neutral real case** wherever a union is created without a value.
- Give every switch that can see such a value an explicit `null` arm that **throws with a message
  naming the property**. Never a silent fallback to a business state.
- Model "genuinely absent" as a real case (`NotRequired`) or as `FourEyesApproval?`, never as the
  null-valued default.
- `is <union type>` is useless as a has-a-value test on every version of the rules
  ([03](03-pattern-matching.md)). Use `approval.Value is not null`, or `approval is not null`,
  since `is null` tests `Value`.
