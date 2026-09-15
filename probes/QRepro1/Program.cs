using System.Text.Json;
using System.Text.Json.Serialization;

foreach (var (name, o) in new[] { ("Default", JsonSerializerOptions.Default), ("Web", JsonSerializerOptions.Web) })
    foreach (var json in new[] { "42", "\"hello\"", "\"42\"" })
    {
        try { Console.WriteLine($"{name,-7} {json,-8} => {JsonSerializer.Deserialize<IntOrString>(json, o).Value?.GetType().Name}"); }
        catch (Exception e) { Console.WriteLine($"{name,-7} {json,-8} => {e.GetType().Name}: {e.Message}"); }
    }

try { JsonSerializer.Deserialize<IntOrStringStrict>("\"hello\"", JsonSerializerOptions.Web); }
catch (Exception e) { Console.WriteLine($"[JsonNumberHandling(Strict)] on the union => {e.GetType().Name}"); }

public union IntOrString(int, string);

[JsonNumberHandling(JsonNumberHandling.Strict)]
public union IntOrStringStrict(int, string);
