# .NET 11 GA probe results

Run on 2026-11-11, Windows x64, SDK **11.0.100**, runtime and ASP.NET Core **11.0.0**,
OpenAPI and EF Core SQLite packages **11.0.0**. Roslyn is **5.12.0.0**, compiler build
`5.12.0-1.26529.118`, default language version **C# 15**.

All **24 projects** were built and all **23 executables** exercised. `QClosedLib` is the
library used by `QClosed`. The captures cover **132 commands**: 91 builds, 40 executions
(including the HTTP request to `RdgNoString`), and `dotnet --info`. Every command matched its
expected outcome. Thirty intentionally fail: 28 compilation checks and two crashing runs
(`REC_RECURSE` and `EXPLICIT_FROM_OBJECT`). `RdgNoString`'s reflection request deliberately
returns HTTP 500. `QCsharp15` compiles **84 snippets** in-process; errors printed for individual
snippets are results, not failures of its host.

The substantive change from the documented RC 3 union results is **MVC validation**:
`QVal`'s invalid union body now returns **400** with `errors.Value.Role`, matching the
plain-record control. Its old 200 response is retained as history in docs 05 and 10.
Minimal API validation remains 400. A missing `[Required]` union property remains **200**.

The language, JSON, closed-hierarchy, provider and EF Core results reproduce. `QCsharp15`
differs from `output-rc3.txt` only in compiler build/path and diagnostic ordering.
`QIde0410` still warns on the two `goto` statements and leaves the flag-variable loop alone.
The non-union `QOa` control also deserves an explicit path distinction: `/ok` returns 500
with reflection but 200 with RDG; an unclassifiable union still makes it 500 on both paths.

## Captures

[summary.json](summary.json) records each command, expected outcome, diagnostic checks,
exit code (or HTTP status), and log filename. [builds.txt](builds.txt) contains all build
diagnostics, grouped by command id. [environment.txt](environment.txt) records the SDK and runtimes.

| Project | Default runtime capture | RDG-enabled runtime capture |
|---|---|---|
| `Probe2` | [output](Probe2-run.txt) | [output](Probe2-rdg-run.txt) |
| `QClosed` | [output](QClosed-run.txt) | n/a |
| `QClosedJson` | [output](QClosedJson-run.txt) | n/a |
| `QClosedLib` | library: build and binary replacement | n/a |
| `QConv` | [output](QConv-run.txt) | n/a |
| `QCsharp15` | [all 84 cases](QCsharp15-run.txt) | n/a |
| `QEf` | [output](QEf-run.txt) | n/a |
| `QFormCtor` | [output](QFormCtor-run.txt) | [reflection fallback](QFormCtor-rdg-run.txt) |
| `QGuid` | [output](QGuid-run.txt) | n/a |
| `QIde0410` | [output](QIde0410-run.txt); warnings in builds.txt | n/a |
| `QIface` | [output](QIface-run.txt) | n/a |
| `QLang` | [output](QLang-run.txt) | n/a |
| `QLang2` | [output](QLang2-run.txt) | n/a |
| `QOa` | [output](QOa-run.txt) | [output](QOa-rdg-run.txt) |
| `QParsable` | [output](QParsable-run.txt) | [output](QParsable-rdg-run.txt) |
| `QRepro1` | [output](QRepro1-run.txt) | n/a |
| `QRepro2` | [form only](QRepro2-run.txt); [WITH_QUERY](QRepro2-WITH_QUERY-run.txt) | [form fallback](QRepro2-rdg-run.txt); [WITH_QUERY](QRepro2-rdg-WITH_QUERY-run.txt) |
| `QRepro3` | [output](QRepro3-run.txt) | n/a |
| `QRoslyn` | [output](QRoslyn-run.txt) | n/a |
| `QSrcGen` | [output](QSrcGen-run.txt) | n/a |
| `QStatus` | [output](QStatus-run.txt) | [output](QStatus-rdg-run.txt) |
| `QVal` | [output](QVal-run.txt) | [output](QVal-rdg-run.txt) |
| `RdgNoString` | [HTTP 500](RdgNoString-run.txt) | build fails with `CS0029` |
| `UnionWeb` | [output](UnionWeb-run.txt) | [output](UnionWeb-rdg-run.txt) |

Conditional runs are captured separately: [CLASS_PROVIDER](QLang2-CLASS_PROVIDER-run.txt),
[CAST_FIX](QLang2-CAST_FIX-run.txt), [REC_RECURSE](QLang2-REC_RECURSE-run.txt),
[NO_CASE_IMPLEMENTS](QIface-NO_CASE_IMPLEMENTS-run.txt), [PARTIAL_OK](QClosed-PARTIAL_OK-run.txt),
[EXPLICIT_FROM_OBJECT](QConv-EXPLICIT_FROM_OBJECT-run.txt), and the
[binary-added-case failure](QClosed-binary-added-case-run.txt).

## Coverage and reproduction

Run each project's `dotnet build --no-incremental`, followed by
`dotnet run --no-build --no-launch-profile` from that project directory. Web runs used
`ASPNETCORE_ENVIRONMENT=Development` unless the probe explicitly selects another environment.
Run the fixed-port hosts sequentially. `RdgNoString` needs
`ASPNETCORE_URLS=http://127.0.0.1:5599`; request `/q?v=42`, then stop the host.

Conditional builds use `-p:DefineConstants=NAME`. The manifest lists every actual invocation:

- `QLang`: `PROBE_ERR`, `PROBE_ERR2`, `RC1_IS_UNION_TYPE`.
- `QLang2`: `TEST_EVENT`, `TEST_CHAIN`, `TEST_EXPLICIT`, `TEST_LIST`,
  `TEST_UNION_INTO_UNION`, `CLASS_PROVIDER`, `CAST_FIX`, `REC_RECURSE`.
- `QIface`: `CLOSED_INT`, `NO_CASE_IMPLEMENTS_SEALED`, `NO_CASE_IMPLEMENTS`.
- `QConv`: `AMBIG_SIBLINGS`, `NULL_LITERAL`, `INFER_TASK`, `VAR_TERNARY`,
  `TASK_CASE_TO_TASK_UNION`, `ENUMERABLE_COVARIANCE`, `INFER_TWO_ARGS`, `EXPLICIT_FROM_OBJECT`.
- `QClosed`: `DERIVE_CLOSED`, `ABSTRACT_CLOSED`, `CLOSED_INTERFACE`, `PARTIAL_ORDER`,
  `PARTIAL_OK`, `NONEXHAUSTIVE`, `NULLABLE_INPUT`, `WHEN_ARM`. The last three were also
  rebuilt with their respective `WarningsAsErrors` code (`CS8509`, `CS8655`, `CS8846`).
- `QClosedLib`: `THIRD`, copied beside a previously built `QClosed` without rebuilding
  the consumer. The consumer sees `Helicopter`, then throws `SwitchExpressionException`.
- `QFormCtor`: `QUERY_CONTROL`.
- `QRepro2`: `WITH_QUERY`, with reflection and RDG.
- `QRepro3`: `RC1_IS_UNION_TYPE`.
- `UnionWeb`: temporarily replace the deliberate `ASP0020` suppression with severity
  `error`, observe the three errors, restore the original configuration and rebuild.

RDG builds use `--no-incremental -p:EnableRequestDelegateGenerator=true
-p:EmitCompilerGeneratedFiles=true`. The `*-rdg-generated.json` files record generated
file hashes, decoded `InterceptsLocationAttribute` source locations, the endpoint registration
at each location, and relevant binding lines. This confirms actual generation for the
endpoint rather than assuming that enabling RDG means it handled every route.

`QFormCtor` emits `RDG003` for all its Minimal API form endpoints and has no generated
endpoint file. Form-only `QRepro2` also falls back; its `WITH_QUERY` build generates the
query endpoint with `IsParsable = False` and `v_local = v_temp!`. `QParsable` generates
query, header, route and form binding with `IsParsable = True`. `RdgNoString` generates
the query endpoint but cannot compile its string-to-union assignment (`CS0029`).

Default builds were restored after the conditional and RDG runs. Captures normalize
the repository path to `<repo>` and remove trailing whitespace; GUIDs, trace IDs and
timings are left as observed. The HTTP header divider in `RdgNoString` is indented so it
is not mistaken for a merge-conflict marker. The probe programs themselves truncate some HTTP bodies.
These runs do not add Blazor, hub round-trips, trimmed/AOT publishing, or a new EF 10 TPH
comparison; the documented limits of those earlier investigations still apply.
