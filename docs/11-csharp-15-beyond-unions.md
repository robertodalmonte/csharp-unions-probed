# 11. C# 15 beyond unions: `with(...)`, extension indexers, labeled jumps, memory safety

*GA re-run on SDK `11.0.100` (Roslyn `5.12.0.0`, compiler build
`5.12.0-1.26529.118`, runtime `11.0.0`), 2026-11-11: every `QCsharp15` case
reproduces the RC 3 result. The output differs only in compiler build/path and diagnostic
ordering. `QIde0410` still reports the two `goto` warnings and no flag-variable warning.
The six findings below therefore also hold on GA, including `CS0227` in `U10`.
[GA output](../probes/results/ga/QCsharp15-run.txt).*

*Compiled on SDK `11.0.100-rc.3.26522.148` (Roslyn 5.12.0, runtime 11.0.0) on 2026-11-07, at the
default language version (`LanguageVersion.Default` = C# 15, no `LangVersion` line). Probes:
`QCsharp15` (every case below, by id; full output in
[`probes/QCsharp15/output-rc3.txt`](../probes/QCsharp15/output-rc3.txt)) and `QIde0410`.*

The other pages probe what a `union` does. This one checks the rest of C# 15 against the claims of
a third-party "csharp-15" agent skill (a `SKILL.md` an agent loads to write C# 15 correctly). It was
read in full, every checkable claim became a case, and each case was compiled in-process with the
SDK's own Roslyn: a case that compiles is emitted, loaded and run with its output captured; one
that does not prints the diagnostics. Ids: `W` = `with(...)`, `E` = extension indexers, `L` =
labeled jumps, `M` = breaking changes, `U` = the memory-safety preview.

**Result: the skill is right on almost every compiler fact it states, and imprecise or incomplete
in six places.** The six are listed first; the confirmations follow.

## Where the skill is imprecise or wrong on RC 3 and GA

1. **Spread enumeration order is not "may happen later".** The skill says that for
   `.. GetItems()` the call happens in position but *enumerating* may happen after later elements
   are evaluated. `W17` and `W26` log it: for `List<T>`, `HashSet<T>`, a `[CollectionBuilder]`
   type, `ImmutableArray<T>`, `int[]` and `IEnumerable<T>` the order is always
   `e1, call, enumerate, e2`. The spec allows other orders; this compiler never produces one.
   Code that relies on in-position enumeration works today and is not guaranteed.
2. **A builder with the span first is not just a `with(...)` problem (`W21`).** The skill says do
   not put the span first with optional parameters after it "because `with(...)` can't bind". It is
   worse: `Create<T>(ReadOnlySpan<T> items, IEqualityComparer<T>? c = null)` makes the type invalid
   as a collection-expression target at all. `["x"]`, `[with(), "x"]` and `[with(cmp), "x"]` all give
   `CS9187` (*a static method whose last parameter is of type `ReadOnlySpan<T>`*). The same error
   existed before C# 15 for the plain form.
3. **`IDE0410` does not flag the flag-variable pattern (`QIde0410`).** The skill lists "replace a
   Boolean found flag" under what the analyzer flags. With `IDE0410` at warning, `EnforceCodeStyleInBuild`
   and `AnalysisLevel=latest-all`, only the two `goto` forms warn (`goto END` after a loop and
   `goto NEXT` with `NEXT: ;` at the end of the body). The `found = true; break;` loop with
   `&& !found` in the condition is silent.
4. **`U10` reports `CS0227` on RC 3 and GA.** The skill's diagnostic table maps `safe`/`unsafe`
   modifiers and blocks without `AllowUnsafeBlocks` to `CS9401 / CS0227`, and says "newer compilers
   (after .NET 11.0.100)" report `CS9401`. This compiler reports `CS0227` for the modifier, the
   block and `unsafe(expr)` alike. `CS9401` is a claim about a compiler that is not installed
   here; untested, not refuted.
5. **"Never used for string or arrays" is wider than "colliding shapes" (`E05`, `E06`, `E17`, `E17b`).**
   The skill is right that an extension `this[int]` on `string` or `int[]` is ignored (it
   compiles and `"abc"[1]` is `b`). But a *non-colliding* extension indexer on `string` or on an
   array is not usable either: `"abc"["k"]` is `CS1503` against the real indexer. A type with an
   ordinary instance indexer behaves differently: `List<int>["k"]` through an extension
   `this[string]` works (`E17`, `ext:k1`). So the rule is "arrays and `string` never fall through
   to extension indexers", not "an instance indexer always wins".
6. **A duplicate extension indexer reports `CS0021`, not an ambiguity (`E16`).** Two extension
   indexers with the same receiver and signature in different classes give *Cannot apply indexing
   with [] to an expression of type 'Holder'*; with one of them removed the call compiles
   (`E16b`). Nothing in the message says "ambiguous". The same `CS0021` is what the
   inference-from-assigned-value failure gives (`E12`).

## Confirmed on RC 3 and GA

### `with(...)` (section 1)

| Claim | Case | Result |
|---|---|---|
| `[with(capacity: n), ..xs]` on `List<T>` presizes | `W01` | `Count 2, Capacity 8` and `4` |
| `HashSet` with a comparer collapses duplicates | `W02` | 1 element |
| `IList<T>` / `ICollection<T>` build a `List<T>` and take `capacity` or nothing | `W03` | `List<int>` capacity 100 and 4 |
| `[with([1, 2]), 3]` binds the copy constructor | `W04` | `1,2,3` |
| Builder type with `Create(comparer, span)`: `with(cmp)` keeps 2 of `"A","a","B"`; `with()` and plain work | `W05` | `2 1 2` |
| A constructor-argument-only type is a target only with `with(...)` | `W06`, `W07` | works / `CS9214` |
| `with` not first, or a second `with` | `W08`, `W24` | `CS9354` |
| Arrays and spans reject even an empty `with()` | `W09` | `CS9355` on `int[]`, `Span<int>`, `ReadOnlySpan<int>` |
| `IEnumerable`/`IReadOnlyCollection`/`IReadOnlyList`: only `with()` | `W10`, `W11` | works / `CS9357` |
| Overload resolution and inference ignore the arguments | `W12`-`W14` | `CS0121`; fixed target resolves; `Use([with(10)])` is `CS0411`, `Use<int>(...)` compiles |
| `[with(default)]` on `List<int>` is ambiguous | `W15` | `CS0121` (plus `CS8716`) |
| `dynamic` arguments are an error | `W16` | `CS9356` |
| `with` arguments are evaluated first | `W17` | log starts `with` |
| `@with(...)` calls a method named `with`; unescaped it is collection arguments | `W18`, `W19` | `3,3` / `CS1729` on `List<int>(1, 2)` |
| Dictionaries: `Dictionary<,>` takes `with(cmp)`; the interface targets are not constructible | `W20` | compiles / `CS9174` for `IDictionary` and `IReadOnlyDictionary` |
| `[with(5)]` is capacity, classic initializers unchanged | `W22`, `W25` | `0 5`, `2 4` |
| Immutable and sorted targets | `W23` | `ImmutableArray` 2, `ImmutableHashSet` 1, `SortedSet` first `a` |

### Extension indexers (section 4)

| Claim | Case | Result |
|---|---|---|
| Basic and two-parameter get/set indexers | `E01`, `E02` | `3`; `Debug \| Logging:Level` |
| Receiver must be named | `E03` | `CS9303` |
| An extension never hijacks an instance indexer | `E04` | `5 5` |
| Not usable in expression trees | `E07` | `CS9296` |
| Extension `Count` + `this[int]` + `Slice` unlock `^1`, ranges, list patterns | `E08` | `40 20 True` |
| `[IndexerName("X")]` renames the static accessor | `E09` | `get_X` vs `get_Item`, both callable directly |
| Forbidden modifiers | `E10` | `virtual`, `override`, `new`, `sealed`, `static` are `CS0106`; `abstract` `CS0106` + `CS0501`; `protected` `CS9302`; `init` `CS9304`; `partial` `CS9248` + `CS0751`. `static` is an error the skill does not list |
| A generic block's type parameter must be used | `E11` | `CS9295` only when it appears in neither receiver nor indexer parameters |
| Inference never uses the assigned value | `E12` | `i[42, null] = new object()` fails; explicit `E.set_Item(i, 42, null, new object())` and `i[42, "s"] = "v"` compile |
| `x?[a, b]` and object-initializer index assignment | `E13` | `1`, `null`, `xy=5` |
| `ref`-returning extension indexer | `E14` | `1,8,3` |
| Lookup tiers: instance implicit-Index support beats an extension `this[Index]`, which beats implicit support from extension members | `E15`, `E15b` | `instance1 instance2`; `ext-int1 ext-Index` |

### Labeled jumps (section 5)

All hold: `break`/`continue` to `for`, `foreach`, `while` and `do` labels (`L01`); `break` a labeled
`switch` from inside a `foreach` leaves the whole switch (`L02`); `continue` to a switch is `CS9394`
(`L03`); only the statement right after a label is labeled, `a: b: while` with `break a` is `CS9393`
(`L04`, `L05`); a label on a plain block is not a target (`L06`); finally blocks run innermost
first (`L07`); leaving a finally is `CS0157` (`L08`); a lambda is `CS1632`, a local function
`CS9394` (`L09`); the same label on nested loops is `CS0158`, on sequential loops in one block
`CS0140`, and fine in sibling blocks (`L10`-`L12`); one label serves both jumps (`L13`); `goto`
to the label still works (`L14`); iterators and `async` methods work (`L15`).
New detail: a label that is declared but never jumped to is warning `CS0164`, including `a` in
`a: b: while` (`L04`, `L05`, `L17`).

### Section 0 and 8 (version check, breaking changes)

`#error version` gives `CS1029` plus `CS8304` naming compiler `5.12.0` and *Language version:
default (15.0)* (`M01`). `CS8352` for the loop-local span (`M02`), `CS0027` for `nameof(this.P)` in
an attribute (`M03`), `CS7083` for interface `&&` dynamic (`M04`), `CS9380` and two `CS1519` for a
type named `closed` (`M05`), `CS9370` for `union Foo;` (`M06`).

### Memory-safety preview (section 7; the `dotnet` skill had these as unverified)

| Claim | Case | Result |
|---|---|---|
| Calling a requires-unsafe member from safe code | `U01`, `U02` | `CS9362`; inside `unsafe { }` compiles |
| `unsafe` on a signature no longer opens a body context | `U03`, `U04` | `CS9360` with the feature flag; compiles without it |
| A safe signature with a pinned array and an inner block discharges the obligation | `U05` | prints `6` |
| `unsafe(expr)` in a field initializer | `U06`, `U06b` | `42`; without it `CS9362` |
| `unsafe` on a type, static constructor or finalizer | `U07` | `CS9377` each |
| `unsafe` on a lambda | `U07` | not a modifier error: a syntax error (`CS1525`, `CS1003`, `CS1002`) |
| `await` in `unsafe` yes, in `fixed` no | `U08` | `CS9398` on `fixed` only |
| `extern` must say `safe` or `unsafe` | `U09` | `CS9389` on the bare one; `safe` and `unsafe` forms compile |
| Explicit-layout fields must say `safe` or `unsafe` | `U13` | `CS9392`; `safe` resolves it |
| Pointer relaxations need only `preview`, even with `AllowUnsafeBlocks` off | `U11`, `U12` | compile; `CS0214` without preview |

## Not tested

- Non-virtual static interface members on `netstandard2.0` / .NET Framework (section 6): needs an
  older target framework, not available in this harness.
- `CS9401` (item 4 above) and `CS9376` (a requires-unsafe parameterless constructor against
  `new()`).
- Whether the extension `Count`/`Length` participates in the collection-expression spread
  presize; it is not observable from output.
- The Learn pages and the spec links in the skill's source list.

## How to re-run

```
dotnet build probes/QCsharp15
dotnet run --no-build --project probes/QCsharp15            # all cases
dotnet run --no-build --project probes/QCsharp15 -- E1      # ids starting with E1
dotnet build probes/QIde0410                                 # IDE0410 warnings on lines 15 and 24
```
