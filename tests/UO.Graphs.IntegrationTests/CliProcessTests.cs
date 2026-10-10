using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests
{
    [Fact]
    public async Task CliProcessDeadlineRetainsProgressAndWaitsForTermination()
    {
        var start = ProcessFixture("wait");
        var culture = CultureInfo.CurrentCulture;
        TimeoutException failure;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            failure = await Assert.ThrowsAsync<TimeoutException>(() => RunProcessAsync(start, TimeSpan.FromSeconds(3)));
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Assert.Contains("exceeded 3.0s", failure.Message);
        Assert.Contains("Argv:", failure.Message);
        Assert.Contains("stdout checkpoint", failure.Message);
        Assert.Contains("Reading symbols: fixture", failure.Message);
        Assert.Contains("process tree kill requested", failure.Message);
        var pid = int.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "process.pid")));
        try
        {
            using var process = Process.GetProcessById(pid);
            Assert.True(process.HasExited);
        }
        catch (ArgumentException) { /* The terminated process was already reaped. */ }
    }

    [Fact]
    public async Task CliProcessCapturePreservesExactStreamsAndNonzeroExit()
    {
        var result = await RunProcessAsync(ProcessFixture("exit"), TimeSpan.FromSeconds(10));
        Assert.Equal(7, result.Code);
        Assert.Equal("stdout checkpoint\n😀\\\"", result.Output);
        Assert.Equal("Reading symbols: fixture\n", result.Error);
    }

    private ProcessStartInfo ProcessFixture(string mode)
    {
        Directory.CreateDirectory(directory);
        var fixture = Path.Combine(directory, "ProcessFixture.dll");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("ProcessFixture", [CSharpSyntaxTree.ParseText("""
            using System;
            using System.IO;
            using System.Threading;
            public static class Entry
            {
                public static int Main(string[] args)
                {
                    File.WriteAllText(args[1], Environment.ProcessId.ToString());
                    Console.Out.Write("stdout checkpoint\n😀\\\"");
                    Console.Error.Write("Reading symbols: fixture\n");
                    if (args[0] == "wait") Thread.Sleep(Timeout.Infinite);
                    return 7;
                }
            }
            """)], references, new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        var emitted = compilation.Emit(fixture);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        File.WriteAllText(Path.ChangeExtension(fixture, ".runtimeconfig.json"), JsonSerializer.Serialize(new
        { runtimeOptions = new { tfm = "net10.0", framework = new { name = "Microsoft.NETCore.App", version = Environment.Version.ToString() } } }));
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(fixture);
        start.ArgumentList.Add(mode);
        start.ArgumentList.Add(Path.Combine(directory, "process.pid"));
        return start;

    }
}
