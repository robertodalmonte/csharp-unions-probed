# 6. The `closed` modifier, and `closed` vs `union`

*Compiled on SDK `11.0.100-rc.1.26425.128`, 2026-09-10. Diagnostic codes and messages are quoted
from the compiler. Probes: `QLang`, `QLang2`; the cross-assembly cases were built as two projects
by hand.*

`closed` is the *class* half of C# 15's closed-hierarchy work: it marks a base class or record
whose derived set the compiler may treat as complete, so a `switch` over it can be exhaustive with
no discard arm. Its edges are not guessable.

```csharp
public closed record Transportation;                       // implicitly abstract, see below
public sealed record Plane(string FlightNumber, TimeOnly ArrivalTime) : Transportation;
public sealed record Train(string Operator,   TimeOnly ArrivalTime) : Transportation;
public sealed record Car(TimeOnly ExpectedArrivalTime)              : Transportation;

// exhaustive, no `_` arm, no CS8509, including from a DIFFERENT assembly
public static string ToLabel(Transportation t) => t switch
{
    Plane p => $"flight {p.FlightNumber}",
    Train r => r.Operator,
    Car    _ => "car",
};
```

## Declaration rules the compiler enforces

| Rule | Diagnostic | Consequence |
|---|---|---|
| A `closed` type is always implicitly abstract; writing `abstract` too is an **error** | `CS9384: a closed type cannot be marked abstract because it is always implicitly abstract` | `new Transportation()` never compiles, so the base is not a case any switch must handle. That is *why* the switch needs no base arm, not a special exhaustiveness rule. |
| `closed` is not valid on an interface | `CS0106: The modifier 'closed' is not valid for this item` | Classes and records only. If you want a closed interface hierarchy, the base must be a class. |
| Modifier order matters against `partial` | `closed partial record` compiles; `partial closed record` is `CS0267` | A papercut that looks like `closed` is unsupported when you hit it. |
| Generic bases are fine | | `closed record Box<T>` with `sealed record BoxA<T> : Box<T>` switches exhaustively. |

## The boundary is the assembly, not the file

```csharp
// same assembly, any file, no `permits` list to update: just derive
public sealed record Ship(string Line, TimeOnly ArrivalTime) : Transportation;   // OK

// a different assembly
public sealed record Helicopter(string Op) : Transportation;
// CS9382: 'Helicopter': cannot use a closed type 'Transportation' from another assembly as a base type.
```

Unlike Java's `sealed … permits`, the case list is **not written down anywhere**. Metadata carries
a single `IsClosedTypeAttribute` on the base; derived types are unmarked, and a consumer works out
the case set by scanning the referenced assembly's types. Three things follow.

## Closure is not transitive: `sealed` on every case is load-bearing

`closed` closes *direct* derivation from the closed type. A public non-sealed case is still an
open extension point across assembly boundaries:

```csharp
// library
public closed record Layer;
public record Mid : Layer;              // NOT sealed
// consumer, different assembly
public sealed record MyMid : Mid;       // compiles: CS9382 does not apply, the base is Mid, not Layer
```

Exhaustiveness survives, because the `Mid` type pattern subsumes `MyMid`, so this is not a
soundness hole. It is a hole in the mental model: "closed hierarchy" does not mean "fixed set of
leaf types", and any code that reasons about concrete cases (a serializer's `$type` table, a
persistence discriminator, a source generator enumerating cases) will meet types it has never seen.
**Seal every case unless you specifically want the extension point.**

## Exhaustiveness is a warning, and there are three codes

This is the fact that inverts the feature's sales pitch. Add a case to the closed hierarchy and
recompile everything:

```
warning CS8509: The switch expression does not handle all possible values of its input type
                (it is not exhaustive). For example, the pattern 'Models.Helicopter' is not covered.
...
Build succeeded.  0 Error(s)  3 Warning(s)
```

The build is green. The program then dies at the first unmatched value:

```
System.Runtime.CompilerServices.SwitchExpressionException: Non-exhaustive switch expression failed
to match its input. Unmatched value was Helicopter { Operator = HeliAir, ... }
```

So `closed` does not give you "adding a case breaks the build" out of the box. It gives you a
warning that a normal project configuration ignores. And there are three distinct codes covering
three distinct holes; promoting only the famous one leaves two open:

| Code | Hole |
|---|---|
| `CS8509` | a case type is not covered |
| `CS8655` | the input is nullable and there is no `null` arm; closedness says nothing about `null` |
| `CS8846` | the only arm that could cover a case has a `when` clause, so the compiler cannot count it |

```xml
<!-- Directory.Build.props: the minimum that makes `closed` mean what people think it means -->
<WarningsAsErrors>$(WarningsAsErrors);CS8509;CS8655;CS8846</WarningsAsErrors>
```

Verified: all three promote to `error` and fail the build. Without this, a `closed` hierarchy is
documentation, not enforcement. The same three codes apply to a `union`.

## Adding a case is a binary-breaking change

The warning only reaches code that is recompiled. Because the case list is discovered by scanning
the referenced assembly, a consumer assembly that is not rebuilt has no idea a case appeared:

> Compiled `App.dll` against `Lib.dll` (two cases): clean. Rebuilt **`Lib.dll` only** with a third
> case and dropped it next to the untouched `App.dll`. Result: `SwitchExpressionException` at
> runtime. No diagnostic anywhere, not at Lib's build, not at App's, because App never built.

Treat adding a case to a published `closed` hierarchy the way you would treat adding a member to a
public interface: a major-version, recompile-all change. In a single solution that rebuilds as a
unit this is invisible. The moment the hierarchy ships in a NuGet package, or a plugin loads
against a host assembly it did not build against, the guarantee is gone and the failure is a
runtime throw in the consumer's process. Keep a closed hierarchy inside the assembly that switches
over it where you can, and accept a defensive `_ => throw` at genuine plug-in boundaries. That is
the one place the "never `_ => throw`" rule of [02](02-the-null-valued-union.md) is wrong for a
closed hierarchy, though never for a union in the same assembly.

## Where this leaves `closed` vs `union`

| | `closed` hierarchy | `union` |
|---|---|---|
| Exhaustiveness | warning (three codes), source-level only | warning, same codes |
| Cross-assembly | case list scanned; adding a case is binary-breaking | same exposure |
| `null` | reference type: `null` is always a hole, needs `CS8655` | `default(U).Value is null` ([02](02-the-null-valued-union.md)) |
| Case payload access | ordinary inheritance, no boxing | boxed in `object?` (`Value`) |
| Leaf set actually fixed | only if every case is `sealed` | yes: the cases are the declaration |
| System.Text.Json | round-trips with `$type` | no discriminator; needs a structural classifier ([04](04-system-text-json.md)) |
| EF Core | mappable as TPH, with the stale-column defect ([08](08-ef-core.md)) | not mappable; needs a bridge |
| OpenAPI | `anyOf` + `discriminator` on `$type` | `anyOf`, no discriminator, identical whether or not it can deserialize |
| ASP.NET Core binding | body only; a missing or misplaced `$type` is a **500** | body, plus every non-body source once the union implements `IParsable<T>`; `int`+`string` rejects strings under Web defaults; without `IParsable<T>`, Minimal `[FromForm]` silently binds `default` and MVC non-body sources are 500 ([05](05-aspnet-core.md)) |

Use `closed` when the type must cross a serialization or persistence boundary, `union` when it
lives in the domain and you want the case list to be the declaration. Both need the
`WarningsAsErrors` line; neither is safe to extend in a shipped library without a recompile.

## Without either keyword

A switch over a plain `abstract record` hierarchy covering every derived case, with no discard,
still fails to compile clean: `CS8509` (verified, Roslyn / C# 14). Roslyn cannot prove an ordinary
class hierarchy closed, so you are forced into the `_ => throw` that makes a sixth case compile
silently everywhere. Such a hierarchy gives you the *shape* of a discriminated union and none of
its guarantee. On .NET 11, change `abstract record` to `closed record` (drop `abstract`, it is now
`CS9384`) and the same switch compiles clean, with the same diagnostics a union gets.

So exhaustiveness is no longer the reason to reach for `union` over a hierarchy. The remaining
reasons are that a union's case list is the declaration rather than a scan of the assembly, and
that a hierarchy's leaf set is only fixed if every case is `sealed`.
