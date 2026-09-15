# 4. System.Text.Json: a union does not round-trip; a `closed` hierarchy does

*Verified on SDK `11.0.100-rc.1.26425.128`, 2026-09-10/11. Probes: `UnionWeb`, `Probe2`, `QOa`.*
*Originally reported for Preview 7 by Andrew Lock ("the pain of serializing unions and closed class
hierarchies with System.Text.Json", 2026-09-01). Everything below was reproduced on RC 1, message
text included.*

## A union serializes as its case, with no wrapper and no discriminator

`IntOrString` holding `42` serializes to the bare number `42`. A `SupportedOS` holding `Windows`
serializes to `{"Version":"11"}`. Nothing in the payload says which case it was. That is
deliberate: the design target is a TypeScript-shaped API surface, not round-tripping.

The consequence lands at **deserialize** time. It succeeds automatically only when every case maps
to a distinct JSON value kind (Number vs String vs Object). Two object-shaped cases and you get:

> `JSON value type 'Object' is ambiguous for union type 'SupportedOS' because multiple case types
> can use this value type. Specify a custom type classifier to support deserialization.`

So a state union such as `union FourEyesApproval(NotRequired, PendingApproval, PartlyApproved,
FullyApproved, Rejected)`, five object cases, three of them distinguishable only by which `Guid`
fields are present, **cannot** be deserialized without a classifier. Serialization still "works",
which is the trap: the write side is silent and the read side fails, possibly in a different
service.

## The hand-written classifier is a payload-shape heuristic

The documented fix is structural sniffing: guess the case from which properties appear.

```csharp
public class SupportedOSClassifier : JsonTypeClassifierFactory<SupportedOS>
{
    public override JsonTypeClassifier CreateJsonClassifier(
        JsonTypeClassifierContext context, JsonSerializerOptions options)
    {
        return static (ref Utf8JsonReader reader) =>
        {
            if (reader.TokenType is not JsonTokenType.StartObject) return null;
            while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
            {
                if (reader.ValueTextEquals("Distro"u8)) return typeof(Linux);
                if (reader.ValueTextEquals("Name"u8))   return typeof(MacOS);
                reader.Read();
                reader.Skip();
            }
            return null;
        };
    }
}

[JsonUnion(TypeClassifier = typeof(SupportedOSClassifier))]
public union SupportedOS(Windows, Linux, MacOS);
```

Hand-written, it is first-match-wins: unsound wherever two cases can produce the same property
set, and it silently mis-classifies rather than throwing. (This sample is from the documentation
and was not compiled here; its generic base `JsonTypeClassifierFactory<T>` does exist in RC 1, and
a non-generic `JsonTypeClassifierFactory` subclass was compiled and run, see
[05](05-aspnet-core.md).)

## RC 1 ships a classifier, and it is checked

`JsonUnionTypeStructuralClassifier` is a ready-made factory you point the attribute *at*:

```csharp
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union PetUnion(Dog, Cat);
```

What it does, verified:

- **It refuses an unclassifiable union outright, when the type's JSON contract is built** rather
  than per payload. Two cases with the same property set, or one whose names are a subset of
  another's, is a `NotSupportedException`:

  > `The JsonUnionTypeStructuralClassifier cannot classify union type 'SubsetU' because the case
  > type 'Small' can never be selected uniquely. The case type 'Big' recognizes every property name
  > recognized by 'Small' and has no stricter required-property or unmapped-member constraints.`

  A `required` member (or `[JsonRequired]`) on the larger case makes the subset legal:
  `{name}` → `Small`, `{name, extra}` → `BigReq`.
- **At read time it throws rather than guesses.** A payload that names no distinguishing property
  (`{"name":"X"}`), or names more than one case's (`{"name":…,"coat":…,"breed":…}`), is a
  `JsonException`: *"The custom type classifier for union type 'UnionPet' returned null for JSON
  token type 'StartObject'"*. It is never a coin toss.
- **It uses the contract's JSON names**, so it follows the naming policy and
  `PropertyNameCaseInsensitive`. camelCase input fails under `JsonSerializerOptions.Default`;
  PascalCase input passes under `JsonSerializerOptions.Web`.
- **The residual silent mode is the downgrade.** Unknown properties are skipped, so a payload that
  *lost* its distinguishing property (a typo, an older client) is classified as the smaller case it
  now resembles. `{"name":"X","extr":"e"}` against `union SubsetReq(Small, BigReq)` is a valid
  `Small`, HTTP 200. `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]` on the
  cases closes it: the smaller case then rejects the stray property, the larger one lacks its
  required member, and the same payload is a `JsonException`. Verified with the attribute on both
  cases.
- **Polymorphic or union-typed cases are refused** when the contract is built: *"… because the
  case type 'PolyBase' uses polymorphism or is itself a union type. Union cases that use
  polymorphism or are union types are not supported."*

### Payload-less cases are the blind spot

`{}` recognizes no property names, so it is a subset of every case, and the subset rule applies:

- **One payload-less case is refused beside any case with no required member.**
  `union OneEmpty(Empty1, Distinct(string Code))` and `union (Empty1, AllOpt(string? D = null))`
  are both contract-time `NotSupportedException`s. Positional record parameters are not required
  members.
- **Making every other case's distinguishing member `required` makes one empty case legal**, and
  brings back the downgrade in its worst form. `{"Cod":"c"}` (a typo for `Code`) against
  `union OneEmptyReq(Empty1, DistinctReq)` is a valid `Empty1`. In a state union the payload-less
  case is usually "nothing has happened yet", so a malformed transition is silently read as no
  transition. `Disallow` on the empty case should stop this, by the result above; that exact shape
  has not been run.
- **Two payload-less cases** both match `{}`, and no constraint can separate them.

`FourEyesApproval` meets every condition: two empty cases, and three `Guid` cases with no required
member. The refusal takes the whole type with it, including `{"rejector":"…"}`, which is
unambiguous on its own.

**A state union is structurally classifiable only if it has at most one payload-less case and every
other case carries a required member.** Even then it is not a `$type` field.

## A `closed` hierarchy serializes with a real discriminator

```jsonc
{"Pet": {"$type":"Labrador", "FavouriteToy":"Ducky", "Hungry":true, "Name":"Goose"}}
```

via `[JsonDerivedType]`, or automatically with

```csharp
var opts = new JsonSerializerOptions { InferClosedTypePolymorphism = true };
```

RC 1 also allows it per type, which is what a library must use because an options-level flag is
not its to set: `[JsonPolymorphic(InferClosedTypePolymorphism = true)]` on the base.

Two edges on the inference, both reproduced:

- **Multi-level hierarchies with abstract intermediates break it.** *"Derived types … cannot be
  abstract classes or interfaces unless `JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor`
  is specified"*, and the sibling *"Runtime type 'Labrador' is not supported by polymorphic type
  'Pet'"*. The failure is not confined to the deep case: serializing a sibling with no abstract
  ancestor (`Cat`) throws *"Specified type 'Canine' is not a supported derived type for the
  polymorphic type 'Pet'"*, because it is the base's contract that is rejected. You are back to
  annotating every case by hand.
- **Every inferred derived type must be at least as accessible as the base.** Practically, one
  accessibility level for the whole hierarchy, or:

  > `The inferred derived type 'Dog' is less accessible than the polymorphic base type 'Pet'.
  > Inferred derived types must be at least as accessible as the base type.`

## Design consequence

For a wire or document contract the trade runs against the union: the hierarchy gets a `$type` and
round-trips; the union gets a shape classifier that refuses most state unions. Keep the union where
its exhaustiveness is paid for, in the domain, and let the JSON boundary see either a DTO with an
explicit discriminator or a `closed` hierarchy with `$type`. Do not put a bare union in a persisted
document or a public contract.

A source-generated `JsonSerializerContext` with `[JsonSerializable(typeof(UnionIntString))]` does
serialize and deserialize the union in an ordinary JIT run. A trimmed `PublishAot` build was not
probed.
