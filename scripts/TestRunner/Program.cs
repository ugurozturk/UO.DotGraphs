using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Xunit;

// Socket-free fallback for this repository's parameterless Fact / InlineData tests.
// Does not implement xUnit discovery/fixtures/parallelization; reject unknown data/lifecycle rather than skip it.
var filter = args.FirstOrDefault() ?? "";
var results = new List<object>();
var failures = 0;
foreach (var assembly in new[] { typeof(UO.Graphs.UnitTests.Phase9Tests).Assembly, typeof(UO.Graphs.IntegrationTests.CliTests).Assembly })
foreach (var type in assembly.GetTypes().Where(t => t.IsPublic))
foreach (var method in type.GetMethods().Where(m => m.GetCustomAttribute<FactAttribute>() is not null && (type.FullName + "." + m.Name).Contains(filter, StringComparison.Ordinal)))
{
    if (method.GetCustomAttribute<FactAttribute>()!.Skip is not null || type.GetInterfaces().Any(i => i.Name.StartsWith("IClassFixture", StringComparison.Ordinal) || i == typeof(IAsyncLifetime)) ||
        method.GetCustomAttributes().Any(a => a is Xunit.Sdk.DataAttribute && a is not InlineDataAttribute))
        throw new InvalidOperationException("Unsupported test lifecycle/data/skip: " + method.Name);
    var rows = method.GetCustomAttributes<InlineDataAttribute>().Select(a => a.GetData(method).Single()).ToArray();
    if (rows.Length == 0) rows = [[]];
    foreach (var row in rows)
    {
        var watch = Stopwatch.StartNew();
        var name = type.FullName + "." + method.Name + JsonSerializer.Serialize(row);
        object? instance = null;
        string? error = null;
        try
        {
            instance = Activator.CreateInstance(type)!;
            object?[] parameters = method.GetParameters().LastOrDefault()?.GetCustomAttribute<ParamArrayAttribute>() is not null ? [row.Cast<string>().ToArray()] : row;
            if (method.Invoke(instance, parameters) is Task task) await task;
        }
        catch (Exception exception) { error = (exception is TargetInvocationException { InnerException: { } inner } ? inner : exception).ToString(); failures++; }
        finally { (instance as IDisposable)?.Dispose(); }
        Console.WriteLine((error is null ? "PASS " : "FAIL ") + name + " " + watch.ElapsedMilliseconds + "ms");
        if (error is not null) Console.WriteLine(error);
        results.Add(new { name, passed = error is null, error, elapsedMs = watch.ElapsedMilliseconds });
    }
}
if (results.Count == 0) throw new InvalidOperationException("No tests matched.");
if (args.Length > 1) await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { runner = "socket-free-reflection", total = results.Count, failures, results }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{results.Count - failures}/{results.Count} passed; {failures} failed");
return failures == 0 ? 0 : 1;
