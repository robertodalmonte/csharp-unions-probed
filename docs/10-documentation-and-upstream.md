# 10. Where the documentation disagrees with the compiler, and what was reported upstream

## Sources used

- The **C# language reference**, *Union types* (`learn.microsoft.com/.../builtin-types/union`,
  page revised 2026-08-14). The lowering, the `object?` storage, the boxing of value-type cases,
  the four behavioural obligations and the `IUnionMembers` provider are all there.
- The **feature specification**, *Unions*. The Learn copy (`proposals/unions`, updated 2026-07-20)
  is **stale on type patterns**. The spec moved to `proposals/csharp-15.0/unions.md` in
  dotnet/csharplang on 2026-08-12, and its type-pattern section was rewritten on 2026-08-18
  (dotnet/csharplang#10302). Cite the GitHub copy and check its history, not the Learn page's date.
- The **.NET 11 RC 1 release notes** for C# and for libraries
  (`github.com/dotnet/core/blob/main/release-notes/11.0/preview/rc1/{csharp,libraries}.md`,
  2026-09-08): the source for `CSharp15`, `ITypeSymbol.UnionCaseTypes`,
  `JsonUnionTypeStructuralClassifier` and `InferClosedTypePolymorphism`.
- The **.NET Blog**, *C# Unions and Closed Hierarchies in ASP.NET Core* (Dmitrii Korolev,
  2026-09-10): the map [05](05-aspnet-core.md) was probed against, and the only source for its
  SignalR and Blazor claims.
- Andrew Lock, *Exploring the .NET 11 preview 7: the pain of serializing unions and closed class
  hierarchies with System.Text.Json* (2026-09-01). Both halves reproduced on RC 1.

Neither the reference nor the spec can be trusted alone.

## The two places where they disagree, and what the compiler does

- **`is <union type>`.** RC 1 follows the July spec (always true, `CS0183`). The spec and
  Roslyn `main` changed on 2026-08-18 to `Value`-only, where it is `CS8121`. The language
  reference's *"typically doesn't match"* is wrong under both ([03](03-pattern-matching.md)).
- **The default null state of `Value`** was never really in conflict. The spec's Nullability
  section, the language reference and the compiler all agree on "not null" unless a case type is
  nullable; the spec's resolved open question that says otherwise is stale (dotnet/roslyn#85054).
  What *is* wrong is the language reference's `Pet pet = default` sample, which implies a warning
  the compiler never gives ([02](02-the-null-valued-union.md)).

## Reported upstream

Check the status of each before relying on the matching page at RC 2 or GA.

- **dotnet/runtime#133668**: `union (int, string)` rejects every string under
  `JsonSerializerDefaults.Web`, and `[JsonNumberHandling]` on the union is ignored. Ruled
  2026-09-11 by the STJ area owner: the rejection is by design and stays; the ignored attribute is
  a bug, labelled `bug`, milestone `11.0.0`. The issue was retitled the same day to
  *"`[JsonNumberHandling]` on a union type is not applied to its cases"*, so a close means the
  attribute fix and nothing more. Confirmed in the thread: a union-level `Strict` makes `"42"`
  bind to `string`. Check `QRepro1`'s output, not the issue state.
- **dotnet/aspnetcore#66648**, comment 5630500296: `[FromForm]` silently binds a null-valued
  union, and RDG binds the `string` case or emits `CS0029` for non-body unions. Narrowed on
  2026-09-15 after the area owner asked for the scenario: there is no union binding in .NET 11,
  "considering" for .NET 12, and an analyzer was offered. Since `IParsable<T>` on the union
  already binds every non-body source correctly (`QParsable`), the ask became the diagnostic, not
  the feature: cover `[FromForm]`, exempt `IParsable<T>` unions, default to `ASP0020`'s severity.
  And the form fallback is the multi-constructor struct rule, not union code. **Ruled the same
  day**: no analyzer in .NET 11 ("too late in the release"), .NET 12 undecided; the [release
  notes'](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-11?view=aspnetcore-10.0&tabs=minimal-apis#c-union-types)
  one sentence ("Union types aren't supported for non-body binding sources") is the documented
  position, and "it is up to user to verify the correct behavior". The form-binding ask was
  split off at the area owner's request as a general, union-free issue (next entry).
- **Minimal API `[FromForm]` with more than one public constructor** (union-free; drafted
  2026-09-15, to be filed): the handler receives `default(T)` / `null` with 200 and nothing is
  logged, because the mapper's `Warning` goes to a null logger and the generated binding catches
  only `FormDataMappingException`. Asks for the type to be refused when the delegate is built, as
  a non-parsable query parameter already is; failing that at request time; and for
  `FormDataMapperOptions` to receive the application's `ILoggerFactory`. Mechanism and repro on
  [page 05](05-aspnet-core.md) and in `QFormCtor`. Related: dotnet/aspnetcore#51379, the same
  path for a class with a primary and a parameterless constructor.
- **dotnet/docs#55951**: the reference's `pet is Pet` note, and its null-handling sample. The
  first item's proposed fix was itself wrong: it proposed "always true" and cited the spec, and on
  2026-09-11 AlekseyTs (Roslyn) pointed out that the spec had changed to `Value`-only, under which
  `pet is Pet` does not compile. The note is still wrong, for a different reason. The second item
  stands.

Not reported, because upstream already covers them:

- the null-state rule, which is by design (dotnet/roslyn#85054);
- union validation, fixed for RC 2 (dotnet/aspnetcore#68268, PR #68846);
- the `record class` provider overflow (dotnet/docs#55322);
- the all-routes 500, which is not union-specific.

## Not compiled

Only three claims in these pages come from documents rather than a run:

- that the compiler calls `TryGetValue`/`HasValue` instead of `Value` when they exist (the
  samples run, but the call was not observed);
- the ambiguous-conversion error for overlapping cases;
- the four behavioural obligations of hand-written unions, which the compiler does not check, so
  there is nothing to compile.
