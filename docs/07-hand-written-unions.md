# 7. Hand-written unions: when the generated struct is wrong

*Verified on SDK `11.0.100-rc.1.26425.128`, 2026-09-11. Probes: `QLang` (the language reference's
samples), `QLang2` (`CLASS_PROVIDER`, `REC_RECURSE`, `CAST_FIX`).*

The generated form is opinionated: always a struct, always `object?` storage, always boxes
value-type cases. There are three documented ways out, all keyed on
`[System.Runtime.CompilerServices.Union]` plus a public `Value` property plus one single-parameter
public constructor per case type.

| Need | Mechanism |
|---|---|
| Reference identity, inheritance, or a union that participates in an existing class hierarchy | **Class-based union.** Put `[Union]` on a `class` implementing `IUnion`. `null` then matches both a null *reference* and a null `Value`. |
| Value-type cases without boxing (`union IntOrBool(int, bool)` on a hot path) | **Non-boxing access pattern.** Add `bool HasValue` and one `bool TryGetValue(out TCase)` per case. The compiler is documented to prefer `TryGetValue` over `Value` when lowering pattern matches. (The samples run; the call itself was not observed.) |
| A private constructor or factory creation (a `record class` union, say) | **`IUnionMembers` provider.** A nested `IUnionMembers` interface with `static Create(...)` factories and `Value` (plus optional `TryGetValue` / `HasValue`). The compiler generates calls **only** to members declared on that interface; a member on the union type but absent from the interface is never used. |

All three of the language reference's samples compile and run unchanged on RC 1: `Shape`, the
non-boxing `IntOrBool`, the class-based `Result<T>`, and the `IUnionMembers` `Outcome<T>`
(`Outcome<int> ok = 42` → `int`, an exception → `Exception`, `default` → `null`). Also verified:
`Create` as `static abstract` members implemented on the union type, `HasValue` on the interface,
and a `record class` union with a private constructor.

## The `record class` provider stack overflow

A `record class` provider with a private constructor taking `object?` plus
`Create(int i) => new(i)` **overflows the stack at runtime**. `new(i)` binds through the union's
own conversion back to `Create`, most likely via the record's synthesized copy constructor, whose
parameter is the union type. The identical shape as a plain `sealed class` works (`CLASS_PROVIDER`
isolates it).

Two fixes, both verified (`CAST_FIX`):

- give the private constructor a signature no case type converts to, e.g. `(object? value, bool _)`;
- cast the argument: `new((object?)i)`.

This is dotnet/docs#55322, which reported the overflow against the reference's own `Outcome<T>`
sample; that sample is now a struct.

## Four obligations the compiler assumes and does not verify

- **Soundness**: `Value` is null or exactly one case type.
- **Stability**: a union round-trips the case type it was built from.
- **Creation equivalence**: ambiguous inputs behave identically however they are created.
- **Access-pattern consistency**: `TryGetValue` / `HasValue` agree with `Value`.

Violate one and pattern matching silently misbehaves. There is nothing to compile here; the
compiler does not check them. Reach for a custom union only when the table above names your
reason.
