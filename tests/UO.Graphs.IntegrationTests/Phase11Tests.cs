using Microsoft.Data.Sqlite;
using UO.Graphs.Core;
using UO.Graphs.Storage.Sqlite;

namespace UO.Graphs.IntegrationTests;

public sealed class Phase11StorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "uographs-phase11-" + Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(root, "graph.db");
    private static GraphSnapshot Snapshot(string id, int count = 1) => new(
        new(id, DateTimeOffset.UtcNow, "/fixture.csproj", "0.9.0", "5", "partial", "notChecked", [], [], []),
        Enumerable.Range(0, count).Select(i => new GraphEntity(id + i, "type", id + i, id + i, "T:" + id + i, "p", Confidence.Exact, [])).ToArray());

    [Fact]
    public void SqliteFullRollsBackAndWriterCanRetry()
    {
        new GraphStore(Database).Replace(Snapshot("old"));
        var full = new GraphStore(Database) { TestMaximumPageCount = 32 };
        Assert.Equal(13, Assert.Throws<SqliteException>(() => full.Replace(Snapshot("large", 10000))).SqliteErrorCode);
        Assert.Equal("old", new GraphStore(Database).Status().Analysis.Id);
        Assert.Single(new GraphStore(Database).Find("").Entities);
        new GraphStore(Database).Replace(Snapshot("retry"));
        Assert.Equal("retry", new GraphStore(Database).Status().Analysis.Id);
    }

    [Theory]
    [InlineData(".writer.lock")]
    [InlineData("-wal")]
    [InlineData("-shm")]
    [InlineData("-journal")]
    public void SymlinkSidecarCannotRedirectWriter(string suffix)
    {
        Directory.CreateDirectory(root);
        var victim = Path.Combine(root, "source.cs");
        File.WriteAllText(victim, "class Preserve {}");
        File.CreateSymbolicLink(Database + suffix, victim);
        Assert.Equal(6, Assert.Throws<GraphException>(() => new GraphStore(Database).AcquireWriter()).ExitCode);
        Assert.Equal("class Preserve {}", File.ReadAllText(victim));
        Assert.False(File.Exists(Database));
    }

    [Fact]
    public void NonDatabaseInputIsNeverReplaced()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Database, "source code or corrupt DB");
        Assert.Throws<SqliteException>(() => new GraphStore(Database).Replace(Snapshot("new")));
        Assert.Equal("source code or corrupt DB", File.ReadAllText(Database));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

public sealed partial class CliTests
{
    [Theory]
    [InlineData("global.json", "{ broken json")]
    [InlineData("Directory.Build.props", "<broken")]
    public async Task MalformedConfigurationDoesNotPublishOrRewriteSource(string name, string content)
    {
        Directory.CreateDirectory(directory);
        var project = Path.Combine(directory, "Project.csproj");
        const string text = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>";
        await File.WriteAllTextAsync(project, text);
        await File.WriteAllTextAsync(Path.Combine(directory, name), content);
        var result = await Run("index", project, "--trust-project", "--db", Database);
        Assert.Equal(4, result.Code);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(Database));
        Assert.Equal(text, await File.ReadAllTextAsync(project));
        Assert.Equal(content, await File.ReadAllTextAsync(Path.Combine(directory, name)));
    }

    [Fact]
    public async Task TrustGatePrecedesMsbuildTargetSideEffects()
    {
        var fixture = CopyFixture("msbuild-side-effects");
        var project = Path.Combine(fixture, "Library/Library.csproj");
        var marker = Path.Combine(fixture, "msbuild-marker.txt");
        var xml = (await File.ReadAllTextAsync(project)).Replace("</Project>",
            "<Target Name=\"WriteMarker\" BeforeTargets=\"ResolveAssemblyReferences\"><WriteLinesToFile File=\"" + marker +
            "\" Lines=\"trusted-side-effect\" Overwrite=\"true\" /></Target></Project>");
        await File.WriteAllTextAsync(project, xml);
        var untrusted = await Run("index", project, "--framework", "net10.0", "--db", Database);
        Assert.Equal(3, untrusted.Code);
        Assert.False(File.Exists(marker));
        var trusted = await Run("index", project, "--framework", "net10.0", "--trust-project", "--db", Database);
        Assert.Equal(0, trusted.Code);
        Assert.Equal("trusted-side-effect", (await File.ReadAllTextAsync(marker)).Trim());
        Assert.Equal(xml, await File.ReadAllTextAsync(project));
    }
}
