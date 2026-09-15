using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

var port = 5310;

Console.WriteLine("=== 1. Raw System.Text.Json: Default vs Web ===");
foreach (var (ctx, o) in new[] { ("Default", JsonSerializerOptions.Default), ("Web", JsonSerializerOptions.Web) })
{
    Try(ctx, "ser UnionIntString(42)", () => JsonSerializer.Serialize(new UnionIntString(42), o));
    Try(ctx, "ser UnionIntString(\"hello\")", () => JsonSerializer.Serialize(new UnionIntString("hello"), o));
    Try(ctx, "deser 42", () => Show(JsonSerializer.Deserialize<UnionIntString>("42", o)));
    Try(ctx, "deser \"hello\"", () => Show(JsonSerializer.Deserialize<UnionIntString>("\"hello\"", o)));
    Try(ctx, "deser \"42\"", () => Show(JsonSerializer.Deserialize<UnionIntString>("\"42\"", o)));
    Try(ctx, "ser default(UnionIntString)", () => JsonSerializer.Serialize(default(UnionIntString), o));
    Try(ctx, "deser null -> UnionIntString", () => Show(JsonSerializer.Deserialize<UnionIntString>("null", o)));
    Try(ctx, "deser null -> UnionNullableIntString", () => Show(JsonSerializer.Deserialize<UnionNullableIntString>("null", o)));
    Try(ctx, "ser UnionPet(Cat)", () => JsonSerializer.Serialize(new UnionPet(new Cat("Tom", "tabby")), o));
    Try(ctx, "deser UnionPet Pascal Cat", () => Show(JsonSerializer.Deserialize<UnionPet>("""{"Name":"Tom","Coat":"tabby"}""", o)));
    Try(ctx, "deser UnionPet camel Cat", () => Show(JsonSerializer.Deserialize<UnionPet>("""{"name":"Tom","coat":"tabby"}""", o)));
    Try(ctx, "deser UnionPet camel Dog", () => Show(JsonSerializer.Deserialize<UnionPet>("""{"name":"Rex","breed":"lab"}""", o)));
    Try(ctx, "deser UnionPet {name} only", () => Show(JsonSerializer.Deserialize<UnionPet>("""{"name":"X"}""", o)));
    Try(ctx, "deser UnionPet {Name} only", () => Show(JsonSerializer.Deserialize<UnionPet>("""{"Name":"X"}""", o)));
    Try(ctx, "deser UnionPet coat+breed", () => Show(JsonSerializer.Deserialize<UnionPet>("""{"name":"X","coat":"c","breed":"b"}""", o)));
    Try(ctx, "deser UnionPet breed first", () => Show(JsonSerializer.Deserialize<UnionPet>("""{"breed":"b","name":"X","coat":"c"}""", o)));
    Try(ctx, "deser UnionPet unknown prop", () => Show(JsonSerializer.Deserialize<UnionPet>("""{"name":"X","colour":"c"}""", o)));
    Try(ctx, "deser UnionPetNoClassifier", () => Show(JsonSerializer.Deserialize<UnionPetNoClassifier>("""{"name":"X","coat":"c"}""", o)));
    Try(ctx, "deser UnionTwins {name}", () => Show(JsonSerializer.Deserialize<UnionTwins>("""{"name":"X","Name":"X"}""", o)));
    Try(ctx, "deser Approval {}", () => Show(JsonSerializer.Deserialize<Approval>("{}", o)));
    Try(ctx, "deser Approval approver", () => Show(JsonSerializer.Deserialize<Approval>("""{"approver":"6f1c2b9e-0000-0000-0000-000000000001","Approver":"6f1c2b9e-0000-0000-0000-000000000001"}""", o)));
    Try(ctx, "deser Approval rejector", () => Show(JsonSerializer.Deserialize<Approval>("""{"rejector":"6f1c2b9e-0000-0000-0000-000000000001","Rejector":"6f1c2b9e-0000-0000-0000-000000000001"}""", o)));
    Try(ctx, "deser SubsetU {name}", () => Show(JsonSerializer.Deserialize<SubsetU>("""{"name":"X","Name":"X"}""", o)));
    Try(ctx, "deser SubsetReq {name}", () => Show(JsonSerializer.Deserialize<SubsetReq>("""{"name":"X","Name":"X"}""", o)));
    Try(ctx, "deser SubsetReq {name,extra}", () => Show(JsonSerializer.Deserialize<SubsetReq>("""{"name":"X","extra":"e","Name":"X","Extra":"e"}""", o)));
    Try(ctx, "deser SubsetReq {name,typo}", () => Show(JsonSerializer.Deserialize<SubsetReq>("""{"name":"X","extr":"e","Name":"X","Extr":"e"}""", o)));
    Try(ctx, "ser PaymentEvent",() => JsonSerializer.Serialize<PaymentEvent>(new PaymentAuthorized("p1", 9.5m), o));
    Try(ctx, "roundtrip PaymentEvent", () =>
    {
        var s = JsonSerializer.Serialize<PaymentEvent>(new PaymentAuthorized("p1", 9.5m), o);
        return JsonSerializer.Deserialize<PaymentEvent>(s, o)!.GetType().Name;
    });
}

Console.WriteLine();
Console.WriteLine("=== 2. Minimal API + MVC + OpenAPI, happy paths ===");
{
    var (app, h) = await Start(port++, s => { s.AddControllers(); s.AddOpenApi(); }, a =>
    {
        a.MapPost("/int-string", (UnionIntString v) => v);
        a.MapGet("/int-string", () => new UnionIntString(42));
        a.MapGet("/default", () => default(UnionIntString));
        a.MapPost("/pet", (UnionPet p) => p);
        a.MapPost("/pet-nc", (UnionPetNoClassifier p) => p);
        a.MapPost("/int-string-strict", (UnionIntStringStrict v) => v);
        a.MapPost("/payment", (PaymentEvent e) => e.GetType().Name);
        a.MapGet("/payment", PaymentEvent () => new PaymentAuthorized("p1", 9.5m));
        a.MapGet("/typed", Results<Ok<UnionIntString>, NotFound> () => TypedResults.Ok(new UnionIntString(7)));
        a.MapControllers();
        a.MapOpenApi();
    });
    await Report("POST /int-string 42", PostJson(h, "/int-string", "42"));
    await Report("POST /int-string \"hello\"", PostJson(h, "/int-string", "\"hello\""));
    await Report("POST /int-string \"42\"", PostJson(h, "/int-string", "\"42\""));
    await Report("GET  /int-string", h.GetAsync("/int-string"));
    await Report("GET  /default", h.GetAsync("/default"));
    await Report("POST /pet camel Cat", PostJson(h, "/pet", """{"name":"Tom","coat":"tabby"}"""));
    await Report("POST /pet camel Dog", PostJson(h, "/pet", """{"name":"Rex","breed":"lab"}"""));
    await Report("POST /pet Pascal Dog", PostJson(h, "/pet", """{"Name":"Rex","Breed":"lab"}"""));
    await Report("POST /pet {name}", PostJson(h, "/pet", """{"name":"X"}"""));
    await Report("POST /pet-nc Cat", PostJson(h, "/pet-nc", """{"name":"Tom","coat":"tabby"}"""));
    var payment = await (await h.GetAsync("/payment")).Content.ReadAsStringAsync();
    Console.WriteLine($"GET  /payment => {payment}");
    await Report("POST /payment (roundtrip)", PostJson(h, "/payment", payment));
    await Report("POST /payment no $type", PostJson(h, "/payment", """{"paymentId":"p1","amount":9.5}"""));
    await Report("GET  /typed", h.GetAsync("/typed"));
    await Report("MVC POST int-string \"hello\"", PostJson(h, "/mvc/int-string", "\"hello\""));
    await Report("MVC POST int-string 42", PostJson(h, "/mvc/int-string", "42"));
    await Report("MVC POST int-string \"42\"", PostJson(h, "/mvc/int-string", "\"42\""));
    await Report("MVC POST pet Cat", PostJson(h, "/mvc/pet", """{"name":"Tom","coat":"tabby"}"""));
    await Report("MVC POST pet-nc Cat", PostJson(h, "/mvc/pet-nc", """{"name":"Tom","coat":"tabby"}"""));
    await Report("MVC GET async", h.GetAsync("/mvc/async"));
    await Report("MVC GET query ?v=42", h.GetAsync("/mvc/q?v=42"));
    await Report("MVC GET route /r/42", h.GetAsync("/mvc/r/42"));

    await Report("POST /int-string-strict \"hello\"", PostJson(h, "/int-string-strict", "\"hello\""));
    await Report("POST /int-string-strict 42", PostJson(h, "/int-string-strict", "42"));
    await Report("POST /payment $type not first", PostJson(h, "/payment", """{"paymentId":"p1","$type":"PaymentAuthorized","amount":9.5}"""));
    await Report("POST /payment unknown $type", PostJson(h, "/payment", """{"$type":"PaymentRefunded","paymentId":"p1"}"""));
    await Report("MVC POST payment no $type", PostJson(h, "/mvc/payment", """{"paymentId":"p1","amount":9.5}"""));
    await Report("MVC POST payment roundtrip", PostJson(h, "/mvc/payment", payment));
    var raw = await (await h.GetAsync("/openapi/v1.json")).Content.ReadAsStringAsync();
    File.WriteAllText(Environment.GetEnvironmentVariable("OPENAPI_OUT") ?? "openapi.json", raw);
    var doc = JsonNode.Parse(raw)!;
    Console.WriteLine("--- OpenAPI paths (body / 200 schema) ---");
    foreach (var (path, item) in doc["paths"]!.AsObject())
        foreach (var (verb, op) in item!.AsObject())
        {
            var req = op?["requestBody"]?["content"]?["application/json"]?["schema"]?.ToJsonString();
            var res = op?["responses"]?["200"]?["content"]?["application/json"]?["schema"]?.ToJsonString();
            var prm = op?["parameters"]?.ToJsonString();
            Console.WriteLine($"{verb.ToUpperInvariant(),-5} {path}  body={req ?? "-"}  200={res ?? "-"}  params={prm ?? "-"}");
        }
    await app.StopAsync();
}

Console.WriteLine();
Console.WriteLine("=== 3. Minimal API, non-JSON binding sources ===");
await Probe("[FromQuery]", a => a.MapGet("/x", ([FromQuery] UnionIntString v) => v), h => h.GetAsync("/x?v=42"));
await Probe("route {v}", a => a.MapGet("/x/{v}", (UnionIntString v) => v), h => h.GetAsync("/x/42"));
await Probe("[FromHeader]", a => a.MapGet("/x", ([FromHeader(Name = "v")] UnionIntString v) => v),
    h => { var m = new HttpRequestMessage(HttpMethod.Get, "/x"); m.Headers.Add("v", "42"); return h.SendAsync(m); });
await Probe("GET, inferred", a => a.MapGet("/x", (UnionIntString v) => v), h => h.GetAsync("/x"));
await Probe("[FromForm]", a => a.MapPost("/x", ([FromForm] UnionIntString v) => v).DisableAntiforgery(),
    h => h.PostAsync("/x", new FormUrlEncodedContent([new("v", "42")])));
await Probe("body \"hello\", NumberHandling=Strict via ConfigureHttpJsonOptions",
    a => a.MapPost("/x", (UnionIntString v) => v), h => PostJson(h, "/x", "\"hello\""),
    s => s.ConfigureHttpJsonOptions(o => o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict));
await Probe("body, unclassifiable union", a => a.MapPost("/x", (Approval p) => p), h => PostJson(h, "/x", "{}"));
await Probe("[AsParameters] w/ union", a => a.MapGet("/x", ([AsParameters] QueryArgs q) => q.V), h => h.GetAsync("/x?v=42"));

static void Try(string ctx, string label, Func<string> f)
{
    string r;
    try { r = f(); } catch (Exception e) { r = $"THROWS {e.GetType().Name}: {e.Message}"; }
    Console.WriteLine($"[{ctx}] {label} => {r}");
}

static string Show(IUnion u) => u.Value is null ? "Value=null" : $"{u.Value.GetType().Name}({u.Value})";

static async Task<(WebApplication, HttpClient)> Start(int port, Action<IServiceCollection> svc, Action<WebApplication> map)
{
    var b = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
    b.Logging.ClearProviders();
    b.WebHost.UseUrls($"http://127.0.0.1:{port}");
    svc(b.Services);
    var app = b.Build();
    map(app);
    await app.StartAsync();
    return (app, new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") });
}

async Task Probe(string label, Action<WebApplication> map, Func<HttpClient, Task<HttpResponseMessage>> send,
    Action<IServiceCollection>? svc = null)
{
    WebApplication? app = null;
    try
    {
        (app, var h) = await Start(port++, svc ?? (_ => { }), map);
        await Report($"minimal {label}", send(h));
    }
    catch (Exception e) { Console.WriteLine($"minimal {label} => STARTUP THROWS {e.GetType().Name}: {e.Message}"); }
    finally { if (app is not null) await app.StopAsync(); }
}

static async Task Report(string label, Task<HttpResponseMessage> t)
{
    try
    {
        var r = await t;
        var body = (await r.Content.ReadAsStringAsync()).ReplaceLineEndings(" ");
        if (body.Length > 420) body = body[..420] + "…";
        Console.WriteLine($"{label} => {(int)r.StatusCode} {body}");
    }
    catch (Exception e) { Console.WriteLine($"{label} => THROWS {e.GetType().Name}: {e.Message}"); }
}

static Task<HttpResponseMessage> PostJson(HttpClient h, string path, string json) =>
    h.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));

public union UnionIntString(int, string);
public union UnionNullableIntString(int?, string);
[JsonNumberHandling(JsonNumberHandling.Strict)]
public union UnionIntStringStrict(int, string);

public record Cat(string Name, string Coat);
public record Dog(string Name, string Breed);

[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union UnionPet(Cat, Dog);
public union UnionPetNoClassifier(Cat, Dog);

public record TwinA(string Name);
public record TwinB(string Name);
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union UnionTwins(TwinA, TwinB);

public record NotRequired;
public record PendingApproval;
public record PartlyApproved(Guid Approver);
public record FullyApproved(Guid Approver1, Guid Approver2);
public record Rejected(Guid Rejector);
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union Approval(NotRequired, PendingApproval, PartlyApproved, FullyApproved, Rejected);

[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record class PaymentEvent(string PaymentId);
public sealed record class PaymentInitiated(string PaymentId) : PaymentEvent(PaymentId);
public sealed record class PaymentAuthorized(string PaymentId, decimal Amount) : PaymentEvent(PaymentId);
public sealed record class PaymentFailed(string PaymentId, string Reason) : PaymentEvent(PaymentId);

public record QueryArgs(UnionIntString V);

public record Small(string Name);
public record Big(string Name, string Extra);
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union SubsetU(Small, Big);
public record BigReq(string Name) { public required string Extra { get; init; } }
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union SubsetReq(Small, BigReq);

[ApiController, Route("mvc")]
public class UnionController : ControllerBase
{
    [HttpPost("int-string")] public UnionIntString Echo(UnionIntString v) => v;
    [HttpPost("pet")] public ActionResult<UnionPet> Pet(UnionPet p) => p;
    [HttpPost("pet-nc")] public UnionPetNoClassifier PetNc(UnionPetNoClassifier p) => p;
    [HttpPost("payment")] public string Payment(PaymentEvent e) => e.GetType().Name;
    [HttpGet("async")] public async Task<UnionIntString> Async() { await Task.Yield(); return "async"; }
    [HttpGet("q")] public string Query([FromQuery] UnionIntString v) => Show(v);
    [HttpGet("r/{v}")] public string Route(UnionIntString v) => Show(v);
    private static string Show(IUnion u) => u.Value is null ? "Value=null" : $"{u.Value.GetType().Name}({u.Value})";
}
