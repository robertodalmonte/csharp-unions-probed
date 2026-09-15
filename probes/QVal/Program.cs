using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;

var b = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
b.Logging.ClearProviders();
b.WebHost.UseUrls("http://127.0.0.1:5480");
b.Services.AddValidation();
b.Services.AddControllers();
var app = b.Build();
app.MapControllers();

app.MapPost("/admin-rec", (AdminRec a) => "ok");
app.MapPost("/admin-cls", (AdminCls a) => "ok");
app.MapPost("/union-rec", (UserRec u) => $"ok {u.Value?.GetType().Name}");
app.MapPost("/union-cls", (UserCls u) =>
{
    var results = new List<ValidationResult>();
    var ok = Validator.TryValidateObject(u.Value!, new ValidationContext(u.Value!), results, validateAllProperties: true);
    return $"ok {u.Value?.GetType().Name}; manual Validator.TryValidateObject={ok} ({string.Join("; ", results.Select(r => r.ErrorMessage))})";
});
app.MapPost("/wrapper", (Wrapper w) => $"ok {w.User.Value?.GetType().Name}");

await app.StartAsync();
var h = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5480") };

const string badAdmin = """{"adminId":"A1","role":"SuperAdminRoleExceeding10"}""";
const string badCust = """{"customerId":"C1","age":12}""";
await Report("CANARY record, param attrs, bad admin", Post("/admin-rec", badAdmin));
await Report("CANARY class, property attrs, bad admin", Post("/admin-cls", badAdmin));
await Report("union of records, bad admin", Post("/union-rec", badAdmin));
await Report("union of records, bad customer", Post("/union-rec", badCust));
await Report("union of classes, bad admin", Post("/union-cls", badAdmin));
await Report("union of classes, bad customer", Post("/union-cls", badCust));
await Report("class with [Required] union property, bad admin inside", Post("/wrapper", $$"""{"user":{{badAdmin}}}"""));
await Report("class with [Required] union property, user missing", Post("/wrapper", "{}"));
await Report("MVC CANARY record body, bad admin", Post("/mvc/admin", badAdmin));
await Report("MVC union body, bad admin", Post("/mvc/union", badAdmin));
await app.StopAsync();

Task<HttpResponseMessage> Post(string path, string json) => h.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));

static async Task Report(string label, Task<HttpResponseMessage> t)
{
    var r = await t;
    var body = (await r.Content.ReadAsStringAsync()).ReplaceLineEndings(" ");
    Console.WriteLine($"{label} => {(int)r.StatusCode} {(body.Length > 260 ? body[..260] + "…" : body)}");
}

public record AdminRec([Required] string AdminId, [MaxLength(10)] string Role);
public record CustomerRec([Required] string CustomerId, [Range(18, 120)] int Age);
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union UserRec(AdminRec, CustomerRec);

public class AdminCls
{
    [Required] public string? AdminId { get; set; }
    [MaxLength(10)] public string? Role { get; set; }
}
public class CustomerCls
{
    [Required] public string? CustomerId { get; set; }
    [Range(18, 120)] public int Age { get; set; }
}
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union UserCls(AdminCls, CustomerCls);

[Microsoft.AspNetCore.Mvc.ApiController, Microsoft.AspNetCore.Mvc.Route("mvc")]
public class ValController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    [Microsoft.AspNetCore.Mvc.HttpPost("admin")] public string Admin(AdminRec a) => "ok";
    [Microsoft.AspNetCore.Mvc.HttpPost("union")] public string Union(UserRec u) => $"ok {u.Value?.GetType().Name}, ModelState.IsValid={ModelState.IsValid}";
}

public class Wrapper
{
    [Required] public UserCls User { get; set; }
}
