# 5. ASP.NET Core: body only, and the Web defaults break the flagship example

*Verified on `Microsoft.AspNetCore.App 11.0.0-rc.1.26425.128` and `Microsoft.AspNetCore.OpenApi`
of the same version, 2026-09-11 and 2026-09-15, one Kestrel host per probe. Probes: `UnionWeb`,
`Probe2`, `RdgNoString`, `QOa`, `QVal`, `QParsable`.*

Microsoft's own account is *C# Unions and Closed Hierarchies in ASP.NET Core* (Dmitrii Korolev,
.NET Blog, 2026-09-10). Unions and closed hierarchies work anywhere ASP.NET Core hands the payload
to System.Text.Json: Minimal API bodies and results on both the reflection and the Request Delegate
Generator (RDG) paths, MVC formatters including `Task<T>`/`ValueTask<T>`, SignalR's
`JsonHubProtocol`, Blazor JS interop and persisted state. In the article's words, *"binding sources
that don't route through STJ don't support unions"*: query, route, header, form, and Blazor's
`[SupplyParameterFromQuery]`/`[SupplyParameterFromForm]`. The article does not say what "don't
support" does. Probed, it is four different behaviours, and one of them is silent.

## The headline type rejects every string in a body

ASP.NET Core deserializes with `JsonSerializerDefaults.Web`, which sets
`NumberHandling = AllowReadingFromString`. The `int` case can therefore claim a JSON String token,
so every string is ambiguous:

| Body posted to `union UnionIntString(int, string)` | Minimal API | MVC `[ApiController]` |
|---|---|---|
| `42` | 200 `42` | 200 `42` |
| `"hello"` | **400**: `JSON value type 'String' is ambiguous for union type 'UnionIntString'` | **400** ProblemDetails, same message under `errors.$` |

Any `JsonSerializerOptions.Web` consumer hits the same thing. It reproduces directly against
`JsonSerializer`, and `HttpClient.GetFromJsonAsync<UnionIntString>` against an endpoint returning
`"hello"` throws the same `JsonException`.

**This is by design and will not change.** The System.Text.Json area owner ruled so on
dotnet/runtime#133668 (2026-09-11). The proposed alternative, an exact token-kind match winning
before number-from-string coercion is considered, was rejected. Under the Web defaults a union
whose cases are distinct JSON kinds under `Default` is ambiguous, and `(int, string)` needs one of
the fixes below in any ASP.NET Core app.

SignalR is the exception the article mentions. The reason is its options, not special union
handling: `JsonHubProtocolOptions.PayloadSerializerOptions` uses `NumberHandling = Strict`,
camelCase naming and case-insensitive matching, and under those `"hello"` deserializes as the
`string` case.

### The fixes, as tested

- **Works, app-wide:** `builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict)`.
  `"hello"` then binds as the `string` case (200), and the OpenAPI schema is corrected with it.
  The cost: every numeric property in every Minimal API stops accepting `"42"`, a contract change
  of its own.
  - **MVC is not covered by it.** With only `ConfigureHttpJsonOptions` set, the same body posted to
    a controller is still 400. MVC also needs
    `AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict)`.
- **Does nothing on RC 1, a confirmed bug:** `[JsonNumberHandling(JsonNumberHandling.Strict)]` on
  the union type. Still 400; the cases' number handling is not taken from the union's attribute.
  The area owner called this "unexpected and should be fixed" (dotnet/runtime#133668, labelled
  `bug`, milestone `11.0.0`). Once fixed it becomes the per-type fix that changes nothing
  app-wide. **Re-run `QRepro1` at RC 2 and GA before relying on it.**
- **Works, per type:** a custom classifier that decides by token kind.

  ```csharp
  public class StringFirstClassifier : JsonTypeClassifierFactory
  {
      public override bool CanClassify(JsonTypeClassifierContext context) => true;

      public override JsonTypeClassifier CreateJsonClassifier(
          JsonTypeClassifierContext context, JsonSerializerOptions options) =>
          static (ref Utf8JsonReader r) => r.TokenType switch
          {
              JsonTokenType.String => typeof(string),
              JsonTokenType.Number => typeof(int),
              _ => null,
          };
  }

  [JsonUnion(TypeClassifier = typeof(StringFirstClassifier))]
  public union IntOrString(int, string);
  ```

  Under `JsonSerializerOptions.Web`: `"hello"` → `string`, `"42"` → `string`, `42` → `int`. Note
  the second result: the classifier now decides that a quoted number is text.

### The OpenAPI document is wrong in the same place

Under the default Web options, the `int` arm is published as
`{"type":["integer","string"],"pattern":"^-?(?:0|[1-9]\\d*)$","format":"int32"}` next to a
`{"type":"string"}` arm. The document declares the overlap and then advertises a string arm the
server rejects with 400. With `NumberHandling = Strict` in `ConfigureHttpJsonOptions`, the schema
becomes `{"anyOf":[{"type":"integer","format":"int32"},{"type":"string"}]}`, which matches the
server.

## Non-body binding sources

A union that does **not** implement `IParsable<T>`, by source; the last row is one that does:

| Binding | Compile time | Minimal, reflection | Minimal, RDG | MVC |
|---|---|---|---|---|
| `[FromQuery]`, `[FromHeader]` | **`ASP0020`, an error by default** | (if suppressed) 500 at first request: *"must have a valid TryParse method"* | (if suppressed) **200, and `?v=42` arrives as the `string` case `"42"`**; a union with **no** `string` case fails the build with `CS0029` in the generated file | 500 for both: *"Could not create an instance of type 'UnionIntString'. Model bound complex types must not be abstract or value types…"* |
| route `{v}` | **`ASP0020`** | (if suppressed) 500: *"Body was inferred but the method does not allow inferred body parameters"* | 400: *"Implicit body inferred…"* | `[ApiController]` infers `[FromBody]`, so **415** on a GET |
| `[FromForm]` | **nothing**: no `ASP0020`; RDG emits only `RDG003` and falls back to reflection | **200, and the handler receives `default(TUnion)` with `Value == null`** | same | **500**, the same message as `[FromQuery]` |
| `[AsParameters]` record with a union property | nothing | 500, body inferred | 400, body inferred | n/a |
| **any of the above, union implements `IParsable<TUnion>`** | nothing; `ASP0020` does not fire | **200, the case `TryParse` chose**, from query, header, route and form | same; the generated file reports `IsParsable = True` | **200**, same, `[ApiController]` included |

### `IParsable<T>` on the union is the whole fix

A union body may carry an interface list and methods:

```csharp
public union IntOrString(int, string) : IParsable<IntOrString>
{
    public static IntOrString Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var r) ? r : throw new FormatException();

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out IntOrString result)
    {
        if (s is null) { result = default; return false; }
        result = int.TryParse(s, NumberStyles.Integer, provider, out var i) ? i : s;
        return true;
    }
}
```

Verified (`QParsable`): `?v=42` → `int 42`, `?v=abc` → `string abc`, a missing value → 400,
identically from `[FromQuery]`, `[FromHeader]`, route and `[FromForm]`, on reflection, RDG and MVC.
`ASP0020`'s own message names this fix. Which case wins for an input both cases could take (`"42"`)
is application policy, and `TryParse` is the right place for it. Non-body union binding is not a
missing feature; a diagnostic for the unparsable case is (dotnet/aspnetcore#66648).

### The `[FromForm]` silence is not union code

Probed with plain structs of the shape a union lowers to (`object? Value` plus constructors):

- `struct Opaque1 { Opaque1(int value); }` binds `value=42` by parameter name (200 `42`) and
  `default` for any other key. That is the ordinary missing-field rule.
- `struct Opaque2 { Opaque2(int value); Opaque2(string value); }` binds `default` with 200 for
  **every** key.

The Minimal API form mapper falls back to `default(T)` when a struct has more than one candidate
constructor. A union lowers to N constructors and inherits it. Why the mapper does this was not
established (source not read); that it does is.

### Three rules

1. **Never suppress `ASP0020` to get a union parameter to compile.** Behind it the two Minimal
   API paths disagree. Reflection throws per request. RDG passes the raw text through the union's
   implicit conversion from `string` (the generated line is `global::UnionIntString v_local =
   v_temp!;`) and returns 200, so an `int` sent as `?v=42` is silently the wrong case. For a
   union with no `string` case that generated line does not compile: `error CS0029: Cannot
   implicitly convert type 'string' to 'IntOrBool'`, reported against a generated file, while the
   reflection build of the same source succeeds.
2. **Minimal API `[FromForm]` on a non-parsable union has no guard at all.** No analyzer, no
   exception, no 400. A null-valued union reaches the handler and blows up in its first `switch`,
   or, if nothing switches on it, the handler serializes `null`. Implement `IParsable<TUnion>` on
   any union bound from a non-body source; failing that, bind the field as `string` and convert.
   MVC never binds a non-parsable union silently: it fails every non-body binding with a 500 from
   `ComplexObjectModelBinder`.
3. **MVC's ApiExplorer documents a parameter that cannot bind.** A `[FromQuery]` union appears
   in OpenAPI as a query parameter named `Value` with an empty schema: the struct's own property,
   decomposed.

## OpenAPI reflects the shape, not whether the union can be deserialized

`union UnionPet(Cat, Dog)` produces
`{"type":"object","anyOf":[{"$ref":"#/components/schemas/Cat"},{"$ref":"#/components/schemas/Dog"}]}`
identically with and without a classifier. A union the server cannot deserialize at all is
published as accepting either case. A `closed` hierarchy under `InferClosedTypePolymorphism` gets
`anyOf` plus `"discriminator":{"propertyName":"$type","mapping":{…}}` and `"required":["$type"]`.
Both use `anyOf`, not `oneOf`. The document version emitted is `3.2.0`.

## A closed hierarchy's discriminator failures are 500s

In Minimal API and MVC alike, in `Production` as well as `Development`. A body with no `$type`, or
with `$type` not first, throws `NotSupportedException: The JSON payload for polymorphic interface
or abstract type 'PaymentEvent' must specify a type discriminator`. That is HTTP 500 for a client
error.

In the out-of-order case the message is also wrong: the payload did specify one, but STJ reads
metadata only at the start of the object. `AllowOutOfOrderMetadataProperties = true`, set on both
option objects, fixes that case (200 in both stacks); a missing `$type` is still a 500. An unknown
`$type` is a `JsonException`, hence 400. If a closed hierarchy is a public request body, map that
`NotSupportedException` to 400 yourself.

## An unclassifiable union takes the whole route table down

The structural classifier's contract-time refusal ([04](04-system-text-json.md)) is thrown when
the endpoint's JSON contract is built, and *when* that happens depends on unrelated services:

- With authorization registered, the authorization middleware's policy cache enumerates endpoints
  inside `StartAsync`, and **the app fails to start**. `AddAuthorization()` alone is enough.
  `AddControllers()` registers authorization, so it has the same effect; `AddOpenApi()` does not.
- Without authorization, the endpoint builds lazily, and then **every request to every endpoint
  is a 500**, not just requests to the bad one. `GET /ok` (a plain string endpoint) and
  `/openapi/v1.json` both return the classifier's `NotSupportedException`, even when `/ok` is the
  first request the host ever sees. The stack shows why: `RouteEndpointDataSource.get_Endpoints`
  builds every Minimal API endpoint, called from `CompositeEndpointDataSource`, which merges all
  sources into one route table. **MVC routes go down with it**: `GET /mvc/ok` returns 500 beside
  the bad Minimal API endpoint. Same on reflection and RDG, and inside a `MapGroup`.
- **None of this is specific to unions.** An ordinary endpoint that fails to build (a GET whose
  record parameter is inferred as a body) takes the table down the same way. Unions only add a new
  way for an endpoint to fail at build time.

The reverse is contained. The same union on an **MVC action** fails only that action (500 on its
POST), because MVC resolves a body's JSON contract only when the action runs. A union that is
ambiguous only at *read* time (no classifier, so no contract-time refusal) poisons nothing: 400
on its own endpoint, 200 everywhere else.

## Validation does not reach inside a union on RC 1

dotnet/aspnetcore#68268 was closed as fixed for RC 2 by PR #68846 (*Support validation for C#
union cases*). **Re-check on RC 2.** On RC 1, with `builder.Services.AddValidation()` (the
generator ships with the SDK; a `Microsoft.Extensions.Validation` package reference is redundant
and draws `NU1510`), the canaries fail correctly: a record with `[MaxLength(10)]` on a constructor
parameter and a class with it on a property both return 400 with `errors.Role`. Against that
baseline:

- The same types used as union cases return **200**, for a union of records and a union of
  classes alike.
- So does a class whose `[Required] public UserCls User { get; set; }` is missing from the
  payload. A union is a struct, so `[Required]` on it is always satisfied.
- The generated `ValidatableInfoResolver` lists some union types, but case types reachable only
  through a union are not in it at all.
- `Validator.TryValidateObject(u.Value, …)` in the handler does see property attributes on class
  cases.
- MVC is no different: an `[ApiController]` action with the union body returns 200 with
  `ModelState.IsValid == true`, while the canary record body returns 400.

Put the invariants in the case types' constructors, or validate `Value` explicitly. Never rely on
the pipeline to validate a union body on RC 1.

## The article's design rule, sharpened

The article says: choose a union when you must preserve an established discriminator-free
contract, or when the cases cannot derive from one base class; otherwise a closed hierarchy. That
holds, with one precondition the article leaves out: a discriminator-free union contract works only
if the reader can tell the cases apart from the payload alone. That means one of:

- every case is a **distinct JSON value kind under the host's options**;
- the cases are objects whose property sets the structural classifier accepts (no payload-less
  case beside a case without required members);
- a custom classifier decides.

Under ASP.NET Core's defaults, `int` and `string` fail the first test, and a state union with
payload-less cases fails the second.

**Not probed:** Blazor, and a trimmed `PublishAot` build. SignalR was checked at the level of its
payload options, not with a hub round-trip.
