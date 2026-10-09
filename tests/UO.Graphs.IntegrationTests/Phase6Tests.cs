using System.Diagnostics;
using System.Text.Json;
using UO.Graphs.Core;
using UO.Graphs.Storage.Sqlite;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests
{
    [Fact]
    public async Task IncrementalMatchesCleanFullForEveryMutationAndNoOpSkipsSemanticAnalysis()
    {
        var fixture = CopyFixture("incremental");
        var input = Path.Combine(fixture, "Phase2.slnx");
        var options = new[] { "--framework", "net10.0" };
        var first = await IndexPath(input, options);
        var noOp = await Run(["index", input, "--trust-project", "--incremental", "--db", Database, .. options]);
        Assert.True(noOp.Code == 0, noOp.Error);
        using (var json = JsonDocument.Parse(noOp.Output))
        {
            Assert.Equal("noOp", json.RootElement.GetProperty("outcome").GetString());
            Assert.Equal(0, json.RootElement.GetProperty("projectsAnalyzed").GetInt32());
            Assert.Equal(0, json.RootElement.GetProperty("analysisMilliseconds").GetInt32());
            Assert.True(json.RootElement.GetProperty("bytesHashed").GetInt64() > 0);
        }
        Assert.DoesNotContain("Loading ", noOp.Error);
        Assert.DoesNotContain("Reading symbols", noOp.Error);
        Assert.Equal(first.Analysis.Id, new GraphStore(Database).Status().Analysis.Id);
        await Oracle("no-op");

        // This small independent pair exercises dependent binding as well as source changes.
        var library = Path.Combine(fixture, "Library/IncrementalApi.cs");
        var consumer = Path.Combine(fixture, "App/IncrementalUse.cs");
        await File.WriteAllTextAsync(library, "namespace Incremental; public static class Api { public static object Call(object value) => value; }");
        await File.WriteAllTextAsync(consumer, "namespace Incremental; public class Use { public object Run() => Api.Call(1); }");
        await Oracle("file-add");
        var stamp = File.GetLastWriteTimeUtc(library);
        await File.WriteAllTextAsync(library, (await File.ReadAllTextAsync(library)).Replace("=> value;", "=> value.ToString()!;"));
        File.SetLastWriteTimeUtc(library, stamp); // Same timestamp must not hide a semantic change.
        await Oracle("method-body-same-mtime");
        await File.WriteAllTextAsync(library, "namespace Incremental; public static class Api { public static string Call(object value) => value.ToString()!; }");
        await Oracle("public-signature");
        await File.WriteAllTextAsync(library, "namespace Incremental; public static class Api { public static string Call(object value) => value.ToString()!; public static int Call(int value) => value; }");
        var overload = await Oracle("new-overload");
        Assert.Contains(overload.Relationships, r => r.Kind == RelationshipKind.CALLS && r.Target?.DeclarationId == "M:Incremental.Api.Call(System.Int32)");
        await File.WriteAllTextAsync(Path.Combine(fixture, "App/Global.cs"), "global using Alias = System.Int64;");
        await File.WriteAllTextAsync(consumer, "namespace Incremental; public class Use { public Alias Run() => 1; }");
        await Oracle("global-using-add");
        await File.WriteAllTextAsync(Path.Combine(fixture, "App/Global.cs"), "global using Alias = System.Int32;");
        await Oracle("global-using-change");
        File.Move(library, library + ".moved.cs");
        await Oracle("file-move");
        File.Delete(library + ".moved.cs");
        var deleted = await Oracle("file-delete");
        Assert.DoesNotContain(deleted.Entities, e => e.DocumentationId.StartsWith("M:Incremental.Api", StringComparison.Ordinal));
        Assert.DoesNotContain(deleted.Relationships, e => e.Target?.DeclarationId.StartsWith("M:Incremental.Api", StringComparison.Ordinal) == true);
        options = ["--framework", "net10.0", "--configuration", "Release"];
        await Oracle("configuration-and-dependency");
        await File.AppendAllTextAsync(Path.Combine(fixture, "Library/packages.lock.json"), "\n");
        await Oracle("lock-content");
        await File.AppendAllTextAsync(Path.Combine(fixture, "Library/obj/project.assets.json"), "\n");
        await Oracle("assets-content");
        await File.WriteAllTextAsync(Path.Combine(fixture, "Imported.props"), "<Project><PropertyGroup><DefineConstants>$(DefineConstants);EXTRA</DefineConstants></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(fixture, "Directory.Build.targets"), "<Project><Import Project=\"Imported.props\" /></Project>");
        await Oracle("import-add");
        await File.WriteAllTextAsync(Path.Combine(fixture, "Imported.props"), "<Project><PropertyGroup><DefineConstants>$(DefineConstants);CHANGED</DefineConstants></PropertyGroup></Project>");
        await Oracle("import-change");
        options = ["--framework", "netstandard2.1"];
        await Oracle("framework");

        async Task<GraphSnapshot> Oracle(string scenario)
        {
            var incremental = await IndexPath(input, ["--incremental", .. options]);
            var clean = Path.Combine(directory, "clean-" + scenario + ".db");
            var full = await Run(["index", input, "--trust-project", "--full", "--db", clean, .. options]);
            Assert.True(full.Code == 0, scenario + ": " + full.Error);
            var expected = new GraphStore(clean).Find("");
            Assert.Equal(Normalize(expected), Normalize(incremental));
            return incremental;
        }
    }

    private static string Normalize(GraphSnapshot graph) => JsonSerializer.Serialize(new
    {
        analysis = graph.Analysis with { Id = "volatile", CreatedAtUtc = DateTimeOffset.UnixEpoch, FreshnessCheck = null },
        graph.Entities, graph.Relationships,
        occurrences = graph.Occurrences.Select(o => o with { AnalysisId = "volatile" }).ToArray()
    }, GraphJson.Options);

    [Fact]
    public async Task FreshnessChecksAreExplicitReadOnlyAndRejectUnknownOrStale()
    {
        var fixture = CopyFixture("freshness");
        File.WriteAllBytes(Database, []); // A first publication interrupted before its first commit.
        Assert.Equal(4, (await Run("status", "--db", Database)).Code);
        var input = Path.Combine(fixture, "Library/Library.csproj");
        await IndexPath(input, "--framework", "net10.0");
        var before = File.ReadAllBytes(Database);
        var uncheckedStatus = await Run("status", "--db", Database);
        Assert.Contains("notChecked", uncheckedStatus.Output);
        var fresh = await Run("query", "find", "Box", "--require-fresh", "--db", Database);
        Assert.True(fresh.Code == 0, fresh.Error);
        Assert.Contains("\"stale\": \"fresh\"", fresh.Output);
        Assert.DoesNotContain("Loading", fresh.Error);
        Assert.Equal(before, File.ReadAllBytes(Database));
        await File.WriteAllTextAsync(Path.Combine(fixture, "Library/New.cs"), "public class BrandNew { }");
        foreach (var command in new[] { new[] { "status" }, ["query", "find", "Box"], ["context", "Box"], ["export", "--format", "ndjson"] })
        {
            var stale = await Run([.. command, "--require-fresh", "--db", Database]);
            Assert.Equal(9, stale.Code);
            Assert.Contains("stale", stale.Error);
            Assert.Empty(stale.Output);
        }
        // Undeclared custom task dependencies force broad invalidation on every index.
        await File.WriteAllTextAsync(Path.Combine(fixture, "Directory.Build.targets"), "<Project><Target Name=\"CustomNeverRuns\" /></Project>");
        var unknown = await IndexPath(input, "--framework", "net10.0");
        Assert.False(unknown.Fingerprint!.Complete);
        var repeated = await IndexPath(input, "--framework", "net10.0");
        Assert.NotEqual(unknown.Analysis.Id, repeated.Analysis.Id);
        var rejected = await Run("status", "--require-fresh", "--db", Database);
        Assert.Equal(9, rejected.Code);
        Assert.Contains("unknown", rejected.Error);
        var trust = await Run("index", input, "--incremental", "--db", Database);
        Assert.Equal(3, trust.Code);
    }

    [Fact]
    public async Task KilledIndexProcessKeepsSnapshotAndReleasesWriterLease()
    {
        var fixture = CopyFixture("killed");
        var input = Path.Combine(fixture, "Phase2.slnx");
        var original = await IndexPath(input, "--framework", "net10.0");
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Root, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in new[] { Path.Combine(Root, "src/UO.Graphs.Cli/bin/Debug/net10.0/uographs.dll"), "index", input,
            "--full", "--trust-project", "--framework", "net10.0", "--db", Database }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            string? line;
            do { line = await process.StandardError.ReadLineAsync(timeout.Token); }
            while (line is not null && !line.StartsWith("Loading ", StringComparison.Ordinal));
            Assert.NotNull(line);
            var competing = await Run("index", input, "--trust-project", "--framework", "net10.0", "--db", Database);
            Assert.Equal(10, competing.Code);
            Assert.Equal(original.Analysis.Id, new GraphStore(Database).Status().Analysis.Id);
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(timeout.Token);
            Assert.Empty(await stdout);
            Assert.Equal(Normalize(original), Normalize(new GraphStore(Database).Find("")));
            using var lease = new GraphStore(Database).AcquireWriter();
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }

    [Fact]
    public async Task KilledProcessDuringSqlitePublicationRecoversLastCommittedGraph()
    {
        var fixture = CopyFixture("killed-publication");
        var input = Path.Combine(fixture, "Library/Library.csproj");
        var original = await IndexPath(input, "--framework", "net10.0");
        await File.WriteAllTextAsync(Path.Combine(fixture, "Library/Large.cs"),
            "public class Large { " + string.Join(" ", Enumerable.Range(0, 10000).Select(i => "public int Field" + i + ";")) + " }");
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Root, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in new[] { Path.Combine(Root, "src/UO.Graphs.Cli/bin/Debug/net10.0/uographs.dll"), "index", input,
            "--full", "--trust-project", "--framework", "net10.0", "--db", Database }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        var watch = Stopwatch.StartNew();
        try
        {
            while (!process.HasExited && (!File.Exists(Database + "-wal") || new FileInfo(Database + "-wal").Length < 32768))
            {
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(90));
                await Task.Delay(1);
            }
            Assert.False(process.HasExited);
            // WAL pages now exist, but replacement is still in progress.
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Empty(await stdout);
            _ = await stderr;
            Assert.Equal(Normalize(original), Normalize(new GraphStore(Database).Find("")));
            using var lease = new GraphStore(Database).AcquireWriter();
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }

    [Fact]
    public async Task NewlyCreatedImplicitUserImportIsStaleAndRebindsCompilation()
    {
        var previous = Environment.GetEnvironmentVariable("MSBuildUserExtensionsPath");
        var extensions = Path.Combine(directory, "external-msbuild");
        Environment.SetEnvironmentVariable("MSBuildUserExtensionsPath", extensions);
        try
        {
            var fixture = CopyFixture("implicit-import");
            var input = Path.Combine(fixture, "Library/Library.csproj");
            var original = await IndexPath(input, "--framework", "net10.0");
            Assert.True(original.Fingerprint!.Complete);
            var importDirectory = Path.Combine(extensions, "Current/Microsoft.CSharp.targets/ImportBefore");
            Directory.CreateDirectory(importDirectory);
            await File.WriteAllTextAsync(Path.Combine(importDirectory, "Custom.targets"),
                "<Project><PropertyGroup><DefineConstants>$(DefineConstants);EXTERNAL_INPUT</DefineConstants></PropertyGroup></Project>");
            var stale = await Run("status", "--require-fresh", "--db", Database);
            Assert.Equal(9, stale.Code);
            Assert.Contains("stale", stale.Error);
            var updated = await IndexPath(input, "--framework", "net10.0");
            Assert.NotEqual(original.Analysis.Id, updated.Analysis.Id);
            Assert.Contains("EXTERNAL_INPUT", Assert.Single(updated.Analysis.Projects).PreprocessorSymbols);
            var expected = await IndexPath(input, "--full", "--framework", "net10.0");
            Assert.Equal(Normalize(expected), Normalize(updated));
        }
        finally { Environment.SetEnvironmentVariable("MSBuildUserExtensionsPath", previous); }
    }

    [Theory]
    [InlineData("index", "x.csproj", "--full", "--incremental")]
    [InlineData("status", "--incremental")]
    [InlineData("index", "x.csproj", "--check-freshness")]
    [InlineData("doctor", "--require-fresh")]
    public async Task Phase6InvalidOptionsFailBeforeOpeningDatabase(params string[] args)
    {
        var result = await Run(args);
        Assert.Equal(2, result.Code);
        Assert.Empty(result.Output);
    }
}

public sealed class Phase6StorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "uographs-atomic-" + Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(root, "graph.db");

    [Fact]
    public async Task WriterLeaseCoversAnalysisAndConcurrentReadersSeeOnlyWholeGenerations()
    {
        var store = new GraphStore(Database);
        store.Replace(Snapshot("old", 1));
        using (var writer = store.AcquireWriter())
        {
            Assert.Equal(10, Assert.Throws<GraphException>(() => new GraphStore(Database).AcquireWriter()).ExitCode);
            Assert.Equal("old", new GraphStore(Database).Status().Analysis.Id);
            var replacement = Snapshot("new", 30000);
            var writing = Task.Run(() => store.Replace(replacement, writer: writer));
            var reads = 0;
            while (!writing.IsCompleted)
            {
                var status = new GraphStore(Database).Status();
                Assert.Equal(status.Analysis.Id == "old" ? 1 : 30000, status.EntityCount);
                reads++;
            }
            await writing;
            Assert.True(reads > 0);
        }
        Assert.Equal("new", store.Status().Analysis.Id);
        using var next = store.AcquireWriter(); // released lease is reusable
    }

    [Fact]
    public async Task CancellationDuringPublicationRollsBackAndRemovesOldInferenceOnlyOnSuccess()
    {
        var store = new GraphStore(Database);
        var old = Snapshot("old", 1);
        var endpoint = new RelationshipSymbol("old0", "T:old", "old", "test", "p", "old0");
        var builder = new RelationshipBuilder("old");
        builder.Add(RelationshipKind.CALLS, endpoint, endpoint, "p", Confidence.Inferred,
            new("old.cs", 1, 1, 1, 2, "hash"), "Synthetic inference lifecycle storage test; not a framework analyzer.");
        old = old with { Relationships = builder.Relationships, Occurrences = builder.Occurrences };
        store.Replace(old);
        using var cancel = new CancellationTokenSource();
        var replacement = Snapshot("cancelled", 100000);
        var writing = Task.Run(() => store.Replace(replacement, cancel.Token));
        var watch = Stopwatch.StartNew();
        while (!writing.IsCompleted && (!File.Exists(Database + "-wal") || new FileInfo(Database + "-wal").Length < 32768))
        {
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30));
            await Task.Delay(1);
        }
        Assert.False(writing.IsCompleted); // cancel after SQLite has started writing pages
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await writing);
        var retained = store.Find("");
        Assert.Equal("old", retained.Analysis.Id);
        Assert.Single(retained.Relationships);
        store.Replace(Snapshot("final", 1));
        Assert.Empty(store.Find("").Relationships);
        Assert.Empty(store.Find("").Occurrences);
    }

    private static GraphSnapshot Snapshot(string id, int count) => new(
        new(id, DateTimeOffset.UtcNow, "/fixture.csproj", "0.6.0", "5", "partial", "notChecked", [], [], []),
        Enumerable.Range(0, count).Select(i => new GraphEntity(id + i, "type", id, id, "T:" + id + i, "p", Confidence.Exact, [])).ToArray());

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
