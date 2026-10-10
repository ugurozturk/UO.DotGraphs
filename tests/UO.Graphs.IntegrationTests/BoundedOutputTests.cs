using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UO.Graphs.Core;
using UO.Graphs.Storage.Sqlite;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests
{
    // Synthetic output regression data, not acceptance evidence for a real workspace.
    private GraphSnapshot BoundedOutputFixture(int diagnosticCount = 1930, int projectCount = 31)
    {
        var graph = QueryFixture(partial: true);
        var projects = Enumerable.Range(0, projectCount).Select(i => new ProjectContext(
            i == 0 ? "project" : $"project-{i:D3}", $"/workspace/Project{i:D3}.csproj", $"Assembly{i:D3}",
            "net10.0", "Debug", "10.0.201", "14.0", ["DEBUG", "TRACE"], "/sdk/MSBuild.dll", "18.0",
            ["net9.0"], ["/workspace/Dependency.csproj"], ["/analyzers/PrivateAnalyzer.dll"],
            [new("/refs/Library.dll", "/output/Library.dll", ["REFERENCE_SYMBOL"])])).Reverse().ToArray();
        var diagnostics = Enumerable.Range(0, diagnosticCount).Select(i =>
            $"{i:D4}: Diagnostic \"escaped\" \\ path\n" + string.Concat(Enumerable.Repeat("😀ş", 260))).Reverse().ToArray();
        var builder = new RelationshipBuilder(graph.Analysis.Id);
        for (var line = 20; line < 26; line++)
            builder.Add(RelationshipKind.CALLS,
                new("A", "M:Fixture.A", "Fixture.A", "Fixture", "project", "A"),
                new("Isolated", "M:Fixture.Isolated", "Fixture.Isolated", "Fixture", "project", "Isolated"),
                "project", Confidence.Exact, new("fixture.cs", line, 1, line, 5, "hash"), "Six bound call sites");
        graph = graph with
        {
            Analysis = graph.Analysis with { Projects = projects, Diagnostics = diagnostics, RootPath = "/workspace" },
            Relationships = [.. graph.Relationships, .. builder.Relationships],
            Occurrences = [.. graph.Occurrences, .. builder.Occurrences]
        };
        new GraphStore(Database).Replace(graph);
        return graph;
    }

    private static JsonDocument AssertBoundedJson((int Code, string Output, string Error) result,
        int expectedCode = 0, int? maxChars = 12000)
    {
        Assert.True(result.Code == expectedCode, result.Error);
        Assert.Empty(result.Error);
        Assert.EndsWith("\n", result.Output);
        Assert.DoesNotContain('\n', result.Output[..^1]);
        Assert.DoesNotContain('\r', result.Output);
        var document = JsonDocument.Parse(result.Output);
        var budget = document.RootElement.GetProperty("budget");
        var count = result.Output.EnumerateRunes().Count();
        Assert.Equal("unicodeScalarValuesIncludingFinalLF", budget.GetProperty("unit").GetString());
        Assert.Equal(count, budget.GetProperty("actualChars").GetInt32());
        if (maxChars is { } maximum)
        {
            Assert.Equal(maximum, budget.GetProperty("maxChars").GetInt32());
            Assert.True(count <= maximum, $"{count} > {maximum}");
        }
        else Assert.Equal(JsonValueKind.Null, budget.GetProperty("maxChars").ValueKind);
        return document;
    }

    [Theory]
    [InlineData("symbol", "A")]
    [InlineData("callers", "Isolated")]
    public async Task BoundedOutputLargeDiagnosticsKeepResultEvidenceAndHaveOneAnalysisCopy(string command, string symbol)
    {
        var graph = BoundedOutputFixture();
        var before = await File.ReadAllBytesAsync(Database);
        using var compact = AssertBoundedJson(await Run("query", command, symbol, "--db", Database));
        var root = compact.RootElement;
        Assert.Equal(6, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("summary", root.GetProperty("details").GetString());
        Assert.Equal("partial", root.GetProperty("completeness").GetProperty("coverage").GetString());
        Assert.Equal(graph.Analysis.Limitations, root.GetProperty("completeness").GetProperty("limitations").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("notChecked", root.GetProperty("stale").GetString());
        var analysis = root.GetProperty("analysisContext");
        Assert.False(analysis.TryGetProperty("diagnostics", out _));
        Assert.False(analysis.TryGetProperty("limitations", out _));
        Assert.Equal(31, analysis.GetProperty("totalProjects").GetInt32());
        Assert.Equal(30, analysis.GetProperty("omittedProjects").GetInt32());
        var project = Assert.Single(analysis.GetProperty("projects").EnumerateArray());
        Assert.Equal("project", project.GetProperty("id").GetString());
        Assert.Equal("net10.0", project.GetProperty("targetFramework").GetString());
        Assert.Equal("10.0.201", project.GetProperty("sdkVersion").GetString());
        Assert.Equal("net9.0", Assert.Single(project.GetProperty("skippedTargetFrameworks").EnumerateArray()).GetString());
        Assert.False(project.TryGetProperty("analyzerReferences", out _));
        Assert.False(project.TryGetProperty("referenceContexts", out _));
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());
        var diagnostics = root.GetProperty("analysisDiagnostics");
        Assert.Equal(1930, diagnostics.GetProperty("total").GetInt32());
        var samples = diagnostics.GetProperty("samples").EnumerateArray().ToArray();
        Assert.InRange(samples.Length, 1, 3);
        Assert.Equal(1930 - samples.Length, diagnostics.GetProperty("omitted").GetInt32());
        for (var i = 0; i < samples.Length; i++)
        {
            Assert.StartsWith($"{i:D4}:", samples[i].GetProperty("message").GetString());
            Assert.True(samples[i].GetProperty("message").GetString()!.EnumerateRunes().Count() <= 300);
            Assert.True(samples[i].GetProperty("truncated").GetBoolean());
            Assert.Equal(new[] { "message", "truncated" }, samples[i].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        }
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal("A", item.GetProperty("symbol").GetProperty("id").GetString());
        if (command == "callers")
        {
            var step = Assert.Single(item.GetProperty("path").EnumerateArray());
            Assert.Equal(6, step.GetProperty("evidence").GetArrayLength());
            Assert.False(step.GetProperty("evidenceTruncated").GetBoolean());
            Assert.Equal("hash", step.GetProperty("evidence")[0].GetProperty("source").GetProperty("sha256").GetString());
        }
        using var full = AssertBoundedJson(await Run("query", command, symbol, "--details", "full", "--db", Database), maxChars: null);
        Assert.Equal("full", full.RootElement.GetProperty("details").GetString());
        Assert.Equal(root.GetProperty("items").GetRawText(), full.RootElement.GetProperty("items").GetRawText());
        Assert.Equal(graph.Analysis.Diagnostics.Order(StringComparer.Ordinal), full.RootElement.GetProperty("analysisDiagnostics").GetProperty("samples").EnumerateArray().Select(x => x.GetProperty("message").GetString()));
        Assert.All(full.RootElement.GetProperty("analysisDiagnostics").GetProperty("samples").EnumerateArray(), x => Assert.False(x.GetProperty("truncated").GetBoolean()));
        Assert.Equal(0, full.RootElement.GetProperty("analysisDiagnostics").GetProperty("omitted").GetInt32());
        Assert.False(full.RootElement.GetProperty("analysisContext").TryGetProperty("diagnostics", out _));
        Assert.Equal(31, full.RootElement.GetProperty("analysisContext").GetProperty("projects").GetArrayLength());
        Assert.Equal("/analyzers/PrivateAnalyzer.dll", full.RootElement.GetProperty("analysisContext").GetProperty("projects")[0].GetProperty("analyzerReferences")[0].GetString());
        Assert.Equal(before, await File.ReadAllBytesAsync(Database));
    }

    [Fact]
    public async Task BoundedOutputStatusUsesOrdinalProjectSampleAndFullRestoresMetadata()
    {
        var graph = BoundedOutputFixture();
        using var summary = AssertBoundedJson(await Run("status", "--db", Database));
        var root = summary.RootElement;
        Assert.Equal(6, root.GetProperty("contractVersion").GetInt32());
        Assert.Equal(3, root.GetProperty("schemaVersion").GetInt32());
        var analysis = root.GetProperty("analysis");
        Assert.Equal(31, analysis.GetProperty("totalProjects").GetInt32());
        Assert.Equal(21, analysis.GetProperty("omittedProjects").GetInt32());
        Assert.Equal(graph.Analysis.Projects.Select(p => p.Id).Order(StringComparer.Ordinal).Take(10), analysis.GetProperty("projects").EnumerateArray().Select(p => p.GetProperty("id").GetString()));
        Assert.Equal(21, root.GetProperty("metadataSummary").GetProperty("omittedProjects").GetInt32());
        Assert.True(root.GetProperty("metadataSummary").GetProperty("projectDetailsOmitted").GetBoolean());
        using var full = AssertBoundedJson(await Run("status", "--details", "full", "--db", Database), maxChars: null);
        Assert.Equal(31, full.RootElement.GetProperty("analysis").GetProperty("projects").GetArrayLength());
        Assert.False(full.RootElement.GetProperty("metadataSummary").GetProperty("projectDetailsOmitted").GetBoolean());
        Assert.Equal(0, full.RootElement.GetProperty("metadataSummary").GetProperty("omittedProjects").GetInt32());
    }

    [Fact]
    public async Task BoundedOutputPrunesDiagnosticAndProjectSamplesBeforeGraphRecords()
    {
        BoundedOutputFixture();
        using var summary = AssertBoundedJson(await Run("status", "--max-chars", "3000", "--db", Database), maxChars: 3000);
        Assert.Empty(summary.RootElement.GetProperty("analysisDiagnostics").GetProperty("samples").EnumerateArray());
        Assert.True(summary.RootElement.GetProperty("analysis").GetProperty("omittedProjects").GetInt32() > 21);
        using var query = AssertBoundedJson(await Run("query", "callers", "Isolated", "--evidence-limit", "1", "--db", Database));
        var step = Assert.Single(Assert.Single(query.RootElement.GetProperty("items").EnumerateArray()).GetProperty("path").EnumerateArray());
        Assert.True(step.GetProperty("evidenceTruncated").GetBoolean());
        Assert.Single(step.GetProperty("evidence").EnumerateArray());
        Assert.Contains("evidenceLimit", query.RootElement.GetProperty("limits").GetProperty("reasons").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task BoundedOutputPrunesWholeResultTailsAndPreservesDomainOutcome()
    {
        QueryFixture(fanout: 100);
        using var full = AssertBoundedJson(await Run("query", "callees", "Isolated", "--details", "full", "--db", Database), maxChars: null);
        using var summary = AssertBoundedJson(await Run("query", "callees", "Isolated", "--db", Database));
        var root = summary.RootElement;
        var returned = root.GetProperty("items").GetArrayLength();
        Assert.InRange(returned, 1, 99);
        Assert.True(root.GetProperty("truncated").GetBoolean());
        Assert.Equal(returned, root.GetProperty("limits").GetProperty("returned").GetInt32());
        Assert.Contains("maxChars", root.GetProperty("limits").GetProperty("reasons").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(full.RootElement.GetProperty("outcome").GetString(), root.GetProperty("outcome").GetString());
        for (var i = 0; i < returned; i++) Assert.Equal(full.RootElement.GetProperty("items")[i].GetRawText(), root.GetProperty("items")[i].GetRawText());
    }

    [Theory]
    [InlineData("symbol", "A", null, 0)]
    [InlineData("path", "A", "D", 0)]
    [InlineData("callers", "Overload", null, 7)]
    public async Task BoundedOutputMinimumRetainsOneWholeResultAndExactScalarBoundary(string command, string symbol, string? target, int domainCode)
    {
        var graph = QueryFixture();
        graph = graph with { Analysis = graph.Analysis with { Limitations = ["Unicode 😀 ş and escape \" \\ \n"] } };
        new GraphStore(Database).Replace(graph);
        var args = target is null ? new[] { "query", command, symbol } : ["query", command, symbol, target];
        using var full = AssertBoundedJson(await Run([.. args, "--details", "full", "--db", Database]), domainCode, null);
        var failure = await Run([.. args, "--max-chars", "1", "--db", Database]);
        Assert.Equal(2, failure.Code);
        Assert.Empty(failure.Output);
        Assert.Contains("minimum", failure.Error, StringComparison.OrdinalIgnoreCase);
        var required = int.Parse(Regex.Matches(failure.Error, @"\b\d+\b")[^1].Value, CultureInfo.InvariantCulture);
        using var exact = AssertBoundedJson(await Run([.. args, "--max-chars", required.ToString(CultureInfo.InvariantCulture), "--db", Database]), domainCode, required);
        Assert.Equal(required, exact.RootElement.GetProperty("budget").GetProperty("actualChars").GetInt32());
        Assert.Single(exact.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(full.RootElement.GetProperty("items")[0].GetRawText(), exact.RootElement.GetProperty("items")[0].GetRawText());
        Assert.Equal(full.RootElement.GetProperty("outcome").GetString(), exact.RootElement.GetProperty("outcome").GetString());
        if (command == "path") Assert.Equal(2, exact.RootElement.GetProperty("items")[0].GetProperty("path").GetArrayLength());
        if (domainCode == 7) Assert.True(exact.RootElement.GetProperty("truncated").GetBoolean());
        var below = await Run([.. args, "--max-chars", (required - 1).ToString(CultureInfo.InvariantCulture), "--db", Database]);
        Assert.Equal(2, below.Code);
        Assert.Empty(below.Output);
    }

    [Fact]
    public async Task BoundedOutputEmptyResultsKeepResolvedStartContextAndUnknownFreshness()
    {
        BoundedOutputFixture(diagnosticCount: 0);
        using var query = AssertBoundedJson(await Run("query", "callers", "ReferenceOnly", "--db", Database));
        Assert.Empty(query.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("ReferenceOnly", query.RootElement.GetProperty("resolvedStart").GetProperty("id").GetString());
        Assert.Equal("project", Assert.Single(query.RootElement.GetProperty("analysisContext").GetProperty("projects").EnumerateArray()).GetProperty("id").GetString());
        using var missing = AssertBoundedJson(await Run("query", "symbol", "absent", "--db", Database), expectedCode: 8);
        Assert.Empty(missing.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("symbolNotFound", missing.RootElement.GetProperty("outcome").GetString());
        using var checkedQuery = AssertBoundedJson(await Run("query", "symbol", "A", "--check-freshness", "--db", Database));
        Assert.Equal("unknown", checkedQuery.RootElement.GetProperty("stale").GetString());
        foreach (var command in new[] { new[] { "query", "symbol", "A" }, ["status"] })
        {
            var rejected = await Run([.. command, "--require-fresh", "--db", Database]);
            Assert.Equal(9, rejected.Code);
            Assert.Empty(rejected.Output);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundedOutputOversizedProjectMetadataFitsButRequiredSourceStillFails(bool oversizedProject)
    {
        var graph = BoundedOutputFixture(diagnosticCount: 0, projectCount: 1);
        if (oversizedProject)
            graph = graph with { Analysis = graph.Analysis with { Projects = [graph.Analysis.Projects[0] with { PreprocessorSymbols = [new string('P', 20000)] }] } };
        else
            graph = graph with { Entities = graph.Entities.Select(e => e.Id == "A" ? e with { Sources = [new(new string('F', 20000) + ".cs", 1, 1, 1, 2, "hash")] } : e).ToArray() };
        new GraphStore(Database).Replace(graph);
        var before = await File.ReadAllBytesAsync(Database);
        var result = await Run("query", "symbol", "A", "--db", Database);
        if (oversizedProject)
        {
            using var summary = AssertBoundedJson(result);
            Assert.Single(summary.RootElement.GetProperty("items").EnumerateArray());
            Assert.Contains(summary.RootElement.GetProperty("metadataSummary").GetProperty("fields").EnumerateArray(),
                f => f.GetProperty("shortened").GetInt32() > 0);
        }
        else
        {
            Assert.Equal(2, result.Code);
            Assert.Empty(result.Output);
            Assert.Contains("minimum", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        using var full = AssertBoundedJson(await Run("query", "symbol", "A", "--details", "full", "--db", Database), maxChars: null);
        Assert.Single(full.RootElement.GetProperty("items").EnumerateArray());
        Assert.True(full.RootElement.GetProperty("budget").GetProperty("actualChars").GetInt32() > 12000);
        Assert.Equal(before, await File.ReadAllBytesAsync(Database));
    }

    [Fact]
    public async Task BoundedOutputFullStillHonorsTraversalAndEvidenceLimits()
    {
        BoundedOutputFixture();
        using var full = AssertBoundedJson(await Run("query", "callers", "Isolated", "--details", "full", "--evidence-limit", "2", "--db", Database), maxChars: null);
        var item = Assert.Single(full.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(2, item.GetProperty("path")[0].GetProperty("evidence").GetArrayLength());
        Assert.True(item.GetProperty("path")[0].GetProperty("evidenceTruncated").GetBoolean());
        using var ambiguous = AssertBoundedJson(await Run("query", "symbol", "Overload", "--details", "full", "--limit", "1", "--db", Database), expectedCode: 7, maxChars: null);
        Assert.Single(ambiguous.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("ambiguousSymbol", ambiguous.RootElement.GetProperty("outcome").GetString());
        Assert.Contains("limit", ambiguous.RootElement.GetProperty("limits").GetProperty("reasons").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task BoundedOutputIncludesEveryProjectAlongAPathAndDropsUnrelatedContexts()
    {
        var graph = BoundedOutputFixture(diagnosticCount: 0);
        string Project(string id) => id switch { "B" => "project-001", "C" => "project-002", "D" => "project-003", _ => "project" };
        var builder = new RelationshipBuilder(graph.Analysis.Id);
        foreach (var relationship in graph.Relationships)
        foreach (var occurrence in graph.Occurrences.Where(o => o.RelationshipId == relationship.Id))
            builder.Add(relationship.Kind, relationship.Source with { ProjectId = Project(relationship.Source.Id) },
                relationship.Target is { } target ? target with { ProjectId = Project(target.Id) } : null,
                Project(relationship.Source.Id), relationship.Confidence, occurrence.Source, occurrence.Reason, occurrence.BoundTarget, occurrence.RelatedSymbols);
        graph = graph with
        {
            Entities = graph.Entities.Select(e => e with { ProjectId = Project(e.Id) }).ToArray(),
            Relationships = builder.Relationships, Occurrences = builder.Occurrences
        };
        new GraphStore(Database).Replace(graph);
        using var path = AssertBoundedJson(await Run("query", "path", "A", "D", "--db", Database));
        var root = path.RootElement;
        var steps = Assert.Single(root.GetProperty("items").EnumerateArray()).GetProperty("path").EnumerateArray().ToArray();
        Assert.Equal(2, steps.Length);
        var expected = steps.SelectMany(step => new[]
        {
            step.GetProperty("relationship").GetProperty("source").GetProperty("projectId").GetString(),
            step.GetProperty("relationship").GetProperty("target").GetProperty("projectId").GetString()
        }).Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(3, expected.Length);
        Assert.Equal(expected, root.GetProperty("analysisContext").GetProperty("projects").EnumerateArray().Select(p => p.GetProperty("id").GetString()));
        Assert.Equal(28, root.GetProperty("metadataSummary").GetProperty("omittedProjects").GetInt32());
    }

    [Fact]
    public async Task BoundedOutputKeepsInferredExternalAndQuerySpecificEvidence()
    {
        var graph = BoundedOutputFixture(diagnosticCount: 4);
        var builder = new RelationshipBuilder(graph.Analysis.Id);
        builder.Add(RelationshipKind.CALLS, new("A", "M:Fixture.A", "Fixture.A", "Fixture", "project", "A"),
            new("external", "M:Library.Run", "Library.Run()", "Library", null), "project", Confidence.Exact,
            new("fixture.cs", 40, 1, 40, 5, "hash"), "Metadata bound target");
        new GraphStore(Database).Replace(graph with { Relationships = [.. graph.Relationships, .. builder.Relationships], Occurrences = [.. graph.Occurrences, .. builder.Occurrences] });
        using var external = AssertBoundedJson(await Run("query", "path", "A", "external", "--db", Database));
        var item = Assert.Single(external.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("entity").ValueKind);
        Assert.Equal("external", external.RootElement.GetProperty("resolvedTarget").GetProperty("id").GetString());
        Assert.Equal("Metadata bound target", item.GetProperty("path")[0].GetProperty("evidence")[0].GetProperty("reason").GetString());
        using var impact = AssertBoundedJson(await Run("query", "impact", "D", "--include-inferred", "--max-chars", "30000", "--db", Database), maxChars: 30000);
        Assert.Contains(impact.RootElement.GetProperty("diagnostics").EnumerateArray(), x => x.GetProperty("code").GetString() == "potentialImpact");
        var inferred = Assert.Single(impact.RootElement.GetProperty("items").EnumerateArray(), x => x.GetProperty("symbol").GetProperty("id").GetString() == "Inferred");
        Assert.Equal("inferred", inferred.GetProperty("path")[0].GetProperty("relationship").GetProperty("confidence").GetString());
    }

    [Theory]
    [InlineData("query", "symbol", "A", "--details", "invalid")]
    [InlineData("query", "symbol", "A", "--max-chars", "0")]
    [InlineData("query", "symbol", "A", "--max-chars", "1000001")]
    [InlineData("query", "symbol", "A", "--max-chars", "NaN")]
    [InlineData("query", "symbol", "A", "--details", "full", "--max-chars", "12000")]
    [InlineData("status", "--details", "full", "--max-chars", "12000")]
    [InlineData("status", "--details", "invalid")]
    [InlineData("status", "--max-chars", "0")]
    [InlineData("doctor", "--details", "summary")]
    [InlineData("index", "fixture.csproj", "--details", "summary")]
    [InlineData("context", "A", "--details", "summary")]
    [InlineData("export", "--details", "summary")]
    public async Task BoundedOutputInvalidOptionsNeverReadOrCreateDatabase(params string[] arguments)
    {
        var result = await Run([.. arguments, "--db", Database]);
        Assert.Equal(2, result.Code);
        Assert.Empty(result.Output);
        Assert.NotEmpty(result.Error);
        Assert.False(File.Exists(Database));
    }
}
