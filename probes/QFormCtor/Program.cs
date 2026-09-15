// Union-free repro: Minimal API [FromForm] hands the handler default(T) / null, with 200, when the
// parameter type has more than one public constructor. Companion to QParsable (which showed the
// same on Opaque2 and on a C# 15 union); this probe has no union in it at all.
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
// Logging is ON, at Debug, for the whole form-mapping namespace, to show that nothing surfaces.
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.Endpoints.FormMapping", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Http", LogLevel.Debug);
builder.WebHost.UseUrls("http://127.0.0.1:5580");
builder.Services.AddControllers();
var app = builder.Build();

// one public constructor: the supported shape
app.MapPost("/s1", ([FromForm] OneCtorStruct v) => $"OneCtorStruct Value={v.Value ?? "null"}").DisableAntiforgery();
app.MapPost("/c1", ([FromForm] OneCtorClass v) => v is null ? "OneCtorClass null" : $"OneCtorClass Value={v.Value ?? "null"}").DisableAntiforgery();
// two public constructors: the unsupported shape
app.MapPost("/s2", ([FromForm] TwoCtorStruct v) => $"TwoCtorStruct Value={v.Value ?? "null"}").DisableAntiforgery();
app.MapPost("/c2", ([FromForm] TwoCtorClass v) => v is null ? "TwoCtorClass null" : $"TwoCtorClass Value={v.Value ?? "null"}").DisableAntiforgery();
// the #51379 shape: settable properties, primary ctor plus a parameterless one
app.MapPost("/post", ([FromForm] BlogPostInput v) => v is null ? "BlogPostInput null" : $"BlogPostInput Title={v.Title ?? "null"} Body={v.Body}").DisableAntiforgery();
// control: the same two-ctor struct as a QUERY parameter. Build with -p:DefineConstants=QUERY_CONTROL
// to see the contrast: the build fails with ASP0020 ("should define a bool TryParse ... or implement
// IParsable<TwoCtorStruct>"). Nothing equivalent exists for [FromForm].
#if QUERY_CONTROL
app.MapGet("/q2", ([FromQuery] TwoCtorStruct v) => $"TwoCtorStruct Value={v.Value ?? "null"}");
#endif
app.MapControllers();

try { await app.StartAsync(); }
catch (Exception ex) { Console.WriteLine($"STARTUP FAILED: {ex.GetType().Name}: {ex.Message}"); return; }

var h = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5580") };
foreach (var (label, path, key) in new (string, string, string)[]
{
    ("s1 value=42     ", "/s1", "value"),
    ("s1 v=42         ", "/s1", "v"),
    ("c1 value=42     ", "/c1", "value"),
    ("s2 value=42     ", "/s2", "value"),
    ("s2 Value=42     ", "/s2", "Value"),
    ("s2 v=42         ", "/s2", "v"),
    ("c2 value=42     ", "/c2", "value"),
    ("post Title=x    ", "/post", "Title"),
    ("post Body=x     ", "/post", "Body"),
    ("mvc/s2 value=42 ", "/mvc/s2", "value"),
    ("mvc/c2 value=42 ", "/mvc/c2", "value"),
    ("mvc/post Body=x ", "/mvc/post", "Body"),
})
{
    try
    {
        var r = await h.PostAsync(path, new FormUrlEncodedContent([new(key, key is "Title" or "Body" ? "x" : "42")]));
        var body = (await r.Content.ReadAsStringAsync()).ReplaceLineEndings(" ");
        Console.WriteLine($"POST {label} => {(int)r.StatusCode} {(body.Length > 160 ? body[..160] + "…" : body)}");
    }
    catch (Exception ex) { Console.WriteLine($"POST {label} => EXCEPTION {ex.GetType().Name}: {ex.Message}"); }
}
await app.StopAsync();

public struct OneCtorStruct
{
    public OneCtorStruct(int value) => Value = value.ToString();
    public string? Value { get; }
}

public class OneCtorClass
{
    public OneCtorClass(int value) => Value = value.ToString();
    public string? Value { get; }
}

public struct TwoCtorStruct
{
    public TwoCtorStruct(int value) => Value = value.ToString();
    public TwoCtorStruct(string value) => Value = value;
    public string? Value { get; }
}

public class TwoCtorClass
{
    public TwoCtorClass(int value) => Value = value.ToString();
    public TwoCtorClass(string value) => Value = value;
    public string? Value { get; }
}

// verbatim from dotnet/aspnetcore#51379
public class BlogPostInput(string? title, string body)
{
    public string? Title { get; set; } = title;
    public string Body { get; set; } = body;
    public BlogPostInput() : this(null, string.Empty) { }
}

[ApiController]
[Route("mvc")]
public class MvcController : ControllerBase
{
    [HttpPost("s2")] public string S2([FromForm] TwoCtorStruct v) => $"TwoCtorStruct Value={v.Value ?? "null"}";
    [HttpPost("c2")] public string C2([FromForm] TwoCtorClass v) => v is null ? "TwoCtorClass null" : $"TwoCtorClass Value={v.Value ?? "null"}";
    [HttpPost("post")] public string Post([FromForm] BlogPostInput v) => v is null ? "BlogPostInput null" : $"BlogPostInput Title={v.Title ?? "null"} Body={v.Body}";
}
