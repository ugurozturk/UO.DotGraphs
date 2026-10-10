using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UO.Graphs.Core;
using UO.Graphs.Storage.Sqlite;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests
{
    private async Task<GraphSnapshot> CompactFixture()
    {
        var fixture = CopyFixture("compact-workflow");
        File.Copy(Path.Combine(Root, "tests/Fixtures/AiWorkflow.cs.txt"), Path.Combine(fixture, "Library/AiWorkflow.cs"));
        return await IndexPath(Path.Combine(fixture, "Library/Library.csproj"), "--framework", "net10.0");
    }

    [Fact]
    public async Task CompactWorkflowKeepsAllSevenCriticalCallsAtTwelveThousandAndPagesProperties()
    {
        var graph = await CompactFixture();
        var run = graph.Entities.Single(e => e.DocumentationId == "M:AiWorkflow.Workflow.Run(AiWorkflow.IGuard)");
        var args = new[] { "query", "callees", run.Id, "--view", "compact", "--calls", "exclude-properties", "--evidence-limit", "2", "--db", Database };
        var result = await Run(args);
        using var json = AssertBoundedJson(result);
        var root = json.RootElement;
        var relationships = root.GetProperty("relationships").EnumerateArray().ToArray();
        Assert.Equal(7, relationships.Length);
        var names = relationships.Select(r => root.GetProperty("symbols")[r.GetProperty("target").GetInt32()].GetProperty("displayName").GetString()).ToArray();
        foreach (var critical in new[] { "CheckEnabledAsync", "EnsureCanChange", "Validate", "InsertAsync", "RecalculateTotalsAsync", "ApplyPostApprovalFinancialChangeAsync", "AddChangeLogAsync" })
            Assert.Contains(names, n => n!.Contains(critical, StringComparison.Ordinal));
        Assert.Equal(62, root.GetProperty("selection").GetProperty("filtered").GetInt32());
        Assert.Equal(0, root.GetProperty("selection").GetProperty("remaining").GetInt32());
        Assert.All(relationships, r => Assert.Equal("exact", r.GetProperty("confidence").GetString()));
        Assert.Contains(root.GetProperty("texts").EnumerateArray(), t => t.GetString()!.Contains("runtime dispatch not resolved", StringComparison.Ordinal));
        VerifyCompactEvidence(root, graph);
        Assert.Equal(result.Output, (await Run(args)).Output);
        Assert.DoesNotContain("canonicalIdentity", root.GetProperty("symbols").GetRawText());
        using var standard = AssertBoundedJson(await Run("query", "callees", run.Id, "--limit", "100", "--evidence-limit", "2", "--db", Database));
        Assert.True(standard.RootElement.GetProperty("items").GetArrayLength() < 7);
        Assert.Equal(6, standard.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.False(standard.RootElement.TryGetProperty("selection", out _));
        var seen = new HashSet<string>();
        string? after = null;
        do
        {
            var pageArgs = new List<string> { "query", "callees", run.Id, "--view", "compact", "--calls", "only-properties", "--limit", "9", "--max-chars", "6500", "--db", Database };
            if (after is not null) pageArgs.AddRange(["--after", after, "--snapshot", graph.Analysis.Id]);
            using var page = AssertBoundedJson(await Run(pageArgs.ToArray()), maxChars: 6500);
            Assert.Equal("excludedByPropertyFilter", page.RootElement.GetProperty("selection").GetProperty("unresolved").GetString());
            VerifyCompactEvidence(page.RootElement, graph);
            foreach (var edge in page.RootElement.GetProperty("relationships").EnumerateArray())
                Assert.True(seen.Add(edge.GetProperty("id").GetString()!), "Continuation repeated an edge");
            var continuation = page.RootElement.GetProperty("selection").GetProperty("continuation");
            after = continuation.ValueKind == JsonValueKind.Null ? null : continuation.GetProperty("after").GetString();
            if (after is not null)
            {
                Assert.Equal(2, (await Run([.. args, "--after", after, "--snapshot", "wrong"])).Code);
                // A valid snapshot with a changed call filter must also reject the cursor.
                Assert.Equal(2, (await Run([.. args, "--after", after, "--snapshot", graph.Analysis.Id])).Code);
            }
        } while (after is not null);
        Assert.Equal(62, seen.Count);
    }

    [Fact]
    public async Task CompactPreservesOverloadsGenericBindingsSameNamesAndUnresolved()
    {
        var graph = await CompactFixture();
        var bindings = graph.Entities.Single(e => e.DocumentationId == "M:AiWorkflow.Workflow.Bindings");
        using var json = AssertBoundedJson(await Run("query", "callees", bindings.Id, "--view", "compact", "--db", Database));
        var root = json.RootElement;
        var expected = graph.Relationships.Where(r => r.Source.Id == bindings.Id && r.Kind == RelationshipKind.CALLS).ToArray();
        Assert.Equal(expected.Select(r => r.Id).Order(), root.GetProperty("relationships").EnumerateArray().Select(r => r.GetProperty("id").GetString()).Order());
        VerifyCompactEvidence(root, graph);
        var unknown = graph.Entities.Single(e => e.Name == "Unknown" && e.QualifiedName.Contains("AiWorkflow", StringComparison.Ordinal));
        using var unresolved = AssertBoundedJson(await Run("query", "callees", unknown.Id, "--view", "compact", "--db", Database));
        var edge = Assert.Single(unresolved.RootElement.GetProperty("relationships").EnumerateArray());
        Assert.Equal("unresolved", edge.GetProperty("confidence").GetString());
        Assert.Equal(JsonValueKind.Null, edge.GetProperty("target").ValueKind);
        Assert.Equal(JsonValueKind.Null, unresolved.RootElement.GetProperty("items")[0].GetProperty("symbol").ValueKind);
        VerifyCompactEvidence(unresolved.RootElement, graph);
    }

    private static void VerifyCompactEvidence(JsonElement root, GraphSnapshot graph)
    {
        foreach (var edge in root.GetProperty("relationships").EnumerateArray())
        {
            var originalEdge = graph.Relationships.Single(r => r.Id == edge.GetProperty("id").GetString());
            Assert.Equal(originalEdge.Source.Id, root.GetProperty("symbols")[edge.GetProperty("source").GetInt32()].GetProperty("id").GetString());
            if (originalEdge.Target is { } target)
                Assert.Equal(target.Id, root.GetProperty("symbols")[edge.GetProperty("target").GetInt32()].GetProperty("id").GetString());
            foreach (var reference in edge.GetProperty("evidence").EnumerateArray())
            {
                var site = root.GetProperty("evidence")[reference.GetInt32()];
                var original = graph.Occurrences.Single(o => o.Id == site.GetProperty("id").GetString());
                Assert.Equal(originalEdge.Id, original.RelationshipId);
                Assert.Equal(original.Reason, root.GetProperty("texts")[site.GetProperty("reason").GetInt32()].GetString());
                Assert.Equal(original.RelatedSymbols.Select(s => s.Id), site.GetProperty("relatedSymbols").EnumerateArray()
                    .Select(s => root.GetProperty("symbols")[s.GetInt32()].GetProperty("id").GetString()));
                if (original.BoundTarget is not null)
                    Assert.Equal(original.BoundTarget, root.GetProperty("texts")[site.GetProperty("boundTarget").GetInt32()].GetString());
                var file = root.GetProperty("files")[site.GetProperty("source").GetProperty("file").GetInt32()];
                Assert.Equal(original.Source.File, file.GetProperty("path").GetString());
                Assert.Equal(original.Source.Sha256, file.GetProperty("sha256").GetString());
                Assert.Equal(new[] { original.Source.StartLine, original.Source.StartColumn, original.Source.EndLine, original.Source.EndColumn },
                    site.GetProperty("source").GetProperty("range").EnumerateArray().Select(n => n.GetInt32()));
            }
        }
    }

    [Fact]
    public async Task CompactMediatorKeepsRequestDispatchHandlerAndInferredConfidence()
    {
        var graph = await IndexPath(Path.Combine(Root, "samples/Phase8/Uo/Uo.csproj"));
        var request = graph.Relationships.First(r => r.Kind == RelationshipKind.DISPATCHES && r.Source.DisplayName == "UoFlows.Service.Run()").Target!;
        using var json = AssertBoundedJson(await Run("query", "dependencies", request.Id, "--view", "compact",
            "--direction", "incoming", "--edge-types", "HANDLES,DISPATCHES", "--db", Database));
        var root = json.RootElement;
        Assert.Equal(new[] { "DISPATCHES", "HANDLES" }, root.GetProperty("relationships").EnumerateArray()
            .Select(e => e.GetProperty("kind").GetString()).Order());
        Assert.All(root.GetProperty("relationships").EnumerateArray(), e => Assert.Equal(request.Id,
            root.GetProperty("symbols")[e.GetProperty("target").GetInt32()].GetProperty("id").GetString()));
        VerifyCompactEvidence(root, graph);
        using var inferred = AssertBoundedJson(await Run("query", "dependencies", request.Id, "--view", "compact",
            "--edge-types", "POSSIBLE_TARGET", "--include-inferred", "--db", Database));
        Assert.Contains(inferred.RootElement.GetProperty("relationships").EnumerateArray(), e => e.GetProperty("confidence").GetString() == "inferred");
        Assert.Contains(inferred.RootElement.GetProperty("relationships").EnumerateArray(), e => e.GetProperty("confidence").GetString() == "unresolved");
        Assert.NotEmpty(inferred.RootElement.GetProperty("relationships").EnumerateArray());
        VerifyCompactEvidence(inferred.RootElement, graph);
    }

    [Fact]
    public async Task CompactSourceRequestProtectsMatchedSnippetBeforeOptionalNeighbors()
    {
        var graph = QueryFixture(fanout: 50);
        var sourceRoot = Path.Combine(OperatingSystem.IsMacOS() ? "/private/tmp" : Path.GetTempPath(), "uographs-compact-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceRoot);
        try
        {
            var file = Path.Combine(sourceRoot, "Code.cs");
            const string text = "// Türkçe 😀\nclass Example { void Workflow() { } }\n";
            await File.WriteAllTextAsync(file, text);
            var source = new SourceEvidence(file, 1, 1, 2, 43, GraphIdentity.Hash(text));
            graph = graph with { Analysis = graph.Analysis with { RootPath = sourceRoot },
                Entities = graph.Entities.Select(e => e.Id == "Isolated" ? e with { Sources = [source] } : e).ToArray() };
            new GraphStore(Database).Replace(graph);
            var result = await Run("context", "Isolated", "--include-source", "--max-chars", "6000", "--db", Database);
            using var json = AssertBoundedJson(result, maxChars: 6000);
            var snippet = Assert.Single(json.RootElement.GetProperty("snippets").EnumerateArray());
            Assert.Equal("snapshotMatched", snippet.GetProperty("status").GetString());
            Assert.Contains("Workflow", snippet.GetProperty("text").GetString());
            Assert.True(json.RootElement.GetProperty("truncated").GetBoolean());
            Assert.Equal("notChecked", json.RootElement.GetProperty("stale").GetString());
        }
        finally { Directory.Delete(sourceRoot, recursive: true); }
    }

    [Fact]
    public async Task CompactBudgetAmbiguityEmptyEvidenceTruncationAndCorruptionAreExplicit()
    {
        var graph = QueryFixture(partial: true);
        graph = graph with { Analysis = graph.Analysis with { Limitations = ["Türkçe 😀 \" \\ \n"] } };
        new GraphStore(Database).Replace(graph);
        using var ambiguity = AssertBoundedJson(await Run("query", "symbol", "Overload", "--view", "compact", "--limit", "1", "--db", Database), 7);
        Assert.Equal("ambiguousSymbol", ambiguity.RootElement.GetProperty("outcome").GetString());
        using var empty = AssertBoundedJson(await Run("query", "callees", "Isolated", "--view", "compact", "--db", Database));
        Assert.Empty(empty.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("partial", empty.RootElement.GetProperty("completeness").GetProperty("coverage").GetString());
        var args = new[] { "query", "callees", "B", "--view", "compact", "--evidence-limit", "1", "--db", Database };
        using var limited = AssertBoundedJson(await Run(args));
        var edge = Assert.Single(limited.RootElement.GetProperty("relationships").EnumerateArray());
        Assert.Equal(3, edge.GetProperty("evidenceTotal").GetInt32());
        Assert.True(edge.GetProperty("evidenceTruncated").GetBoolean());
        Assert.Contains("evidenceLimit", limited.RootElement.GetProperty("limits").GetProperty("reasons").EnumerateArray().Select(n => n.GetString()));
        var failure = await Run([.. args, "--max-chars", "1"]);
        Assert.Equal(2, failure.Code); Assert.Empty(failure.Output);
        var required = int.Parse(Regex.Matches(failure.Error, @"\b\d+\b")[^1].Value, CultureInfo.InvariantCulture);
        using var exact = AssertBoundedJson(await Run([.. args, "--max-chars", required.ToString(CultureInfo.InvariantCulture)]), maxChars: required);
        Assert.Equal(required, exact.RootElement.GetProperty("budget").GetProperty("actualChars").GetInt32());
        Assert.Equal(2, (await Run([.. args, "--max-chars", (required - 1).ToString(CultureInfo.InvariantCulture)])).Code);
        ExecuteSql("DELETE FROM occurrences WHERE relationship_id IN (SELECT id FROM relationships WHERE source_id = 'B');");
        var corrupt = await Run(args);
        Assert.Equal(6, corrupt.Code); Assert.Empty(corrupt.Output);
        new GraphStore(Database).Replace(graph);
        ExecuteSql("UPDATE relationships SET json = json_set(json, '$.source.id', 'other') WHERE source_id = 'B';");
        var inconsistent = await Run(args);
        Assert.Equal(6, inconsistent.Code); Assert.Empty(inconsistent.Output);
    }

    [Theory]
    [InlineData("status", "--view", "compact")]
    [InlineData("query", "callees", "A", "--calls", "exclude-properties")]
    [InlineData("query", "callees", "A", "--view", "compact", "--calls", "invalid")]
    [InlineData("query", "callees", "A", "--view", "compact", "--after", "x")]
    [InlineData("query", "callees", "A", "--view", "compact", "--calls", "all", "--depth", "2")]
    public async Task CompactInvalidOptionsFailBeforeReadingDatabase(params string[] args)
    {
        var result = await Run([.. args, "--db", Database]);
        Assert.Equal(2, result.Code); Assert.Empty(result.Output); Assert.False(File.Exists(Database));
    }
}
