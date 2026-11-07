// A case harness: every case is a complete C# source string, compiled in-process by the SDK's own
// Roslyn at the DEFAULT language version (what net11.0 gives with no LangVersion line). A case that
// compiles is emitted, loaded and its P.Run() invoked with Console.Out captured; a case that does
// not compile prints its diagnostics with the offending source line. The claims under test come from
// a third-party "csharp-15" agent skill (a SKILL.md for coding agents); each case names the claim it checks.
//
//   dotnet run --no-build               all cases
//   dotnet run --no-build -- W          only ids starting with "W" (with), "E" (extension indexers), "L" (labeled jumps)
//   dotnet run --no-build -- E07        one case
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

var filter = args.FirstOrDefault();
var defaultVersion = LanguageVersion.Default.MapSpecifiedToEffectiveVersion();
Console.WriteLine($"Microsoft.CodeAnalysis {typeof(Compilation).Assembly.GetName().Version}; LanguageVersion.Default = {defaultVersion}; runtime {Environment.Version}");
Console.WriteLine();

var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
    .Split(Path.PathSeparator).Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();

IEnumerable<Case> all = [.. Cases.With, .. Cases.Indexers, .. Cases.Labeled, .. Cases.Misc];
foreach (var c in all.Where(c => filter is null || c.Id.StartsWith(filter, StringComparison.Ordinal)))
{
    Console.WriteLine($"=== {c.Id}  {c.Claim}");
    var parse = new CSharpParseOptions(c.Preview ? LanguageVersion.Preview : LanguageVersion.Default);
    if (c.Preview && !c.OriginalRules) parse = parse.WithFeatures([new("updated-memory-safety-rules", "true")]);
    var tree = CSharpSyntaxTree.ParseText(c.Source, parse);
    var comp = CSharpCompilation.Create("P_" + c.Id, [tree], refs,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, allowUnsafe: c.AllowUnsafe));
    var diags = comp.GetDiagnostics().Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning).ToList();
    foreach (var d in diags)
    {
        var line = d.Location.GetLineSpan().StartLinePosition.Line;
        var text = tree.GetText().Lines[line].ToString().Trim();
        Console.WriteLine($"  {(d.Severity == DiagnosticSeverity.Error ? "error" : "warning")} {d.Id}: {d.GetMessage()}   <<  {text}");
    }
    if (diags.Any(d => d.Severity == DiagnosticSeverity.Error)) { Console.WriteLine(); continue; }

    using var ms = new MemoryStream();
    var emit = comp.Emit(ms);
    if (!emit.Success)
    {
        foreach (var d in emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)) Console.WriteLine($"  emit error {d.Id}: {d.GetMessage()}");
        Console.WriteLine();
        continue;
    }
    ms.Position = 0;
    var alc = new AssemblyLoadContext(c.Id, isCollectible: true);
    var asm = alc.LoadFromStream(ms);
    var run = asm.GetType("P")?.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
    if (run is null) { Console.WriteLine("  compiles; no P.Run()"); Console.WriteLine(); alc.Unload(); continue; }
    var original = Console.Out;
    var sw = new StringWriter();
    Console.SetOut(sw);
    string? thrown = null;
    try { run.Invoke(null, null); }
    catch (TargetInvocationException e) { thrown = $"{e.InnerException!.GetType().Name}: {e.InnerException.Message}"; }
    finally { Console.SetOut(original); }
    Console.WriteLine("  compiles; runs:");
    foreach (var l in sw.ToString().ReplaceLineEndings("\n").TrimEnd().Split('\n').Where(l => l.Length > 0)) Console.WriteLine($"    {l}");
    if (thrown is not null) Console.WriteLine($"    THREW {thrown}");
    Console.WriteLine();
    alc.Unload();
}

// Preview = LangVersion.Preview plus the updated-memory-safety-rules feature flag (unless OriginalRules); AllowUnsafe = AllowUnsafeBlocks
public sealed record Case(string Id, string Claim, string Source, bool Preview = false, bool AllowUnsafe = false, bool OriginalRules = false);

public static partial class Cases
{
    const string Hdr = """
        using System;
        using System.Collections;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Linq;
        using System.Linq.Expressions;
        using System.Runtime.CompilerServices;
        using System.Threading.Tasks;

        """;

    // body goes inside static void Run(); types are top-level declarations; members sit beside Run in class P
    public static string Wrap(string body, string types = "", string members = "") =>
        Hdr + types + "\npublic static class P\n{\n" + members + "\npublic static void Run()\n{\n" + body + "\n}\n}\n";
}
