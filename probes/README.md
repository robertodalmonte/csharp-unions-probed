# Probes

Every claim in [`../docs`](../docs) came from one of these projects, run on **.NET SDK
`11.0.100-rc.1.26425.128`** between 2026-09-10 and 2026-09-15, and re-run on
**`11.0.100-rc.2.26504.105`** on 2026-10-14 and on **`11.0.100-rc.3.26522.148`** on 2026-10-26 (output identical to RC 2 apart from GUIDs, trace ids and paths).
**All 24 projects were re-run on GA SDK `11.0.100` / runtime `11.0.0` on 2026-11-11**,
including conditional and RDG checks; [captured commands and results](results/ga/README.md).
MVC now validates union cases (400 in `QVal`); the other union findings reproduce.
These are small, self-contained console and web projects that print what the framework did.
Read the output, not the docs, when they disagree.

| Project | Kind | What it probes | Doc |
|---|---|---|---|
| `QLang` | console | The `null` arm and its flow variants, the pattern-matching table, declaration errors (`PROBE_ERR`, `PROBE_ERR2`), the language reference's custom-union samples | 01, 02, 03, 07 |
| `QLang2` | console, `AnalysisLevel=latest-all` | Null-arm variants with a `CS8655` canary, conversion and declaration rules (`TEST_*`), union member providers, the `record class` provider overflow (`REC_RECURSE`, `CLASS_PROVIDER`, `CAST_FIX`) | 01, 02, 07 |
| `QRoslyn` | console, the SDK's own Roslyn | `ITypeSymbol.UnionCaseTypes` | 09 |
| `UnionWeb` | web + OpenApi | STJ `Default` vs `Web`; Minimal API and MVC bodies; the non-JSON binding sources on reflection and RDG; OpenAPI schemas; closed-hierarchy `$type` | 04, 05 |
| `Probe2` | web + OpenApi | Structural-classifier edges, the custom classifier, SignalR payload options, OpenAPI under `Strict`, MVC options, `AllowOutOfOrderMetadataProperties`, the Production 500, startup failure vs authorization | 04, 05 |
| `RdgNoString` | web | RDG emitting `CS0029` for a union with no `string` case | 05 |
| `QOa` | web + OpenApi | The blast radius of an unclassifiable union (reflection/RDG, `MapGroup`, MVC, read-time, a non-union control); a nested union case | 04, 05 |
| `QVal` | web | Validation on Minimal APIs and MVC, with canaries | 05 |
| `QParsable` | web + MVC | `IParsable<T>` on a union bound from query, header, route and form, Minimal API and MVC; the multi-constructor struct form fallback | 05 |
| `QFormCtor` | web + MVC, no union | The multi-constructor `[FromForm]` fallback with plain structs and classes, logging on at `Debug`; the #51379 shape; MVC contrast; `QUERY_CONTROL` for the `ASP0020` contrast | 05, 10 |
| `QIface` | console | Interface type patterns on a union: open and closed generic, sealed and unsealed cases, the `CS8121` cases (`CLOSED_INT`, `NO_CASE_IMPLEMENTS_SEALED`; `NO_CASE_IMPLEMENTS` compiles) | 03 |
| `QSrcGen` | console | A union-level `[JsonNumberHandling(Strict)]` through a `JsonSerializerContext` under the `Web` defaults, next to reflection | 05, 10 |
| `QClosedJson` | console | The data-less state from runtime#135742 as a closed hierarchy with `InferClosedTypePolymorphism`: five cases incl. two data-less round-trip by `$type`; missing and unknown discriminators throw | 04, 10 |
| `QClosed` + `QClosedLib` | console + class library | `closed` across assemblies: the declaration rules and the three warnings behind `DefineConstants`, closure that is not transitive, and the binary-breaking added case | 06 |
| `QEf` | console + EF Core SQLite | The persistence recipe verbatim with the real `union` keyword; the placeholder constructor; detached entities; an int-mapped `Kind` | 08 |
| `QConv` | console | Overlapping-case conversions (`CS0457` for two unrelated interfaces, better-conversion ties otherwise) and target typing / inference (`Task.FromResult`, `?:`, `var`, covariance); behind defines `AMBIG_SIBLINGS`, `NULL_LITERAL`, `INFER_TASK`, `VAR_TERNARY`, `TASK_CASE_TO_TASK_UNION`, `ENUMERABLE_COVARIANCE`, `INFER_TWO_ARGS`, `EXPLICIT_FROM_OBJECT` | 01 |
| `QGuid` | console | `(Guid, string)`, `(DateTime, string)`, `(Guid, int)` under `Default`, `Web` and `Web`+`Strict`; a try-parse classifier | 05 |
| `QStatus` | web + OpenApi | A union returned from Minimal API and MVC: status, body, `default`, OpenAPI (port 5590) | 05 |
| `QCsharp15` | console | 84 self-contained snippets compiled in-process by the SDK's own Roslyn at the default language version (a few with `Preview` and the `updated-memory-safety-rules` feature): `with(...)` (`W`), extension indexers (`E`), labeled jumps (`L`), breaking changes (`M`), memory safety (`U`). A case that compiles is run and its output printed; one that does not prints its diagnostics. `dotnet run --no-build -- E1` filters by id prefix. Saved output: `output-rc3.txt` and [GA](results/ga/QCsharp15-run.txt) | 11 |
| `QIde0410` | console | `IDE0410` at warning with `EnforceCodeStyleInBuild`: flags `goto END` and `goto NEXT`, not the flag-variable loop | 11 |
| `QRepro1` / `QRepro2` / `QRepro3` | the repros exactly as posted | dotnet/runtime#133668, dotnet/aspnetcore#66648, dotnet/docs#55951 | 10 |

## Running

- **Basic:** each project stands alone. `dotnet build`, then `dotnet run --no-build`. The web
  probes bind fixed ports on `127.0.0.1`, so run them one at a time: `UnionWeb` 5310+, `Probe2`
  5410+, `QVal` 5480, `QOa` 5491+, `QRepro2` 5560, `QParsable` 5570, `QFormCtor` 5580.
  `QStatus` uses 5590. `RdgNoString` keeps serving until stopped; set `ASPNETCORE_URLS`
  to choose its port (the GA run used `http://127.0.0.1:5599`) and request `/q?v=42`.
- **Conditional probes:** `dotnet build --no-incremental -p:DefineConstants=NAME`.
  - `QLang` and `QRepro3` carry their RC 1 `is <union type>` lines behind `RC1_IS_UNION_TYPE`;
    on RC 2 and later that define fails with `CS8121`.
  - Builds that fail by design: `QLang` `PROBE_ERR` and `PROBE_ERR2`; `QLang2` `TEST_EVENT`,
    `TEST_CHAIN`, `TEST_EXPLICIT`, `TEST_LIST` and `TEST_UNION_INTO_UNION`; `QFormCtor`
    `QUERY_CONTROL` (`ASP0020`).
  - A run that crashes by design: `QLang2` `REC_RECURSE` overflows the stack.
  - `QClosed` switches: `DERIVE_CLOSED` (`CS9382`), `ABSTRACT_CLOSED` (`CS9384`), `CLOSED_INTERFACE`
    (`CS0106`), `PARTIAL_ORDER` (`CS0267`); `PARTIAL_OK` builds; `NONEXHAUSTIVE`, `NULLABLE_INPUT`
    and `WHEN_ARM` build with `CS8509`, `CS8655` and `CS8846` as warnings.
  - The binary-breaking case: build `QClosed`, then
    `dotnet build QClosedLib -p:DefineConstants=THIRD -o <dir>`, copy `<dir>/QClosedLib.dll` over
    `QClosed/bin/Debug/net11.0/` and run `QClosed.dll`. `Helicopter => SwitchExpressionException`.
  - `QIface` switches: `CLOSED_INT` and `NO_CASE_IMPLEMENTS_SEALED` fail with `CS8121`;
    `NO_CASE_IMPLEMENTS` builds.
  - Runs that succeed: `QLang2` `CLASS_PROVIDER` and `CAST_FIX`.
  - `QConv`: `AMBIG_SIBLINGS`, `NULL_LITERAL`, `INFER_TASK`, `VAR_TERNARY`,
    `TASK_CASE_TO_TASK_UNION`, `ENUMERABLE_COVARIANCE` and `INFER_TWO_ARGS` fail as
    documented on page 01. `EXPLICIT_FROM_OBJECT` builds, then throws `InvalidCastException`.
  - `QRepro2` `WITH_QUERY`: reflection makes both requests 500; with RDG the query
    returns the string case (200), and the form endpoint falls back to reflection (200, default).
- **RDG (Request Delegate Generator):**

  ```
  dotnet build --no-incremental -p:EnableRequestDelegateGenerator=true -p:EmitCompilerGeneratedFiles=true
  ```

  Then confirm that `obj\…\GeneratedRouteBuilderExtensions.g.cs` exists **and covers the
  endpoint**, using its `InterceptsLocationAttribute` source location and generated binding
  code (route strings need not appear in the generated file). `RDG003` means that endpoint
  falls back to reflection; `QFormCtor` and the form-only `QRepro2` emit no endpoint file.
  An incremental build after flipping the property does not
  re-run the generator, so it silently re-tests the reflection path and produces an empty diff that
  looks like "RDG behaves identically". That happened here once.
- **`QVal` MVC re-run (dotnet/aspnetcore#69834):** the two `MVC` lines at the end of the output are
  the ones to watch. On RC 2 and RC 3 the canary record body (`/mvc/admin`) returns 400 and the union body
  (`/mvc/union`) returns 200 with `ModelState.IsValid == true`. **GA carries the fix:** both
  return 400; the union's error is keyed as `Value.Role`. This was verified with reflection
  and RDG enabled on 2026-11-11. The Minimal API lines (`/union-rec`, `/union-cls`) still
  return 400 and the `[Required]` wrapper "user missing" line still returns 200. Docs 05
  and 10 retain the older results, labelled with their builds.
- **`ASP0020`:** `UnionWeb`, `Probe2`, `RdgNoString` and `QRepro2` carry an `.editorconfig` that
  suppresses it on purpose, to observe the runtime behaviour behind the analyzer. Delete the file
  from `UnionWeb` to see the three `ASP0020` errors instead.
- **Moving to a newer SDK:**
  - Packages are pinned to GA `11.0.0`; bump them deliberately when moving versions.
  - `QRoslyn` references the SDK's own Roslyn through `$(NetCoreRoot)` and `$(NETCoreSdkVersion)`,
    so it follows whatever SDK `dotnet` resolves to. The repository's `global.json` pins
    `11.0.100`, permits later patches, and excludes prerelease SDKs.
- **The empty `Directory.Build.props` / `.targets`** are deliberate. They stop MSBuild inheriting
  anything from the folders above your clone, so the probes build the same way everywhere.
