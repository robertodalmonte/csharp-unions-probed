// What a union returned from an endpoint looks like on the wire: status, body, OpenAPI. No HTTP semantics travel with it.
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;

var b = WebApplication.CreateBuilder(args);
b.WebHost.UseUrls("http://127.0.0.1:5590");
b.Logging.ClearProviders();
b.Services.AddControllers();
b.Services.AddOpenApi();
await using var app = b.Build();

app.MapOpenApi();
app.MapGet("/min/{kind}", (string kind) => Make(kind));
app.MapGet("/min-async/{kind}", async (string kind) => { await Task.Yield(); return Make(kind); });
app.MapGet("/min-typed/{kind}", Task<Microsoft.AspNetCore.Http.HttpResults.Ok<CreateOrderResult>> (string kind) =>
    Task.FromResult(TypedResults.Ok(Make(kind))));
app.MapControllers();
await app.StartAsync();

using var h = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5590") };
foreach (var path in new[] { "/min/created", "/min/invalid", "/min/missing", "/min/default",
                             "/min-async/invalid", "/min-typed/missing",
                             "/mvc/created", "/mvc/invalid", "/mvc/missing", "/mvc/default", "/mvc-async/invalid", "/mvc-action/invalid" })
{
    var r = await h.GetAsync(path);
    Console.WriteLine($"GET {path,-22} -> {(int)r.StatusCode} {r.Content.Headers.ContentType?.MediaType}  {(await r.Content.ReadAsStringAsync()).ReplaceLineEndings(" ")}");
}

var doc = JsonNode.Parse(await h.GetStringAsync("/openapi/v1.json"))!;
foreach (var p in new[] { "/min/{kind}", "/mvc/{kind}", "/mvc-action/{kind}" })
    Console.WriteLine($"OpenAPI {p} 200 -> {doc["paths"]![p]!["get"]!["responses"]!.ToJsonString()}");
Console.WriteLine("Schema: " + doc["components"]!["schemas"]!["CreateOrderResult"]!.ToJsonString());
await app.StopAsync();

static CreateOrderResult Make(string kind) => kind switch
{
    "created" => new OrderCreated(Guid.Parse("6f1c2b9e-0000-0000-0000-000000000001")),
    "invalid" => new InvalidOrder("Quantity must be greater than zero."),
    "missing" => new ProductNotFound("P404"),
    _ => default,
};

public sealed record OrderCreated(Guid OrderId);
public sealed record InvalidOrder(string Message);
public sealed record ProductNotFound(string ProductCode);
public union CreateOrderResult(OrderCreated, InvalidOrder, ProductNotFound);

[ApiController]
public sealed class OrdersController : ControllerBase
{
    static CreateOrderResult Make(string kind) => kind switch
    {
        "created" => new OrderCreated(Guid.Parse("6f1c2b9e-0000-0000-0000-000000000001")),
        "invalid" => new InvalidOrder("Quantity must be greater than zero."),
        "missing" => new ProductNotFound("P404"),
        _ => default,
    };

    [HttpGet("/mvc/{kind}")] public CreateOrderResult Direct(string kind) => Make(kind);
    [HttpGet("/mvc-async/{kind}")] public async Task<CreateOrderResult> Async(string kind) { await Task.Yield(); return Make(kind); }
    [HttpGet("/mvc-action/{kind}")] public ActionResult<CreateOrderResult> Wrapped(string kind) => Make(kind);
}
