using System.Text.Json;
using UO.Graphs.Core;
using UO.Graphs.Roslyn;
using UO.Graphs.Storage.Sqlite;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests
{
    [Fact]
    public async Task CopiedFixtureSelectsRepositorySdkOutsideCheckout()
    {
        // A conflicting ancestor makes missing fixture SDK isolation fail even
        // on development machines that have only the repository SDK installed.
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "global.json"),
            """{"sdk":{"version":"99.0.100","rollForward":"disable"}}""");
        var fixture = CopyFixture("sdk-selection");
        using var pinned = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root, "global.json")));
        var selected = await ProjectPreparation.GetSdkVersionAsync(Path.Combine(fixture, "Library"));
        Assert.Equal(pinned.RootElement.GetProperty("sdk").GetProperty("version").GetString(), selected);
    }

    [Fact]
    public async Task SolutionFormatsAndProjectInputKeepReferencesAndSymbolIdentities()
    {
        GraphSnapshot? previous = null;
        foreach (var input in new[] { "Phase2.sln", "Phase2.slnx", "App/App.csproj" })
        {
            var graph = await IndexFixture(input, "--framework", "net10.0", "--allow-generators");
            Assert.Equal("completeWithinDeclaredScope", graph.Analysis.Coverage);
            Assert.Equal(2, graph.Analysis.Projects.Length);
            Assert.All(graph.Analysis.Projects, p =>
            {
                Assert.Equal("net10.0", p.TargetFramework);
                Assert.Equal("10.0.201", p.SdkVersion);
                Assert.StartsWith("18.3", p.MSBuildVersion);
                Assert.Contains("10.0.201", p.MSBuildPath);
            });
            Assert.Equal("5.0.0.0", graph.Analysis.RoslynVersion);
            Assert.Equal("2", graph.Analysis.IdentityVersion);
            var partial = Assert.Single(graph.Entities, e => e.DocumentationId == "T:Fixture.Box`1");
            Assert.Equal(2, partial.Sources.Length);
            var methods = graph.Entities.Where(e => e.DocumentationId.StartsWith("M:Fixture.Box`1.Run(", StringComparison.Ordinal)).ToArray();
            Assert.Equal(2, methods.Length);
            Assert.Equal(2, methods.Select(e => e.Id).Distinct().Count());
            Assert.Equal(2, graph.Entities.Count(e => e.Name == "Generic"));
            Assert.Contains(graph.Entities, e => e.DocumentationId.Contains("IContract#Run", StringComparison.Ordinal));
            Assert.Contains(graph.Entities, e => e.Name == "op_Addition");
            Assert.Contains(graph.Entities, e => e.Name == "Length");
            Assert.Contains(graph.Entities, e => e.Name == "Nested");
            Assert.Contains(graph.Entities, e => e.Name == "Callback" && e.Kind == "type");
            Assert.Equal(4, graph.Entities.Count(e => e.Kind == "constructor"));
            Assert.Equal(3, graph.Entities.Count(e => e.Kind == "localFunction"));
            Assert.Equal(2, graph.Entities.Count(e => e.Name == "Hidden"));
            foreach (var kind in new[] { "assembly", "namespace", "type", "method", "property", "field", "event", "accessor" })
                Assert.Contains(graph.Entities, e => e.Kind == kind);
            Assert.DoesNotContain(graph.Entities, e => e.Name == "CheckedInGenerated");
            Assert.Contains(graph.Entities, e => e.Name == "Net10Only");
            Assert.DoesNotContain(graph.Entities, e => e.Name == "StandardOnly");
            Assert.Equal(graph.Entities.Length, graph.Entities.Select(e => e.Id).Distinct().Count());
            if (previous is not null)
                Assert.Equal(previous.Entities.Where(e => e.Kind != "solution").Select(e => e.Id), graph.Entities.Where(e => e.Kind != "solution").Select(e => e.Id));
            previous = graph;
        }
    }

    [Fact]
    public async Task RelocationAndBlankLinesKeepEveryDeclarationIdAndPartialEvidence()
    {
        var firstRoot = CopyFixture("first");
        var secondRoot = CopyFixture("second");
        var original = await IndexPath(Path.Combine(firstRoot, "Phase2.slnx"), "--framework", "net10.0", "--allow-generators");
        foreach (var file in Directory.EnumerateFiles(secondRoot, "*.cs", SearchOption.AllDirectories))
            await File.WriteAllTextAsync(file, "\n\n" + await File.ReadAllTextAsync(file));
        var copied = await IndexPath(Path.Combine(secondRoot, "Phase2.slnx"), "--framework", "net10.0", "--allow-generators");
        Assert.Equal("completeWithinDeclaredScope", original.Analysis.Coverage);
        Assert.Equal("completeWithinDeclaredScope", copied.Analysis.Coverage);
        Assert.Equal(original.Entities.Select(e => e.Id), copied.Entities.Select(e => e.Id));
        var a = Assert.Single(original.Entities, e => e.DocumentationId == "T:Fixture.Box`1");
        var b = Assert.Single(copied.Entities, e => e.DocumentationId == a.DocumentationId);
        Assert.Equal(2, b.Sources.Length);
        Assert.Equal(a.Sources[0].StartLine + 2, b.Sources[0].StartLine);
        Assert.NotEqual(a.Sources[0].Sha256, b.Sources[0].Sha256);
    }

    [Fact]
    public async Task FrameworkAndConfigurationRemainSeparateAndRequireSelection()
    {
        var missing = await Run("index", Path.Combine(Root, "samples/Phase2/Library/Library.csproj"), "--trust-project", "--db", Database);
        Assert.Equal(4, missing.Code);
        Assert.Contains("--framework", missing.Error);
        var net10 = await IndexFixture("Library/Library.csproj", "--framework", "net10.0", "--allow-generators");
        var standard = await IndexFixture("Library/Library.csproj", "--framework", "netstandard2.1", "--configuration", "Release", "--allow-generators");
        Assert.Equal("completeWithinDeclaredScope", standard.Analysis.Coverage);
        var context = Assert.Single(standard.Analysis.Projects);
        Assert.Equal("netstandard2.1", context.TargetFramework);
        Assert.Equal("Release", context.Configuration);
        Assert.Equal(["net10.0"], context.SkippedTargetFrameworks!);
        Assert.Contains("NETSTANDARD2_1", context.PreprocessorSymbols);
        Assert.Contains("RELEASE_FEATURE", context.PreprocessorSymbols);
        Assert.Contains(standard.Entities, e => e.Name == "StandardOnly");
        Assert.Contains(standard.Entities, e => e.Name == "ReleaseOnly");
        Assert.DoesNotContain(standard.Entities, e => e.Name is "Net10Only" or "DebugOnly");
        Assert.Empty(net10.Entities.Select(e => e.Id).Intersect(standard.Entities.Select(e => e.Id)));
    }

    [Fact]
    public async Task ConditionalReferencesAndCompileItemsFollowConfiguration()
    {
        var debug = await IndexFixture("App/App.csproj", "--framework", "net10.0", "--allow-generators");
        Assert.Equal(2, debug.Analysis.Projects.Length);
        Assert.DoesNotContain(debug.Entities, e => e.Name == "ReleaseUse");
        var release = await IndexFixture("App/App.csproj", "--framework", "net10.0", "--configuration", "Release", "--allow-generators");
        Assert.Equal("completeWithinDeclaredScope", release.Analysis.Coverage);
        Assert.Equal(3, release.Analysis.Projects.Length);
        Assert.All(release.Analysis.Projects, p => Assert.Equal("Release", p.Configuration));
        Assert.Contains(release.Entities, e => e.Name == "ReleaseUse");
        Assert.Contains(release.Entities, e => e.Name == "ReleaseFeature");
    }

    [Fact]
    public async Task DifferingReferenceFrameworkIsReportedInsteadOfSilentlyMerged()
    {
        var graph = await IndexFixture("Phase2.slnx", "--framework", "netstandard2.1", "--allow-generators");
        Assert.Contains(graph.Analysis.Projects, p => p.TargetFramework == "net10.0");
        Assert.Contains(graph.Analysis.Projects, p => p.TargetFramework == "netstandard2.1");
        Assert.Equal("partial", graph.Analysis.Coverage);
        Assert.Contains(graph.Analysis.Diagnostics, d => d.StartsWith("referenceContextMismatch", StringComparison.Ordinal));
        var library = Assert.Single(graph.Analysis.Projects, p => p.AssemblyName == "Library");
        Assert.DoesNotContain(graph.Relationships, r => r.Kind == RelationshipKind.CALLS &&
            r.ContextId != library.Id && r.Target?.ProjectId == library.Id);
        var reference = Assert.Single(graph.Relationships, r => r.Kind == RelationshipKind.PROJECT_REFERENCES);
        Assert.Equal(Confidence.Unresolved, reference.Confidence);
        Assert.Null(reference.Target);
    }

    [Fact]
    public async Task MissingDependencyAndDocumentPersistPartialDiagnostics()
    {
        var graph = await IndexFixture("Broken/Broken.csproj", "--allow-generators");
        Assert.Equal("partial", graph.Analysis.Coverage);
        Assert.Contains(graph.Analysis.Diagnostics, d => d.Contains("Missing.csproj", StringComparison.Ordinal));
        Assert.Contains(graph.Analysis.Diagnostics, d => d.Contains("MissingFile.cs", StringComparison.Ordinal));
        Assert.Contains(graph.Analysis.Diagnostics, d => d.Contains("CS0246", StringComparison.Ordinal));
        Assert.Contains(graph.Entities, e => e.Name == "Survivable");
        var status = await Run("status", "--db", Database);
        Assert.Equal(0, status.Code);
        Assert.Contains("partial", status.Output);
    }

    [Fact]
    public async Task GeneratorsNeedPermissionAndGeneratedPolicyDoesNotRunDiagnosticAnalyzers()
    {
        var fixture = CopyFixture("generator");
        var generatorMarker = Path.Combine(fixture, "generator.marker");
        var analyzerMarker = Path.Combine(fixture, "analyzer.marker");
        Environment.SetEnvironmentVariable("UOGRAPHS_GENERATOR_MARKER", generatorMarker);
        Environment.SetEnvironmentVariable("UOGRAPHS_ANALYZER_MARKER", analyzerMarker);
        try
        {
            var input = Path.Combine(fixture, "GeneratedConsumer/GeneratedConsumer.csproj");
            var blocked = await IndexPath(input, "--generated", "include");
            Assert.Equal("partial", blocked.Analysis.Coverage);
            Assert.Contains(blocked.Analysis.Diagnostics, d => d.StartsWith("generatorsSkipped", StringComparison.Ordinal));
            Assert.False(File.Exists(generatorMarker));
            Assert.DoesNotContain(blocked.Entities, e => e.Name == "Produced");
            var allowed = await IndexPath(input, "--allow-generators", "--generated", "include");
            Assert.Equal("completeWithinDeclaredScope", allowed.Analysis.Coverage);
            Assert.True(File.Exists(generatorMarker));
            Assert.False(File.Exists(analyzerMarker));
            var produced = Assert.Single(allowed.Entities, e => e.Name == "Produced");
            Assert.True(Assert.Single(produced.Sources).Generated);
            var excluded = await IndexPath(input, "--allow-generators");
            Assert.Equal("completeWithinDeclaredScope", excluded.Analysis.Coverage);
            Assert.DoesNotContain(excluded.Entities, e => e.Name == "Produced");
            Assert.Contains(excluded.Analysis.Diagnostics, d => d.StartsWith("generatedExcluded", StringComparison.Ordinal));
            await File.WriteAllTextAsync(input, (await File.ReadAllTextAsync(input)).Replace("<ItemGroup>", "<PropertyGroup><AssemblyName>ThrowingConsumer</AssemblyName></PropertyGroup><ItemGroup>"));
            var throwing = await IndexPath(input, "--allow-generators");
            Assert.Equal("partial", throwing.Analysis.Coverage);
            Assert.Contains(throwing.Analysis.Diagnostics, d => d.Contains("Fixture generator failure", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("UOGRAPHS_GENERATOR_MARKER", null);
            Environment.SetEnvironmentVariable("UOGRAPHS_ANALYZER_MARKER", null);
        }
    }

    [Fact]
    public async Task GeneratedPolicyUsesWorkspaceItemsWithoutScanningObjOrBin()
    {
        var fixture = CopyFixture("generated");
        Directory.CreateDirectory(Path.Combine(fixture, "Library/obj/random"));
        await File.WriteAllTextAsync(Path.Combine(fixture, "Library/obj/random/Unlisted.cs"), "public class Unlisted { }");
        var graph = await IndexPath(Path.Combine(fixture, "Library/Library.csproj"), "--framework", "net10.0", "--generated", "include", "--allow-generators");
        Assert.Contains(graph.Entities, e => e.Name == "CheckedInGenerated" && e.Sources.All(s => s.Generated));
        Assert.DoesNotContain(graph.Entities, e => e.Name == "Unlisted");
    }

    [Fact]
    public async Task UnsupportedAndUnloadedSolutionEntriesAreReported()
    {
        var fixture = CopyFixture("unsupported");
        await File.WriteAllTextAsync(Path.Combine(fixture, "Legacy.csproj"), "<Project><PropertyGroup><TargetFrameworkVersion>v4.8</TargetFrameworkVersion></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(fixture, "Unsupported.slnx"), "<Solution><Project Path=\"App/App.csproj\" /><Project Path=\"Legacy.csproj\" /><Project Path=\"Missing.vbproj\" /></Solution>");
        var graph = await IndexPath(Path.Combine(fixture, "Unsupported.slnx"), "--framework", "net10.0", "--allow-generators");
        Assert.Equal("partial", graph.Analysis.Coverage);
        Assert.Contains(graph.Analysis.Diagnostics, d => d.Contains("non-SDK-style", StringComparison.Ordinal));
        Assert.Contains(graph.Analysis.Diagnostics, d => d.Contains("Missing.vbproj", StringComparison.Ordinal));
    }

    private Task<GraphSnapshot> IndexFixture(string input, params string[] options) =>
        IndexPath(Path.Combine(Root, "samples/Phase2", input), options);

    private async Task<GraphSnapshot> IndexPath(string input, params string[] options)
    {
        var result = await Run(["index", input, "--trust-project", "--db", Database, .. options]);
        Assert.True(result.Code == 0, result.Error + "\n" + result.Output);
        return new GraphStore(Database).Find("");
    }

    private string CopyFixture(string name)
    {
        var destination = Path.Combine(directory, name);
        var source = Path.Combine(Root, "samples/Phase2");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        // These projects leave the checkout and its ancestor SDK pin. Preserve
        // the same SDK for preparation, preprocessing and workspace loading.
        File.Copy(Path.Combine(Root, "global.json"), Path.Combine(destination, "global.json"));
        return destination;
    }
}
