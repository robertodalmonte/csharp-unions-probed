# C# discriminated unions, probed

What C# 15 `union` types and `closed` hierarchies actually do on .NET 11, at every boundary a
real application crosses: the compiler, pattern matching, System.Text.Json, ASP.NET Core (Minimal
API, RDG, MVC, OpenAPI, validation), EF Core, and Roslyn. Every claim was produced by a small
program in [`probes/`](probes/) and is tagged with the SDK build it was verified on. The
official documentation says what works. These pages record what silently does not, with the
diagnostic codes, exception messages and HTTP status codes quoted from the run.

Unions ship in .NET 11 GA (November 2026, STS). RC 1 (2026-09-08) carries a go-live licence; on
`net11.0` the language version defaults to `15.0`, so no `<LangVersion>preview</LangVersion>` is
needed.

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
| 10 | [Documentation and upstream](docs/10-documentation-and-upstream.md) | Where the reference, the spec and the compiler disagree; the three issues filed and how each was ruled |

## Status by build

| Page | RC 1 `11.0.100-rc.1.26425.128` | RC 2 | GA |
|---|---|---|---|
| 01 compiler | verified 2026-09-11 | | |
| 02 null-valued union | verified 2026-09-11 | | |
| 03 pattern matching | verified 2026-09-11; `is <union type>` rule changed after RC 1 | **re-run: `is <union type>` and interface cases become `CS8121`** | |
| 04 System.Text.Json | verified 2026-09-11 | | |
| 05 ASP.NET Core | verified 2026-09-11 and 2026-09-15 | **re-run: validation of union cases (aspnetcore#68268); `[JsonNumberHandling]` on the union (runtime#133668); any new analyzer from aspnetcore#66648** | |
| 06 closed hierarchies | verified 2026-09-10 | | |
| 07 hand-written unions | verified 2026-09-11 | | |
| 08 EF Core | verified 2026-09-11 (EF 11 RC 1) and on EF 10.0.9 | | |
| 09 Roslyn | verified 2026-09-11 | | |

Empty cells mean "not yet run on that build", not "still true". The RC 2 column names the items
already known to change. When a build changes a finding, the page gets the new result and the
old one stays, labelled with the build it held on.

## Running the probes

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
