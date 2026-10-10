using System.Text.Json;
using UO.Graphs.Core;
using UO.Graphs.Storage.Sqlite;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests
{
    private GraphSnapshot LargeMetadataFixture()
    {
        var graph = BoundedOutputFixture(diagnosticCount: 2400, projectCount: 114);
        var limitations = Enumerable.Range(0, 114).Select(i =>
            $"{i:D3}: unsupported dependency \"path\" \\ \n" + string.Concat(Enumerable.Repeat("😀ş", 400))).Reverse().ToArray();
        // A single CALLS relationship with six sites, in either query direction.
        var relationship = Assert.Single(graph.Relationships, r => r.Target?.Id == "Isolated");
        graph = graph with
        {
            Analysis = graph.Analysis with
            {
                DependencyFingerprint = new(false, "entireSelectedProjectClosure", limitations),
                Limitations = ["Generators disabled", .. limitations],
                Projects = graph.Analysis.Projects.Select(p => p with
                {
                    Path = p.Path + string.Concat(Enumerable.Repeat("😀ş", 400)),
                    PreprocessorSymbols = Enumerable.Range(0, 114).Select(i => $"SYMBOL_{i:D3}" + new string('P', 400)).ToArray()
                }).ToArray()
            },
            Relationships = [relationship],
            Occurrences = graph.Occurrences.Where(o => o.RelationshipId == relationship.Id).ToArray()
        };
        new GraphStore(Database).Replace(graph);
        return graph;
    }

    [Theory]
    [InlineData("status", "")]
    [InlineData("find", "Fixture.Isolated")]
    [InlineData("callers", "Isolated")]
    [InlineData("callees", "A")]
    public async Task MetadataBudgetLargeFingerprintPreservesAnswersAtDefaultBudget(string command, string symbol)
    {
        var graph = LargeMetadataFixture();
        var args = command == "status" ? new[] { "status" } : ["query", command, symbol];
        var first = await Run([.. args, "--db", Database]);
        using var summary = AssertBoundedJson(first);
        Assert.Equal(first, await Run([.. args, "--db", Database]));
        using var full = AssertBoundedJson(await Run([.. args, "--details", "full", "--db", Database]), maxChars: null);
        var root = summary.RootElement;
        var key = command == "status" ? "analysis" : "analysisContext";
        Assert.Equal(graph.Analysis.Id, root.GetProperty(key).GetProperty("id").GetString());
        Assert.Equal("partial", root.GetProperty("completeness").GetProperty("coverage").GetString());
        Assert.Equal("notChecked", root.GetProperty(key).GetProperty("freshness").GetString());
        Assert.False(root.GetProperty(key).GetProperty("dependencyFingerprint").GetProperty("complete").GetBoolean());
        Assert.Equal(114, root.GetProperty(key).GetProperty("totalProjects").GetInt32());
        Assert.Equal(2400, root.GetProperty("analysisDiagnostics").GetProperty("total").GetInt32());
        var field = Assert.Single(root.GetProperty("metadataSummary").GetProperty("fields").EnumerateArray(),
            f => f.GetProperty("path").GetString() == $"/{key}/dependencyFingerprint/limitations");
        var samples = root.GetProperty(key).GetProperty("dependencyFingerprint").GetProperty("limitations");
        Assert.Equal(114, field.GetProperty("total").GetInt32());
        Assert.Equal(114 - samples.GetArrayLength(), field.GetProperty("omitted").GetInt32());
        Assert.Equal(samples.GetArrayLength(), field.GetProperty("shortened").GetInt32());
        Assert.All(samples.EnumerateArray(), x => Assert.True(x.GetString()!.EnumerateRunes().Count() <= 300));
        Assert.Equal(graph.Analysis.DependencyFingerprint!.Limitations,
            full.RootElement.GetProperty(key).GetProperty("dependencyFingerprint").GetProperty("limitations").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(graph.Analysis.Limitations,
            full.RootElement.GetProperty("completeness").GetProperty("limitations").EnumerateArray().Select(x => x.GetString()));
        Assert.Empty(full.RootElement.GetProperty("metadataSummary").GetProperty("fields").EnumerateArray());
        if (command == "status") return;
        Assert.Equal(full.RootElement.GetProperty("items").GetRawText(), root.GetProperty("items").GetRawText());
        Assert.False(root.GetProperty("truncated").GetBoolean());
        Assert.Empty(root.GetProperty("limits").GetProperty("reasons").EnumerateArray());
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        if (command == "find") return;
        var step = Assert.Single(item.GetProperty("path").EnumerateArray());
        Assert.Equal(6, step.GetProperty("evidence").GetArrayLength());
        Assert.False(step.GetProperty("evidenceTruncated").GetBoolean());
        using var limited = AssertBoundedJson(await Run([.. args, "--evidence-limit", "2", "--db", Database]));
        Assert.Equal(2, limited.RootElement.GetProperty("items")[0].GetProperty("path")[0].GetProperty("evidence").GetArrayLength());
        Assert.True(limited.RootElement.GetProperty("items")[0].GetProperty("path")[0].GetProperty("evidenceTruncated").GetBoolean());
        Assert.Contains("evidenceLimit", limited.RootElement.GetProperty("limits").GetProperty("reasons").EnumerateArray().Select(x => x.GetString()));
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("unknown")]
    [InlineData("notChecked")]
    public void MetadataBudgetPreservesFreshnessAndDeduplicatesReasonsInFull(string state)
    {
        var graph = LargeMetadataFixture();
        var result = new GraphStore(Database).Query("callers", "ReferenceOnly", null, QueryOptions.Defaults("callers"));
        var analysis = graph.Analysis with
        {
            Freshness = state,
            FreshnessCheck = new(state, "contentSha256", 3, 99, 10, graph.Analysis.DependencyFingerprint!.Limitations, false)
        };
        result = result with { Envelope = result.Envelope with { AnalysisContext = analysis, Stale = state } };
        foreach (var details in new[] { "summary", "full" })
        {
            var output = ReadOutputWriter.Query(result, new(details));
            using var json = AssertBoundedJson((output.ExitCode, output.Text, ""), maxChars: details == "full" ? null : 12000);
            var root = json.RootElement;
            Assert.Empty(root.GetProperty("items").EnumerateArray());
            Assert.Equal(state, root.GetProperty("stale").GetString());
            Assert.Equal("partial", root.GetProperty("completeness").GetProperty("coverage").GetString());
            var context = root.GetProperty("analysisContext");
            Assert.Equal(state, context.GetProperty("freshness").GetString());
            var check = context.GetProperty("freshnessCheck");
            Assert.Equal(state, check.GetProperty("state").GetString());
            Assert.False(check.GetProperty("countsComplete").GetBoolean());
            Assert.Equal(99, check.GetProperty("bytesHashed").GetInt64());
            Assert.Empty(check.GetProperty("reasons").EnumerateArray());
            Assert.Equal("/analysisContext/dependencyFingerprint/limitations", check.GetProperty("reasonsReference").GetString());
        }
    }

    [Theory]
    [InlineData("symbol", "Overload", null, 7, "ambiguousSymbol")]
    [InlineData("symbol", "missing", null, 8, "symbolNotFound")]
    [InlineData("path", "A", "Isolated", 0, "boundedSearchIncomplete")]
    public async Task MetadataBudgetDoesNotHideDomainOutcomes(string command, string symbol, string? target, int exitCode, string outcome)
    {
        LargeMetadataFixture();
        var args = target is null ? new[] { "query", command, symbol } : ["query", command, symbol, target, "--depth", "0"];
        using var json = AssertBoundedJson(await Run([.. args, "--db", Database]), exitCode);
        Assert.Equal(outcome, json.RootElement.GetProperty("outcome").GetString());
        if (target is not null)
        {
            Assert.True(json.RootElement.GetProperty("truncated").GetBoolean());
            Assert.Contains("depth", json.RootElement.GetProperty("limits").GetProperty("reasons").EnumerateArray().Select(x => x.GetString()));
        }
    }
    [Fact]
    public async Task MetadataBudgetKeepsLongUnicodeSymbolIdentityAndSourceIntact()
    {
        var graph = LargeMetadataFixture();
        var name = "Namespace." + string.Concat(Enumerable.Repeat("長名😀ş", 20));
        var source = new SourceEvidence("src/" + string.Concat(Enumerable.Repeat("資料😀", 15)) + ".cs", 21, 3, 22, 4, "hash");
        graph = graph with { Entities = graph.Entities.Select(e => e.Id == "Isolated"
            ? e with { Name = name, QualifiedName = name, Sources = [source] } : e).ToArray() };
        new GraphStore(Database).Replace(graph);
        using var summary = AssertBoundedJson(await Run("query", "find", name, "--db", Database));
        using var full = AssertBoundedJson(await Run("query", "find", name, "--details", "full", "--db", Database), maxChars: null);
        var items = summary.RootElement.GetProperty("items");
        Assert.Equal(full.RootElement.GetProperty("items").GetRawText(), items.GetRawText());
        var entity = Assert.Single(items.EnumerateArray()).GetProperty("entity");
        Assert.Equal(name, entity.GetProperty("qualifiedName").GetString());
        Assert.Equal(source.File, entity.GetProperty("sources")[0].GetProperty("file").GetString());
        Assert.Equal(21, entity.GetProperty("sources")[0].GetProperty("startLine").GetInt32());
    }

    [Fact]
    public void MetadataBudgetDropsOptionalProjectsBeforeAnyItems()
    {
        BoundedOutputFixture();
        var result = new GraphStore(Database).Query("find", "Fixture.", null, QueryOptions.Defaults("find") with { Limit = 2 });
        // Build the same minimal scope without optional metadata to establish a
        // budget that fits both results, independently of metadata sample sizes.
        var minimalAnalysis = result.Envelope.AnalysisContext with { Projects = [], Diagnostics = [] };
        var minimal = ReadOutputWriter.Query(result with { Envelope = result.Envelope with { AnalysisContext = minimalAnalysis } });
        var output = ReadOutputWriter.Query(result, new(MaxChars: OutputJson.Characters(minimal.Text) + 100));
        using var json = AssertBoundedJson((output.ExitCode, output.Text, ""), maxChars: OutputJson.Characters(minimal.Text) + 100);
        var root = json.RootElement;
        Assert.Equal(2, root.GetProperty("items").GetArrayLength());
        Assert.Empty(root.GetProperty("analysisContext").GetProperty("projects").EnumerateArray());
        Assert.Equal(31, root.GetProperty("analysisContext").GetProperty("omittedProjects").GetInt32());
        Assert.DoesNotContain("maxChars", root.GetProperty("limits").GetProperty("reasons").EnumerateArray().Select(x => x.GetString()));
        using var full = JsonDocument.Parse(ReadOutputWriter.Query(result, new("full")).Text);
        Assert.Equal(full.RootElement.GetProperty("items").GetRawText(), root.GetProperty("items").GetRawText());
    }

}
