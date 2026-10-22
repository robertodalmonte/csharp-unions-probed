// Guid / DateTime / string unions: both cases read a JSON String token, so token kind cannot separate them.
using System.Text.Json;
using System.Text.Json.Serialization;

var strictWeb = new JsonSerializerOptions(JsonSerializerOptions.Web) { NumberHandling = JsonNumberHandling.Strict };
var opts = new (string, JsonSerializerOptions)[]
{
    ("Default", JsonSerializerOptions.Default),
    ("Web", JsonSerializerOptions.Web),
    ("Web+Strict", strictWeb),
};
var guid = "\"6f1c2b9e-0000-0000-0000-000000000001\"";
var date = "\"2026-10-22T15:00:00Z\"";

foreach (var (name, o) in opts)
{
    Console.WriteLine($"--- {name}");
    Run<GuidOrString>("GuidOrString  guid-text ", guid, o);
    Run<GuidOrString>("GuidOrString  \"hello\"   ", "\"hello\"", o);
    Run<DateOrString>("DateOrString  iso-date  ", date, o);
    Run<DateOrString>("DateOrString  \"hello\"   ", "\"hello\"", o);
    Run<GuidOrInt>("GuidOrInt     guid-text ", guid, o);
    Run<GuidOrInt>("GuidOrInt     42        ", "42", o);
    Run<GuidOrInt>("GuidOrInt     \"42\"      ", "\"42\"", o);
    Run<ClassifiedGuidOrString>("Classified    guid-text ", guid, o);
    Run<ClassifiedGuidOrString>("Classified    \"hello\"   ", "\"hello\"", o);
    Run<ClassifiedGuidOrString>("Classified    \"\"        ", "\"\"", o);
    Run<ClassifiedGuidOrString>("Classified    42        ", "42", o);
}

Console.WriteLine("--- serialization (Default)");
Console.WriteLine(JsonSerializer.Serialize(new GuidOrString(Guid.Parse("6f1c2b9e-0000-0000-0000-000000000001"))));
Console.WriteLine(JsonSerializer.Serialize(new GuidOrString("hello")));

static void Run<T>(string label, string json, JsonSerializerOptions o)
{
    try
    {
        var u = (System.Runtime.CompilerServices.IUnion)JsonSerializer.Deserialize<T>(json, o)!;
        Console.WriteLine($"{label} -> {u.Value?.GetType().Name ?? "null"} {u.Value}");
    }
    catch (Exception e) { Console.WriteLine($"{label} -> {e.GetType().Name}: {e.Message.Split('.')[0]}"); }
}

public union GuidOrString(Guid, string);
public union DateOrString(DateTime, string);
public union GuidOrInt(Guid, int);

public class GuidFirstClassifier : JsonTypeClassifierFactory
{
    public override bool CanClassify(JsonTypeClassifierContext context) => true;
    public override JsonTypeClassifier CreateJsonClassifier(JsonTypeClassifierContext context, JsonSerializerOptions options) =>
        static (ref Utf8JsonReader r) => r.TokenType switch
        {
            JsonTokenType.String => r.TryGetGuid(out _) ? typeof(Guid) : typeof(string),
            _ => null,
        };
}

[JsonUnion(TypeClassifier = typeof(GuidFirstClassifier))]
public union ClassifiedGuidOrString(Guid, string);
