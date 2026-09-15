using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:5570");
builder.Services.AddControllers();
var app = builder.Build();

app.MapGet("/q", ([FromQuery] IntOrString v) => Formatter.Describe(v));
app.MapGet("/h", ([FromHeader(Name = "X-V")] IntOrString v) => Formatter.Describe(v));
app.MapGet("/r/{v}", (IntOrString v) => Formatter.Describe(v));
app.MapPost("/form", ([FromForm] IntOrString v) => Formatter.Describe(v)).DisableAntiforgery();
// control: a NON-union struct with no settable members, bound from a form
app.MapPost("/form-opaque", ([FromForm] Opaque v) => v.Value is null ? "null-valued Opaque" : $"Opaque {v.Value}").DisableAntiforgery();
app.MapPost("/form-plain", ([FromForm] PlainIntOrString v) => v.Value switch { int i => $"int {i}", string s => $"string {s}", null => "null-valued union", _ => "other" }).DisableAntiforgery();
app.MapPost("/form-opaque1", ([FromForm] Opaque1 v) => v.Value is null ? "null-valued Opaque1" : $"Opaque1 {v.Value}").DisableAntiforgery();
app.MapPost("/form-opaque2", ([FromForm] Opaque2 v) => v.Value is null ? "null-valued Opaque2" : $"Opaque2 {v.Value}").DisableAntiforgery();
app.MapControllers();

await app.StartAsync();
var h = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5570") };
var hdr42 = new HttpRequestMessage(HttpMethod.Get, "/h"); hdr42.Headers.Add("X-V", "42");
var hdrAbc = new HttpRequestMessage(HttpMethod.Get, "/h"); hdrAbc.Headers.Add("X-V", "abc");
foreach (var (label, t) in new (string, Func<Task<HttpResponseMessage>>)[]
{
    ("GET  /q?v=42       ", () => h.GetAsync("/q?v=42")),
    ("GET  /q?v=abc      ", () => h.GetAsync("/q?v=abc")),
    ("GET  /q (missing)  ", () => h.GetAsync("/q")),
    ("GET  /h X-V:42     ", () => h.SendAsync(hdr42)),
    ("GET  /h X-V:abc    ", () => h.SendAsync(hdrAbc)),
    ("GET  /r/42         ", () => h.GetAsync("/r/42")),
    ("GET  /r/abc        ", () => h.GetAsync("/r/abc")),
    ("POST /form v=42    ", () => h.PostAsync("/form", new FormUrlEncodedContent([new("v", "42")]))),
    ("POST /form v=abc   ", () => h.PostAsync("/form", new FormUrlEncodedContent([new("v", "abc")]))),
    ("POST /form-opaque  ", () => h.PostAsync("/form-opaque", new FormUrlEncodedContent([new("v", "42")]))),
    ("POST /form-opaque1 ", () => h.PostAsync("/form-opaque1", new FormUrlEncodedContent([new("v", "42")]))),
    ("POST /form-opaque2 ", () => h.PostAsync("/form-opaque2", new FormUrlEncodedContent([new("v", "42")]))),
    ("POST /form-opaque1 value=42", () => h.PostAsync("/form-opaque1", new FormUrlEncodedContent([new("value", "42")]))),
    ("POST /form-opaque2 value=42", () => h.PostAsync("/form-opaque2", new FormUrlEncodedContent([new("value", "42")]))),
    ("POST /form-plain v=42      ", () => h.PostAsync("/form-plain", new FormUrlEncodedContent([new("v", "42")]))),
    ("POST /form-plain value=42  ", () => h.PostAsync("/form-plain", new FormUrlEncodedContent([new("value", "42")]))),
    ("POST /form-plain Value=42  ", () => h.PostAsync("/form-plain", new FormUrlEncodedContent([new("Value", "42")]))),
    ("MVC GET  /mvc/q?v=42       ", () => h.GetAsync("/mvc/q?v=42")),
    ("MVC GET  /mvc/q?v=abc      ", () => h.GetAsync("/mvc/q?v=abc")),
    ("MVC GET  /mvc/h X-V:42     ", () => { var m = new HttpRequestMessage(HttpMethod.Get, "/mvc/h"); m.Headers.Add("X-V", "42"); return h.SendAsync(m); }),
    ("MVC GET  /mvc/r/42         ", () => h.GetAsync("/mvc/r/42")),
    ("MVC POST /mvc/form v=42    ", () => h.PostAsync("/mvc/form", new FormUrlEncodedContent([new("v", "42")]))),
})
{
    try
    {
        var r = await t();
        var body = (await r.Content.ReadAsStringAsync()).ReplaceLineEndings(" ");
        Console.WriteLine($"{label} => {(int)r.StatusCode} {(body.Length > 140 ? body[..140] + "…" : body)}");
    }
    catch (Exception ex) { Console.WriteLine($"{label} => EXCEPTION {ex.GetType().Name}: {ex.Message}"); }
}
await app.StopAsync();

public static class Formatter
{
    public static string Describe(IntOrString v) => v.Value switch
    {
        int i => $"int {i}",
        string s => $"string {s}",
        null => "null-valued union",
        _ => "other",
    };
}

[ApiController]
[Route("mvc")]
public class MvcTestController : ControllerBase
{
    [HttpGet("q")]
    public string Query([FromQuery] IntOrString v) => Formatter.Describe(v);

    [HttpGet("h")]
    public string Header([FromHeader(Name = "X-V")] IntOrString v) => Formatter.Describe(v);

    [HttpGet("r/{v}")]
    public string RouteParam([FromRoute] IntOrString v) => Formatter.Describe(v);

    [HttpPost("form")]
    public string Form([FromForm] IntOrString v) => Formatter.Describe(v);
}

public union IntOrString(int, string) : IParsable<IntOrString>
{
    public static IntOrString Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var r) ? r : throw new FormatException();

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out IntOrString result)
    {
        if (s is null) { result = default; return false; }
        result = int.TryParse(s, System.Globalization.NumberStyles.Integer, provider, out var i) ? i : s;
        return true;
    }
}

public union PlainIntOrString(int, string);

// control 1: one ctor, parameter named like the generated union ctor
public struct Opaque1
{
    public Opaque1(int value) => Value = value;
    public object? Value { get; }
}

// control 2: two single-arg ctors, the exact shape the union lowers to
public struct Opaque2
{
    public Opaque2(int value) => Value = value;
    public Opaque2(string value) => Value = value;
    public object? Value { get; }
}

public struct Opaque
{
    public Opaque(int v) => Value = v;
    public object? Value { get; }
}
