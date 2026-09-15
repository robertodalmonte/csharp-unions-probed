using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;

// --- the draft's repro endpoints, verbatim (minus the CS0029 one, which would stop the RDG build) ---
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:5560");
var app = builder.Build();
app.MapPost("/form", ([FromForm] IntOrString v) => ((IUnion)v).Value is null ? "null-valued union" : "has value")
   .DisableAntiforgery();                               // POST v=42 (form) -> 200 "null-valued union"
#if WITH_QUERY
app.MapGet("/q", ([FromQuery] IntOrString v) => v);     // RDG: ?v=42 -> 200 "42" (string case)
#endif

// --- harness ---
await app.StartAsync();
var h = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5560") };
foreach (var (label, t) in new (string, Task<HttpResponseMessage>)[]
{
    ("POST /form v=42", h.PostAsync("/form", new FormUrlEncodedContent([new("v", "42")]))),
    ("GET  /q?v=42   ", h.GetAsync("/q?v=42")),
})
{
    var r = await t;
    var body = (await r.Content.ReadAsStringAsync()).ReplaceLineEndings(" ");
    Console.WriteLine($"{label} => {(int)r.StatusCode} {(body.Length > 120 ? body[..120] + "…" : body)}");
}
await app.StopAsync();

public union IntOrString(int, string);
