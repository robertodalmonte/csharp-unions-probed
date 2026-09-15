using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

var port = 5410;
var d = JsonSerializerOptions.Default;
var w = JsonSerializerOptions.Web;

Console.WriteLine("=== A. Structural classifier edges (Default options) ===");
Try("OneEmpty(Empty1, Distinct(Code)) {Code}", () => Show(JsonSerializer.Deserialize<OneEmpty>("""{"Code":"c"}""", d)));
Try("OneEmptyReq(Empty1, DistinctReq{required Code}) {}", () => Show(JsonSerializer.Deserialize<OneEmptyReq>("{}", d)));
Try("OneEmptyReq {Code}", () => Show(JsonSerializer.Deserialize<OneEmptyReq>("""{"Code":"c"}""", d)));
Try("OneEmptyReq {Cod} (typo)", () => Show(JsonSerializer.Deserialize<OneEmptyReq>("""{"Cod":"c"}""", d)));
Try("OneEmptyOptional(Empty1, AllOpt(D = null)) {}", () => Show(JsonSerializer.Deserialize<OneEmptyOptional>("{}", d)));
Try("TwoEmpty(Empty1, Empty2) {}", () => Show(JsonSerializer.Deserialize<TwoEmpty>("{}", d)));
Try("[JsonRequired] subset {Name}", () => Show(JsonSerializer.Deserialize<JReqU>("""{"Name":"n"}""", d)));
Try("[JsonRequired] subset {Name,Extra}", () => Show(JsonSerializer.Deserialize<JReqU>("""{"Name":"n","Extra":"e"}""", d)));
Try("DisallowBoth {Name,Extr} (typo)", () => Show(JsonSerializer.Deserialize<DisallowBoth>("""{"Name":"n","Extr":"e"}""", d)));
Try("DisallowBoth {Name}", () => Show(JsonSerializer.Deserialize<DisallowBoth>("""{"Name":"n"}""", d)));
Try("DisallowBoth {Name,Extra}", () => Show(JsonSerializer.Deserialize<DisallowBoth>("""{"Name":"n","Extra":"e"}""", d)));
Try("polymorphic case {Title}", () => Show(JsonSerializer.Deserialize<UnionPoly>("""{"Title":"t"}""", d)));
Try("JsonTypeClassifierFactory`1 exists?", () =>
    typeof(JsonTypeClassifierFactory).Assembly.GetType("System.Text.Json.Serialization.JsonTypeClassifierFactory`1")?.FullName ?? "NO");
Try("custom classifier \"hello\" (Web)", () => Show(JsonSerializer.Deserialize<IntOrStringCustom>("\"hello\"", w)));
Try("custom classifier \"42\" (Web)", () => Show(JsonSerializer.Deserialize<IntOrStringCustom>("\"42\"", w)));
Try("custom classifier 42 (Web)", () => Show(JsonSerializer.Deserialize<IntOrStringCustom>("42", w)));
Try("source-gen ctx deser 42", () => Show(JsonSerializer.Deserialize("42", UnionContext.Default.UnionIntString)));
Try("source-gen ctx deser \"hello\"", () => Show(JsonSerializer.Deserialize("\"hello\"", UnionContext.Default.UnionIntString)));
Try("source-gen ctx ser \"x\"", () => JsonSerializer.Serialize(new UnionIntString("x"), UnionContext.Default.UnionIntString));

Console.WriteLine();
Console.WriteLine("=== B. SignalR payload options ===");
{
    var b = WebApplication.CreateBuilder();
    b.Logging.ClearProviders();
    b.Services.AddSignalR();
    await using var app = b.Build();
    var o = app.Services.GetRequiredService<IOptions<JsonHubProtocolOptions>>().Value.PayloadSerializerOptions;
    Console.WriteLine($"PayloadSerializerOptions: NumberHandling={o.NumberHandling}, CaseInsensitive={o.PropertyNameCaseInsensitive}, Naming={o.PropertyNamingPolicy?.GetType().Name ?? "null"}");
    Try("SignalR options deser \"hello\"", () => Show(JsonSerializer.Deserialize<UnionIntString>("\"hello\"", o)));
}

Console.WriteLine();
Console.WriteLine("=== C. OpenAPI with ConfigureHttpJsonOptions Strict ===");
{
    var (app, h) = await Start(port++, "Development",
        s => { s.AddOpenApi(); s.ConfigureHttpJsonOptions(o => o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict); },
        a => { a.MapPost("/x", (UnionIntString v) => v); a.MapOpenApi(); });
    var doc = JsonNode.Parse(await h.GetStringAsync("/openapi/v1.json"))!;
    Console.WriteLine("UnionIntString schema => " + doc["components"]!["schemas"]!["UnionIntString"]!.ToJsonString());
    await Report("minimal POST \"hello\"", PostJson(h, "/x", "\"hello\""));
    await app.StopAsync();
}

Console.WriteLine();
Console.WriteLine("=== D. http options Strict only; MVC options default ===");
{
    var (app, h) = await Start(port++, "Development",
        s => { s.AddControllers(); s.ConfigureHttpJsonOptions(o => o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict); },
        a => { a.MapPost("/x", (UnionIntString v) => v); a.MapGet("/hello", () => Results.Json("hello")); a.MapControllers(); });
    await Report("minimal POST \"hello\"", PostJson(h, "/x", "\"hello\""));
    await Report("MVC POST \"hello\"", PostJson(h, "/mvc/echo", "\"hello\""));
    await Report("MVC [FromForm] v=42", h.PostAsync("/mvc/form", new FormUrlEncodedContent([new("v", "42")])));
    await Report("MVC [FromHeader] v: 42", Send(h, HttpMethod.Get, "/mvc/header", ("v", "42")));
    try { Console.WriteLine("GetFromJsonAsync<UnionIntString> \"hello\" => " + Show(await h.GetFromJsonAsync<UnionIntString>("/hello"))); }
    catch (Exception e) { Console.WriteLine($"GetFromJsonAsync<UnionIntString> \"hello\" => THROWS {e.GetType().Name}: {e.Message}"); }
    await app.StopAsync();
}

Console.WriteLine();
Console.WriteLine("=== E. both options Strict + AllowOutOfOrderMetadataProperties ===");
{
    var (app, h) = await Start(port++, "Development",
        s =>
        {
            s.AddControllers().AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
                o.JsonSerializerOptions.AllowOutOfOrderMetadataProperties = true;
            });
            s.ConfigureHttpJsonOptions(o =>
            {
                o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
                o.SerializerOptions.AllowOutOfOrderMetadataProperties = true;
            });
        },
        a => { a.MapPost("/payment", (PaymentEvent e) => e.GetType().Name); a.MapControllers(); });
    await Report("MVC POST \"hello\"", PostJson(h, "/mvc/echo", "\"hello\""));
    await Report("minimal $type not first", PostJson(h, "/payment", """{"paymentId":"p1","$type":"PaymentAuthorized","amount":9.5}"""));
    await Report("MVC $type not first", PostJson(h, "/mvc/payment", """{"paymentId":"p1","$type":"PaymentAuthorized","amount":9.5}"""));
    await Report("minimal no $type", PostJson(h, "/payment", """{"paymentId":"p1","amount":9.5}"""));
    await app.StopAsync();
}

Console.WriteLine();
Console.WriteLine("=== F. Production environment, closed hierarchy without $type ===");
{
    var (app, h) = await Start(port++, "Production", s => s.AddControllers(),
        a => { a.MapPost("/payment", (PaymentEvent e) => e.GetType().Name); a.MapControllers(); });
    await Report("Production minimal no $type", PostJson(h, "/payment", """{"paymentId":"p1","amount":9.5}"""));
    await Report("Production MVC no $type", PostJson(h, "/mvc/payment", """{"paymentId":"p1","amount":9.5}"""));
    await app.StopAsync();
}

Console.WriteLine();
Console.WriteLine("=== G. unclassifiable union: what makes StartAsync throw ===");
await StartupProbe("bare", _ => { });
await StartupProbe("AddAuthorization()", s => s.AddAuthorization());
await StartupProbe("AddOpenApi()", s => s.AddOpenApi());
await StartupProbe("AddControllers()", s => s.AddControllers());

async Task StartupProbe(string label, Action<IServiceCollection> svc)
{
    WebApplication? app = null;
    try
    {
        (app, var h) = await Start(port++, "Development", svc, a => a.MapPost("/x", (TwoEmpty p) => p));
        Console.Write($"{label}: startup OK; ");
        await Report("first request", PostJson(h, "/x", "{}"));
    }
    catch (Exception e) { Console.WriteLine($"{label}: STARTUP THROWS {e.GetType().Name}"); }
    finally { if (app is not null) try { await app.StopAsync(); } catch { } }
}

static void Try(string label, Func<string> f)
{
    string r;
    try { r = f(); } catch (Exception e) { r = $"THROWS {e.GetType().Name}: {e.Message}"; }
    Console.WriteLine($"{label} => {r}");
}

static string Show(IUnion u) => u.Value is null ? "Value=null" : $"{u.Value.GetType().Name}({u.Value})";

static async Task<(WebApplication, HttpClient)> Start(int port, string env, Action<IServiceCollection> svc, Action<WebApplication> map)
{
    var b = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = env, Args = [] });
    b.Logging.ClearProviders();
    b.WebHost.UseUrls($"http://127.0.0.1:{port}");
    svc(b.Services);
    var app = b.Build();
    map(app);
    await app.StartAsync();
    return (app, new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") });
}

static async Task Report(string label, Task<HttpResponseMessage> t)
{
    try
    {
        var r = await t;
        var body = (await r.Content.ReadAsStringAsync()).ReplaceLineEndings(" ");
        if (body.Length > 300) body = body[..300] + "…";
        Console.WriteLine($"{label} => {(int)r.StatusCode} {body}");
    }
    catch (Exception e) { Console.WriteLine($"{label} => THROWS {e.GetType().Name}: {e.Message}"); }
}

static Task<HttpResponseMessage> PostJson(HttpClient h, string path, string json) =>
    h.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));

static Task<HttpResponseMessage> Send(HttpClient h, HttpMethod m, string path, (string, string) header)
{
    var req = new HttpRequestMessage(m, path);
    req.Headers.Add(header.Item1, header.Item2);
    return h.SendAsync(req);
}

public union UnionIntString(int, string);

public record Empty1;
public record Empty2;
public record Distinct(string Code);
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union OneEmpty(Empty1, Distinct);
public record DistinctReq { public required string Code { get; init; } }
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union OneEmptyReq(Empty1, DistinctReq);
public record AllOpt(string? D = null);
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union OneEmptyOptional(Empty1, AllOpt);
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union TwoEmpty(Empty1, Empty2);

public record JReqA(string Name);
public record JReqB(string Name, [property: JsonRequired] string Extra);
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union JReqU(JReqA, JReqB);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record DisA(string Name);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record DisB(string Name) { public required string Extra { get; init; } }
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union DisallowBoth(DisA, DisB);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PolySub), "sub")]
public abstract record PolyBase;
public record PolySub(string Name) : PolyBase;
public record OtherCase(string Title);
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union UnionPoly(PolyBase, OtherCase);

public class StringFirstClassifier : JsonTypeClassifierFactory
{
    public override bool CanClassify(JsonTypeClassifierContext context) => true;

    public override JsonTypeClassifier CreateJsonClassifier(JsonTypeClassifierContext context, JsonSerializerOptions options) =>
        static (ref Utf8JsonReader r) => r.TokenType switch
        {
            JsonTokenType.String => typeof(string),
            JsonTokenType.Number => typeof(int),
            _ => null,
        };
}
[JsonUnion(TypeClassifier = typeof(StringFirstClassifier))]
public union IntOrStringCustom(int, string);

[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record class PaymentEvent(string PaymentId);
public sealed record class PaymentInitiated(string PaymentId) : PaymentEvent(PaymentId);
public sealed record class PaymentAuthorized(string PaymentId, decimal Amount) : PaymentEvent(PaymentId);
public sealed record class PaymentFailed(string PaymentId, string Reason) : PaymentEvent(PaymentId);

[JsonSerializable(typeof(UnionIntString))]
public partial class UnionContext : JsonSerializerContext { }

[ApiController, Route("mvc")]
public class UnionController : ControllerBase
{
    [HttpPost("echo")] public UnionIntString Echo(UnionIntString v) => v;
    [HttpPost("form")] public string Form([FromForm] UnionIntString v) => Show(v);
    [HttpGet("header")] public string Header([FromHeader(Name = "v")] UnionIntString v) => Show(v);
    [HttpPost("payment")] public string Payment(PaymentEvent e) => e.GetType().Name;
    private static string Show(IUnion u) => u.Value is null ? "Value=null" : $"{u.Value.GetType().Name}({u.Value})";
}
