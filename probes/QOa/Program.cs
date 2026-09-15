using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

var port = 5491;
await Scenario("bare, bad minimal endpoint, /ok first", _ => { }, a => { a.MapPost("/x", (TwoEmpty p) => p); a.MapGet("/ok", () => "ok"); },
    [("GET", "/ok"), ("GET", "/ok")]);
await Scenario("bare, no bad endpoint (control)", _ => { }, a => a.MapGet("/ok", () => "ok"), [("GET", "/ok")]);
await Scenario("bare, NON-union bad endpoint (GET with inferred body)", _ => { }, a => { a.MapGet("/bad", (Todo t) => t); a.MapGet("/ok", () => "ok"); },
    [("GET", "/ok")]);
await Scenario("bad endpoint inside MapGroup(\"/sub\")", _ => { }, a => { a.MapGroup("/sub").MapPost("/x", (TwoEmpty p) => p); a.MapGet("/ok", () => "ok"); },
    [("GET", "/ok")]);
await Scenario("AddControllers, bad MVC action only", s => s.AddControllers(), a => { a.MapControllers(); a.MapGet("/ok", () => "ok"); },
    [("GET", "/ok"), ("GET", "/mvc/ok"), ("POST", "/mvc/bad")]);
await Scenario("AddMvcCore (no authorization), bad minimal endpoint + MVC routes", s => s.AddMvcCore(), a => { a.MapControllers(); a.MapPost("/x", (TwoEmpty p) => p); a.MapGet("/ok", () => "ok"); },
    [("GET", "/mvc/ok"), ("GET", "/ok")]);
await Scenario("AddControllers, bad minimal endpoint + MVC routes", s => s.AddControllers(), a => { a.MapControllers(); a.MapPost("/x", (TwoEmpty p) => p); a.MapGet("/ok", () => "ok"); },
    [("GET", "/mvc/ok")]);
await Scenario("read-time ambiguity only (no classifier)", _ => { }, a => { a.MapPost("/amb", (UnionPetNoClassifier p) => p); a.MapGet("/ok", () => "ok"); },
    [("GET", "/ok"), ("POST", "/amb")]);

Try("nested union case {Name}", () => JsonSerializer.Deserialize<OuterUnion>("""{"Name":"Tom"}""").Value?.GetType().Name ?? "null");

async Task Scenario(string label, Action<IServiceCollection> svc, Action<WebApplication> map, (string Method, string Path)[] requests)
{
    var b = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
    b.Logging.ClearProviders();
    b.WebHost.UseUrls($"http://127.0.0.1:{port}");
    svc(b.Services);
    var app = b.Build();
    map(app);
    try { await app.StartAsync(); }
    catch (Exception e)
    {
        Console.WriteLine($"[{label}] STARTUP THROWS {e.GetType().Name}: {e.Message[..Math.Min(90, e.Message.Length)]}…");
        Console.WriteLine($"    top frames: {string.Join(" <- ", (e.StackTrace ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("at Microsoft.AspNetCore.Routing")).Take(3))}");
        port++;
        return;
    }
    var h = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port++}") };
    foreach (var (method, path) in requests)
    {
        var r = method == "GET" ? await h.GetAsync(path) : await h.PostAsync(path, new StringContent("{\"Name\":\"a\"}", Encoding.UTF8, "application/json"));
        var body = (await r.Content.ReadAsStringAsync()).ReplaceLineEndings(" ");
        Console.WriteLine($"[{label}] {method} {path} => {(int)r.StatusCode} {(body.Length > 130 ? body[..130] + "…" : body)}");
    }
    await app.StopAsync();
}

static void Try(string label, Func<string> f)
{
    string r;
    try { r = f(); } catch (Exception e) { r = $"THROWS {e.GetType().Name}: {e.Message}"; }
    Console.WriteLine($"{label} => {r}");
}

public record Todo(string Title);
public record Empty1;
public record Empty2;
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union TwoEmpty(Empty1, Empty2);

public record Cat(string Name);
public record Dog(string Name);
public union UnionPetNoClassifier(Cat, Dog);

public record LeafA(string Name);
public record LeafB(string Code);
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union InnerUnion(LeafA, LeafB);
public record Other(string Title);
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union OuterUnion(InnerUnion, Other);

[ApiController, Route("mvc")]
public class GoodController : ControllerBase
{
    [HttpGet("ok")] public string Get() => "mvc ok";
}

[ApiController, Route("mvc")]
public class BadController : ControllerBase
{
    [HttpPost("bad")] public string Bad([FromBody] TwoEmpty p) => "bad";
}
