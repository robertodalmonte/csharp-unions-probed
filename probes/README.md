# Probes

Every claim in [`../docs`](../docs) came from one of these projects, run on **.NET SDK
`11.0.100-rc.1.26425.128`** between 2026-09-10 and 2026-09-15. They are deliberately small,
self-contained console and web projects that print what the framework did. Re-run them on RC 2 and
on GA before relying on anything in the docs, and read the output, not the docs, when they
disagree.

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
| `QEf` | console + EF Core SQLite | The persistence recipe verbatim with the real `union` keyword; the placeholder constructor; detached entities; an int-mapped `Kind` | 08 |
| `QRepro1` / `QRepro2` / `QRepro3` | the repros exactly as posted | dotnet/runtime#133668, dotnet/aspnetcore#66648, dotnet/docs#55951 | 10 |

## Running

- **Basic:** each project stands alone. `dotnet build`, then `dotnet run --no-build`. The web
  probes bind fixed ports on `127.0.0.1`, so run them one at a time: `UnionWeb` 5310+, `Probe2`
  5410+, `QVal` 5480, `QOa` 5491+, `QRepro2` 5560, `QParsable` 5570.
- **Conditional probes:** `dotnet build --no-incremental -p:DefineConstants=NAME`.
  - Builds that fail by design: `QLang` `PROBE_ERR` and `PROBE_ERR2`; `QLang2` `TEST_EVENT`,
    `TEST_CHAIN`, `TEST_EXPLICIT`, `TEST_LIST` and `TEST_UNION_INTO_UNION`.
  - A run that crashes by design: `QLang2` `REC_RECURSE` overflows the stack.
  - Runs that succeed: `QLang2` `CLASS_PROVIDER` and `CAST_FIX`.
- **RDG (Request Delegate Generator):**

  ```
  dotnet build --no-incremental -p:EnableRequestDelegateGenerator=true -p:EmitCompilerGeneratedFiles=true
  ```

  Then confirm that `obj\…\GeneratedRouteBuilderExtensions.g.cs` exists **and mentions the
  endpoint** before trusting the run. An incremental build after flipping the property does not
  re-run the generator, so it silently re-tests the reflection path and produces an empty diff that
  looks like "RDG behaves identically". That happened here once.
- **`ASP0020`:** `UnionWeb`, `Probe2`, `RdgNoString` and `QRepro2` carry an `.editorconfig` that
  suppresses it on purpose, to observe the runtime behaviour behind the analyzer. Delete the file
  from `UnionWeb` to see the three `ASP0020` errors instead.
- **Moving to a newer SDK:**
  - Packages are pinned to `11.0.0-rc.1.26425.128`; bump them to the matching version.
  - `QRoslyn` references the SDK's own Roslyn through `$(NetCoreRoot)` and `$(NETCoreSdkVersion)`,
    so it follows whatever SDK `dotnet` resolves to. Pin the SDK with a `global.json` if you have
    several installed.
- **The empty `Directory.Build.props` / `.targets`** are deliberate. They stop MSBuild inheriting
  anything from the folders above your clone, so the probes build the same way everywhere.
