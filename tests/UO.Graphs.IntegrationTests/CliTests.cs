using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "uographs-cli-" + Guid.NewGuid().ToString("N"));
    private static readonly string Root = FindRoot();
    private static readonly string BuildConfiguration = typeof(CliTests).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyConfigurationAttribute), false)
        .Cast<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
    private static string CliAssembly => Path.Combine(Root, "src/UO.Graphs.Cli/bin", BuildConfiguration, "net10.0/uographs.dll");
    private string Database => Path.Combine(directory, "graph.db");

    [Fact]
    public async Task RealSolutionIndexPersistsTypeAndMethodAcrossProcesses()
    {
        var indexed = await Run("index", Path.Combine(Root, "samples/HelloGraph/HelloGraph.slnx"), "--trust-project", "--db", Database);
        Assert.Equal(0, indexed.Code);
        Assert.Contains("Reading symbols", indexed.Error);
        using var indexJson = JsonDocument.Parse(indexed.Output);
        Assert.Equal(15, indexJson.RootElement.GetProperty("entityCount").GetInt32());
        Assert.Equal(UO.Graphs.Core.ProductVersion.Current, indexJson.RootElement.GetProperty("analysis").GetProperty("toolVersion").GetString());
        var analysisId = indexJson.RootElement.GetProperty("analysis").GetProperty("id").GetString();
        var projectPath = Path.Combine(Root, "samples/HelloGraph/HelloGraph.Domain/HelloGraph.Domain.csproj");
        var projectId = Hash(Canonical("project", "HelloGraph.Domain/HelloGraph.Domain.csproj", "net10.0", "Debug"));
        foreach (var (search, name, docId, qualified) in new[]
        {
            ("Greeter", "Greeter", "T:HelloGraph.Domain.Greeter", "HelloGraph.Domain.Greeter"),
            ("Greet", "Greet", "M:HelloGraph.Domain.Greeter.Greet(System.String)", "HelloGraph.Domain.Greeter.Greet(string)")
        })
        {
            var found = await Run("query", "find", search, "--format", "json", "--db", Database);
            Assert.Equal(0, found.Code);
            Assert.Empty(found.Error);
            using var json = JsonDocument.Parse(found.Output);
            Assert.Equal(6, json.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(analysisId, json.RootElement.GetProperty("analysisContext").GetProperty("id").GetString());
            var entity = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("entity")), e => e.GetProperty("documentationId").GetString() == docId);
            Assert.Equal(name, entity.GetProperty("name").GetString());
            Assert.Equal(qualified, entity.GetProperty("qualifiedName").GetString());
            Assert.Equal(Hash(Canonical("symbol", projectId, docId)), entity.GetProperty("id").GetString());
            Assert.Equal(projectId, entity.GetProperty("projectId").GetString());
            Assert.Equal("exact", entity.GetProperty("confidence").GetString());
            var evidence = Assert.Single(entity.GetProperty("sources").EnumerateArray());
            Assert.Equal(Hash(await File.ReadAllTextAsync(evidence.GetProperty("file").GetString()!)), evidence.GetProperty("sha256").GetString());
            Assert.True(evidence.GetProperty("startLine").GetInt32() > 0);
        }
        var status = await Run("status", "--db", Database);
        Assert.Equal(0, status.Code);
        using var statusJson = JsonDocument.Parse(status.Output);
        Assert.Equal(analysisId, statusJson.RootElement.GetProperty("analysis").GetProperty("id").GetString());
        Assert.Equal(2, statusJson.RootElement.GetProperty("analysis").GetProperty("projects").GetArrayLength());
        var missing = Path.Combine(directory, "Missing.csproj");
        await File.WriteAllTextAsync(missing, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var failed = await Run("index", missing, "--trust-project", "--db", Database);
        Assert.Equal(4, failed.Code);
        Assert.Empty(failed.Output);
        Assert.False(File.Exists(Path.Combine(directory, "obj/project.assets.json")));
        var retained = await Run("status", "--db", Database);
        Assert.Equal(0, retained.Code);
        using var retainedJson = JsonDocument.Parse(retained.Output);
        Assert.Equal(analysisId, retainedJson.RootElement.GetProperty("analysis").GetProperty("id").GetString());
    }

    [Fact]
    public async Task ProjectInputFollowsReferencesAndDefaultDatabaseUsesWorkingDirectory()
    {
        Directory.CreateDirectory(directory);
        var result = await RunIn(directory, "index", Path.Combine(Root, "samples/HelloGraph/HelloGraph.App/HelloGraph.App.csproj"), "--trust-project");
        Assert.Equal(0, result.Code);
        using var index = JsonDocument.Parse(result.Output);
        Assert.Equal(14, index.RootElement.GetProperty("entityCount").GetInt32());
        Assert.True(File.Exists(Path.Combine(directory, ".uographs/graph.db")));
        var status = await RunIn(directory, "status");
        Assert.Equal(0, status.Code);
        using var json = JsonDocument.Parse(status.Output);
        Assert.Equal(14, json.RootElement.GetProperty("entityCount").GetInt32());
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("query", "find")]
    [InlineData("query", "find", "x", "--format", "xml")]
    [InlineData("status", "--trust-project")]
    [InlineData("status", "--db")]
    [InlineData("status", "--db", "x", "--db", "y")]
    public async Task BadArgumentsProduceOnlyStderr(params string[] args)
    {
        var result = await Run(args);
        Assert.Equal(2, result.Code);
        Assert.Empty(result.Output);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public async Task HelpAndDoctorRunWithoutLoadingTarget()
    {
        var help = await Run("--help");
        Assert.Equal(0, help.Code);
        Assert.StartsWith($"UO.Graphs {UO.Graphs.Core.ProductVersion.Current} —", help.Output);
        Assert.Contains("uographs index", help.Output);
        var doctor = await Run("doctor");
        Assert.Equal(0, doctor.Code);
        using var json = JsonDocument.Parse(doctor.Output);
        Assert.False(json.RootElement.GetProperty("automaticRestore").GetBoolean());
        Assert.False(json.RootElement.GetProperty("targetProjectChecked").GetBoolean());
        Assert.Contains("restore", json.RootElement.GetProperty("preparation").GetString());
    }

    [Fact]
    public async Task TrustIsRequiredBeforeMalformedProjectIsEvaluated()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "Bad.csproj");
        await File.WriteAllTextAsync(path, "not XML");
        var result = await Run("index", path, "--db", Database);
        Assert.Equal(3, result.Code);
        Assert.Empty(result.Output);
        Assert.DoesNotContain("Checking", result.Error);
        Assert.False(File.Exists(Database));
    }

    [Fact]
    public async Task MissingAssetsFailsWithoutRestoringOrCreatingDatabase()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "Missing.csproj");
        await File.WriteAllTextAsync(path, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var result = await Run("index", path, "--trust-project", "--db", Database);
        Assert.Equal(4, result.Code);
        Assert.Contains("assets missing", result.Error);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(Path.Combine(directory, "obj/project.assets.json")));
        Assert.False(File.Exists(Database));
    }

    [Fact]
    public async Task MissingTargetSdkReportsPreparationError()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "Missing.csproj");
        await File.WriteAllTextAsync(path, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"99.0.100\",\"rollForward\":\"disable\"}}");
        var result = await Run("index", path, "--trust-project", "--db", Database);
        Assert.Equal(4, result.Code);
        Assert.Contains("SDK", result.Error);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(Database));
    }

    private static Task<(int Code, string Output, string Error)> Run(params string[] arguments) => RunIn(Root, arguments);

    private static async Task<(int Code, string Output, string Error)> RunIn(string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = workingDirectory, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false };
        // CI already built these binaries. Avoid an extra `dotnet run`/MSBuild launcher.
        foreach (var arg in new[] { CliAssembly }.Concat(arguments)) start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        // A single SDK preparation process may legitimately take up to 120 seconds.
        // Match the existing packaged-index smoke deadline; read commands stay bounded at 90.
        var budget = TimeSpan.FromSeconds(arguments.FirstOrDefault() == "index" ? 180 : 90);
        return await RunProcessAsync(start, budget);
    }

    private static async Task<(int Code, string Output, string Error)> RunProcessAsync(ProcessStartInfo start, TimeSpan budget)
    {
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start CLI test process.");
        var watch = Stopwatch.StartNew();
        var output = new StringBuilder();
        var error = new StringBuilder();
        using var deadline = new CancellationTokenSource(budget);
        var stdout = CaptureAsync(process.StandardOutput, output, deadline.Token);
        var stderr = CaptureAsync(process.StandardError, error, deadline.Token);
        var timedOut = false;
        var cleanup = "process exited";
        try
        {
            // Also bound pipe draining: a child can keep a pipe open after the CLI exits.
            await Task.WhenAll(process.WaitForExitAsync(deadline.Token), stdout, stderr);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested) { timedOut = true; }
        finally
        {
            deadline.Cancel();
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    cleanup = "process tree kill requested";
                }
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (InvalidOperationException) { cleanup = "process exited during cleanup"; }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or TimeoutException)
            { cleanup = "cleanup failed: " + ex.Message; }
        }
        if (timedOut)
            throw new TimeoutException(FormattableString.Invariant($"CLI test process exceeded {budget.TotalSeconds:F1}s (elapsed {watch.Elapsed.TotalSeconds:F1}s; PID {process.Id}; {cleanup}).") +
                $"\nWorking directory: {start.WorkingDirectory}\nArgv: {JsonSerializer.Serialize(new[] { start.FileName }.Concat(start.ArgumentList))}" +
                $"\nSTDOUT tail:\n{Tail(output)}\nSTDERR tail:\n{Tail(error)}");
        return (process.ExitCode, output.ToString(), error.ToString());

        static async Task CaptureAsync(StreamReader reader, StringBuilder capture, CancellationToken token)
        {
            var buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
                capture.Append(buffer, 0, count);
        }
        static string Tail(StringBuilder capture)
        {
            const int maxChars = 16000;
            var start = Math.Max(0, capture.Length - maxChars);
            return (start > 0 ? "[earlier output omitted]\n" : "") + capture.ToString(start, capture.Length - start);
        }
    }

    private static string Canonical(params string[] parts) => "uographs:2:" + string.Concat(parts.Select(p => $"{Encoding.UTF8.GetByteCount(p)}:{p}"));

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "UO.Graphs.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
