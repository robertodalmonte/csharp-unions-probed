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
  Roslyn `main` changed on 2026-08-18 to `Value`-only, where it is `CS8121`; RC 2 compiles it as
  `CS8121` (verified 2026-10-14). The language
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
  bind to `string`. **Closed as completed 2026-09-15 via #133673**, merged to `main`. **RC 2
  carries the fix**: `QRepro1` no longer throws for the attributed union. The close is the
  attribute fix and nothing more, as expected: the unattributed union is still ambiguous under
  `Web`. Check `QRepro1`'s output on the build in hand, not the issue state.
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
- **dotnet/aspnetcore#69322** (filed 2026-09-15, union-free): Minimal API `[FromForm]` with more
  than one public constructor. The handler receives `default(T)` / `null` with 200 and nothing is
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

- **dotnet/aspnetcore#64599** (comment, 2026-10-14): the umbrella *Support C# unions*. Asks
  whether MVC validation of union cases is in scope. #68846 changed the validation source
  generator, and an `[ApiController]` action with a union body still returns 200 with
  `ModelState.IsValid == true` on RC 2 while the plain-record control returns 400. Also asks for a
  docs line that `[Required]` on a union-typed property is always satisfied. Backed by `QVal`.
  [Comment](https://github.com/dotnet/aspnetcore/issues/64599#issuecomment-6160610105). Answered the same
  day ([reply](https://github.com/dotnet/aspnetcore/issues/64599#issuecomment-6171437285)): a maintainer has a
  fix, [PR #69834](https://github.com/dotnet/aspnetcore/pull/69834) *Validate active union cases in MVC*;
  whether it is backported to net11 is undecided. **Update 2026-10-26:** #69834 merged to `main` on 2026-10-19 and
  the `release/11.0` backport ([#69929](https://github.com/dotnet/aspnetcore/pull/69929)) merged the same day, but
  RC 3 (`11.0.100-rc.3.26522.148`) still returns 200 for the union body in `QVal`. Re-run `QVal` on GA.
- **dotnet/aspnetcore#69826** (filed 2026-10-14): one Minimal API endpoint taking a union the
  structural classifier refuses makes every route return 500, and with `AddControllers()` or
  `AddAuthorization()` startup throws, while the same union on an MVC action fails only that
  action. Asks whether the failure should be scoped to the endpoint. Backed by `QOa` and `Probe2`
  section G. [Issue](https://github.com/dotnet/aspnetcore/issues/69826). Answered 2026-10-16
  ([comment](https://github.com/dotnet/aspnetcore/issues/69826#issuecomment-6199377701)): not
  union-specific (an unregistered service inferred as a GET body fails the same way, and inferred
  GET bodies are rejected on purpose, #35405); no behaviour change this late in .NET 11; diagnostics,
  opt-in early validation and per-endpoint isolation to be revisited after it. Follow-up
  ([comment](https://github.com/dotnet/aspnetcore/issues/69826#issuecomment-6208367953)): agreed,
  noted that `AddAuthorization()` already builds every endpoint in `StartAsync` (the
  `AuthorizationPolicyCache` frames `Probe2` section G now prints), so opt-in early validation would
  make that deliberate; asked for a milestone. Issue still open, no milestone.
- **dotnet/runtime#135742** (filed 2026-10-14, closed as answered the same day): a state union
  with data-less cases cannot be classified by `JsonUnionTypeStructuralClassifier`. The refusal is
  by design (dotnet/runtime#129805); the issue asked for guidance (a hand-written classifier or a
  `closed` hierarchy) and whether the message should mention `[JsonRequired]` for the subset case.
  Backed by `UnionWeb` and `Probe2`. Answer ([comment](https://github.com/dotnet/runtime/issues/135742#issuecomment-6170098759)):
  two data-less cases both serialize to `{}`, so no structural classifier, custom ones included, can
  recover the case; use a closed class or record hierarchy with a type discriminator
  (`[JsonPolymorphic(InferClosedTypePolymorphism = true)]`) instead of a union. `[JsonRequired]`
  only changes which payloads a case accepts and does not resolve the ambiguity, so the message
  should not suggest it as a general fix. `QClosedJson` confirms the recommended shape on RC 2: the
  same five-case state as a `closed` hierarchy round-trips every case, both data-less ones included.
  [Issue](https://github.com/dotnet/runtime/issues/135742).

Not reported, because upstream already covers them:

- the null-state rule, which is by design (dotnet/roslyn#85054);
- union validation in Minimal APIs, fixed for RC 2 (dotnet/aspnetcore#68268, PR #68846); the MVC
  gap is the #64599 comment above;
- the `record class` provider overflow (dotnet/docs#55322).

## Not compiled

Only two claims in these pages come from documents rather than a run:

- that the compiler calls `TryGetValue`/`HasValue` instead of `Value` when they exist (the
  samples run, but the call was not observed);
- the four behavioural obligations of hand-written unions, which the compiler does not check, so
  there is nothing to compile.
