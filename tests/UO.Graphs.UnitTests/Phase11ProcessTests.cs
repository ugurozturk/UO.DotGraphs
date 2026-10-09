using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UO.Graphs.Core;
using UO.Graphs.Roslyn;

namespace UO.Graphs.UnitTests;

// These tests use real dotnet parent/child processes, not a mocked TextReader.
public sealed class Phase11ProcessTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "uographs-pipe-tests-" + Guid.NewGuid().ToString("N"));
    private bool preserveDirectory;
    private string Fixture => Path.Combine(directory, "PipeFixture.dll");

    public Phase11ProcessTests()
    {
        Directory.CreateDirectory(directory);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("PipeFixture", [CSharpSyntaxTree.ParseText(FixtureSource)], references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        var emitted = compilation.Emit(Fixture);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        File.WriteAllText(Path.ChangeExtension(Fixture, ".runtimeconfig.json"), JsonSerializer.Serialize(new
        {
            runtimeOptions = new { tfm = "net10.0", framework = new { name = "Microsoft.NETCore.App", version = Environment.Version.ToString() } }
        }));
    }

    [Fact]
    public async Task CancellationOfLivePreparationTerminatesItsChildTree()
    {
        using var cancellation = new CancellationTokenSource();
        var operation = ProjectPreparation.RunDotnetAsync(directory, null, cancellation.Token, Fixture, "live", directory);
        Process? parent = null, child = null;
        try
        {
            parent = await ReadProcessAsync("parent.pid", operation);
            child = await ReadProcessAsync("child.pid", operation);
            Assert.False(parent.HasExited);
            Assert.False(child.HasExited);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(10)));
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            cancellation.Cancel();
            await CleanupAsync(operation, parent, child);
        }
    }

    [Fact]
    public async Task CancellationInterruptsActualInheritedPipesAfterParentExit()
    {
        using var cancellation = new CancellationTokenSource();
        var operation = ProjectPreparation.RunDotnetAsync(directory, null, cancellation.Token, Fixture, "orphan", directory);
        Process? child = null;
        try
        {
            child = await ReadProcessAsync("child.pid", operation);
            await UntilAsync(() => File.Exists(Path.Combine(directory, "parent-exiting")), operation);
            var parentPid = int.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "parent.pid")));
            await UntilAsync(() => HasExited(parentPid), operation);
            await UntilAsync(() => File.Exists(Path.Combine(directory, "child-holds-pipes-after-parent-exit")), operation);
            Assert.False(child.HasExited);
            // A stable pending interval after the child's handshake distinguishes pipe
            // ownership from an unscheduled parent-exit continuation.
            Assert.NotSame(operation, await Task.WhenAny(operation, Task.Delay(250)));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(10)));
            // An already reparented process is outside Process.Kill(entireProcessTree)'s
            // reachable tree. The test owns and explicitly cleans that surviving child.
        }
        finally
        {
            cancellation.Cancel();
            await CleanupAsync(operation, child);
        }
    }

    [Fact]
    public async Task OversizedActualStderrFailsPromptlyAndTerminatesPreparation()
    {
        using var cancellation = new CancellationTokenSource();
        var operation = ProjectPreparation.RunDotnetAsync(directory, null, cancellation.Token, Fixture, "flood", directory);
        try
        {
            var error = await Assert.ThrowsAsync<GraphException>(() => operation.WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal(4, error.ExitCode);
            Assert.Contains("safety limit", error.Message);
            var parentPid = int.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "parent.pid")));
            await UntilAsync(() => HasExited(parentPid));
        }
        finally
        {
            cancellation.Cancel();
            await CleanupAsync(operation);
        }
    }

    private async Task<Process> ReadProcessAsync(string file, Task operation)
    {
        var path = Path.Combine(directory, file);
        await UntilAsync(() => File.Exists(path), operation);
        return Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(path)));
    }

    private static async Task UntilAsync(Func<bool> condition, Task? operation = null)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (operation?.IsCompleted == true)
            {
                await operation;
                Assert.Fail("Preparation completed before the fixture reached the required lifecycle state.");
            }
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(15), "Fixture did not reach the required lifecycle state.");
            await Task.Delay(20);
        }
    }

    private static bool HasExited(int pid)
    {
        try { using var process = Process.GetProcessById(pid); return process.HasExited; }
        catch (ArgumentException) { return true; }
    }

    private async Task CleanupAsync(Task operation, params Process?[] processes)
    {
        // Also discover a PID if an assertion failed between fixture startup and handle acquisition.
        var owned = processes.OfType<Process>().ToList();
        var knownIds = owned.Select(process => process.Id).ToHashSet();
        foreach (var file in new[] { "parent.pid", "child.pid" })
        {
            var path = Path.Combine(directory, file);
            if (!File.Exists(path) || !int.TryParse(await File.ReadAllTextAsync(path), out var pid) || !knownIds.Add(pid)) continue;
            try { owned.Add(Process.GetProcessById(pid)); }
            catch (ArgumentException) { /* Already exited. */ }
        }
        foreach (var process in owned)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (InvalidOperationException) { /* Concurrent exit. */ }
            catch
            {
                preserveDirectory = true;
                throw;
            }
            finally { process.Dispose(); }
        }
        try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) when (ex is OperationCanceledException or GraphException) { /* Original assertion wins. */ }
        catch (TimeoutException ex)
        {
            preserveDirectory = true;
            throw new InvalidOperationException($"Preparation cleanup is still running; fixture preserved at {directory}.", ex);
        }
    }

    public void Dispose() { if (!preserveDirectory && Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }

    private const string FixtureSource = """
        using System;
        using System.Diagnostics;
        using System.IO;
        using System.Reflection;
        using System.Threading.Tasks;

        internal static class Program
        {
            private static async Task Main(string[] args)
            {
                string mode = args[0], root = args[1];
                string marker = mode == "child" ? "child.pid" : "parent.pid";
                string temporary = Path.Combine(root, marker + ".tmp");
                File.WriteAllText(temporary, Environment.ProcessId.ToString());
                File.Move(temporary, Path.Combine(root, marker));
                if (mode == "child")
                {
                    int parentPid = int.Parse(File.ReadAllText(Path.Combine(root, "parent.pid")));
                    var deadline = Stopwatch.StartNew();
                    while (ParentExists(parentPid) && deadline.Elapsed < TimeSpan.FromSeconds(30)) await Task.Delay(10);
                    if (!ParentExists(parentPid))
                    {
                        Console.Out.WriteLine("child holds stdout after parent exit");
                        Console.Error.WriteLine("child holds stderr after parent exit");
                        File.WriteAllText(Path.Combine(root, "child-holds-pipes-after-parent-exit"), "ready");
                        await Task.Delay(30000);
                    }
                    return;
                }
                if (mode == "flood")
                {
                    Console.Error.Write(new string('x', 2 * 1024 * 1024));
                    await Task.Delay(30000);
                    return;
                }
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
                start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
                start.ArgumentList.Add("child");
                start.ArgumentList.Add(root);
                using var child = Process.Start(start);
                while (!File.Exists(Path.Combine(root, "child.pid"))) await Task.Delay(10);
                if (mode == "orphan") { File.WriteAllText(Path.Combine(root, "parent-exiting"), "ready"); return; }
                await Task.Delay(30000);
            }

            private static bool ParentExists(int pid)
            {
                try { using var parent = Process.GetProcessById(pid); return !parent.HasExited; }
                catch (ArgumentException) { return false; }
            }
        }
        """;
}
