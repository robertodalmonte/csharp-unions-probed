# C# discriminated unions, probed

What C# 15 `union` types and `closed` hierarchies actually do on .NET 11, at every boundary a
real application crosses: the compiler, pattern matching, System.Text.Json, ASP.NET Core (Minimal
API, RDG, MVC, OpenAPI, validation), EF Core, and Roslyn. Every claim was produced by a small
program in [`probes/`](probes/) and is tagged with the SDK build it was verified on. The
official documentation says what works. These pages record what silently does not, with the
diagnostic codes, exception messages and HTTP status codes quoted from the run.

Unions ship in .NET 11 GA (November 2026, STS). RC 1 (2026-09-08) and RC 2 carry a go-live
licence; on `net11.0` the language version defaults to `15.0`, so no `<LangVersion>preview</LangVersion>`
is needed.

## The mental model in five lines

A `union` lowers to a **struct** over a single `object?` called `Value`. Each case type gets one
constructor and one implicit conversion. So a union **boxes** value-type cases, has a legal
**null-valued `default`** that no diagnostic ever mentions, offers **no equality**, and on the wire
is **just its case's JSON with no discriminator**. Almost everything on these pages follows from
one of those four facts.

## The pages

| | Page | The finding |
|---|---|---|
| 01 | [What the compiler generates](docs/01-what-the-compiler-generates.md) | Struct over `object?`; a user-defined implicit operator silently shadows the generated conversion; what a union body may and may not declare |
| 02 | [The null-valued union](docs/02-the-null-valued-union.md) | `default(U).Value` is `null`, a switch with no `null` arm compiles clean and throws at runtime, and no analysis level changes that |
| 03 | [Pattern matching](docs/03-pattern-matching.md) | Which patterns unwrap to `Value` is per pattern; `is <union type>` is always true on RC 1 and a compile error under the current spec |
| 04 | [System.Text.Json](docs/04-system-text-json.md) | A union serializes as its case with no discriminator and cannot come back without a classifier; the shipped structural classifier refuses most state unions; a `closed` hierarchy round-trips with `$type` |
| 05 | [ASP.NET Core](docs/05-aspnet-core.md) | `(int, string)` rejects every string under the Web defaults; `IParsable<T>` on the union is the whole fix for query/header/route/form; Minimal API `[FromForm]` silently binds `default`; one unclassifiable union takes every route down, MVC included |
| 06 | [Closed hierarchies](docs/06-closed-hierarchies.md) | Exhaustiveness is a warning under three codes; closure is per assembly and not transitive; adding a case is binary-breaking; the `closed` vs `union` table |
| 07 | [Hand-written unions](docs/07-hand-written-unions.md) | Class-based, non-boxing and `IUnionMembers` providers all work on RC 1; the `record class` provider stack overflow and its two fixes |
| 08 | [EF Core](docs/08-ef-core.md) | EF cannot map a union and will not; the `Ignore` + `ComplexProperty` + private bridge recipe, verified with the keyword; why TPH corrupts mutable state |
| 09 | [Roslyn](docs/09-roslyn.md) | `ITypeSymbol.UnionCaseTypes` lists cases of generated and hand-written unions, and returns an empty array for an ordinary type |
| 10 | [Documentation and upstream](docs/10-documentation-and-upstream.md) | Where the reference, the spec and the compiler disagree; the issues filed and how each was ruled, including three filed on 2026-10-14 |

## Status by build

| Page | RC 1 `11.0.100-rc.1.26425.128` | RC 2 `11.0.100-rc.2.26504.105` (re-run 2026-10-14) | GA |
|---|---|---|---|
| 01 compiler | verified 2026-09-11 | declaration errors `CS9373`, `CS9374`, `CS9375` and the `TEST_*` conversion errors reproduce; no change found | |
| 02 null-valued union | verified 2026-09-11 | unchanged: `default` is `null`, a switch with no `null` arm throws `SwitchExpressionException` | |
| 03 pattern matching | verified 2026-09-11; `is <union type>` rule changed after RC 1 | **changed: `is <union type>` and `pet is Pet` are `CS8121`; a union's own interface in `is` no longer matches (`g is IMarker` is `False`); an interface no held type can implement is `CS8121`, an unsealed case keeps it legal (`QIface`)** | |
| 04 System.Text.Json | verified 2026-09-11 | unchanged: `Web` still rejects `(int, string)` strings; the structural classifier still refuses `Approval`, `UnionTwins`, `SubsetU`. New: `QClosedJson` shows the data-less `Approval` state as a `closed` hierarchy round-trips by `$type` (runtime#135742) | |
| 05 ASP.NET Core | verified 2026-09-11 and 2026-09-15 | **changed: union-level `[JsonNumberHandling]` works (runtime#133668 / #133673); Minimal API validation reaches union cases (aspnetcore#68268).** The source-generator path (`QSrcGen`) behaves like reflection: the union-level attribute makes `"hello"` bind to `string`, the plain union stays ambiguous under `Web`. Unchanged: MVC union bodies are not validated, `[FromForm]` still binds `default`, RDG `CS0029`, the all-routes 500, `IParsable<T>` as the fix | |
| 06 closed hierarchies | verified 2026-09-10 | re-run in full with `QClosed`/`QClosedLib`: `CS9382`, `CS9384`, `CS0106`, `CS0267`, the three warnings and their `WarningsAsErrors` promotion, closure not transitive, and the binary-breaking added case (`SwitchExpressionException` with no diagnostic) all reproduce | |
| 07 hand-written unions | verified 2026-09-11 | unchanged: `CLASS_PROVIDER` and `CAST_FIX` run, `REC_RECURSE` still overflows the stack | |
| 08 EF Core | verified 2026-09-11 (EF 11 RC 1) and on EF 10.0.9 | recipe re-run on EF 11 RC 2: builds and runs; corrupt-row and `default` guards throw as described | |
| 09 Roslyn | verified 2026-09-11 | unchanged on `Microsoft.CodeAnalysis` 5.12: same four `UnionCaseTypes` results | |

Empty cells mean "not yet run on that build", not "still true". "Unchanged" means the probe was
re-run and its output matched what the page says, not that every line was diffed against an RC 1
capture. When a build changes a finding, the page gets the new result and the old one stays,
labelled with the build it held on. Packages in `probes/` are pinned to the RC 2 versions.

## Running the probes

| Project | Kind | Backs pages |
|---|---|---|
| `QLang` | console | 01, 02, 03, 07 |
| `QLang2` | console, `AnalysisLevel=latest-all` | 01, 02, 07 |
| `QRoslyn` | console, the SDK's own Roslyn | 09 |
| `UnionWeb` | web + OpenApi | 04, 05 |
| `Probe2` | web + OpenApi | 04, 05 |
| `RdgNoString` | web | 05 |
| `QOa` | web + OpenApi | 04, 05 |
| `QVal` | web | 05 |
| `QParsable` | web + MVC | 05 |
| `QFormCtor` | web + MVC, no union | 05, 10 |
| `QIface` | console | 03 |
| `QSrcGen` | console | 05, 10 |
| `QClosedJson` | console | 04, 10 |
| `QClosed` + `QClosedLib` | console + class library | 06 |
| `QEf` | console + EF Core SQLite | 08 |
| `QRepro1` / `QRepro2` / `QRepro3` | the repros exactly as posted | 10 |

Each project stands alone: `dotnet build`, then `dotnet run --no-build`, and read the output. The
web probes bind fixed ports on `127.0.0.1`. [`probes/README.md`](probes/README.md) maps each
project to the pages it backs, lists the conditional builds that fail or crash by design, and
gives the RDG build command with the one check that matters: confirm the generated file exists
and mentions your endpoint before trusting an RDG run.

Requirements: .NET SDK `11.0.100-rc.1` or later (packages are pinned to `11.0.0-rc.1.26425.128`;
bump them when you move). `QEf` uses SQLite through the EF provider and needs nothing else.

## What this is not

It is not a tutorial on union types; the language reference and the .NET blog cover that. It is
not a library. And it is not stable: several findings are RC 1 behaviour that RC 2 or GA will
change, and the status table is the only claim about which ones. Read the output, not the docs,
when they disagree.

## Licence

MIT. Roberto Dalmonte, 2026.
