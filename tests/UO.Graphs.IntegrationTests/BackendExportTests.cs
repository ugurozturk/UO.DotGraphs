using System.Text;
using System.Text.Json;
using UO.Graphs.Core;
using UO.Graphs.Storage.Sqlite;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests
{
    private static DeliveryOptions BackendOptions => new(Depth: 4, Profile: "backend", Calls: "exclude-properties");

    private GraphSnapshot BackendFixture()
    {
        var original = QueryFixture(partial: true);
        var ids = new[] { "Endpoint", "Handler", "Call", "Message", "Consumer", "Behavior", "Http", "Remote", "Incoming", "Ignored", "Getter", "Setter", "get_Name" };
        var entities = ids.Select(id => new GraphEntity(id, "method", id, "Fixture." + id, "M:Fixture." + id,
            "project", Confidence.Exact, [new("fixture.cs", 1, 1, 1, 2, "hash")])
        {
            HttpEndpoint = id == "Endpoint" ? new("GET", "/start", null, "aspnet", false, []) : null,
            Message = id == "Message" ? new("mediatr", "T:Event", "Fixture", "Event", "fixture", []) : null,
            HttpContract = id == "Http" ? new("refit", "GET", "/remote", null, "M:Remote", "Refit", []) : null
        }).ToArray();
        RelationshipSymbol Symbol(string id) => new(id, id switch
        {
            "Getter" => "accessor:P:Fixture.Custom:PropertyGet",
            "Setter" => "accessor:P:Fixture.Custom:PropertySet",
            _ => "M:Fixture." + id
        }, "Fixture." + id, "Fixture", "project", id);
        var builder = new RelationshipBuilder(original.Analysis.Id);
        void Edge(string from, string? to, RelationshipKind kind, Confidence confidence = Confidence.Exact, int line = 1) =>
            builder.Add(kind, Symbol(from), to is null ? null : Symbol(to), "project", confidence,
                new("fixture.cs", line, 1, line, 2, "hash"), "Backend evidence " + kind);
        Edge("Endpoint", "Handler", RelationshipKind.HANDLED_BY);
        Edge("Handler", "Call", RelationshipKind.CALLS);
        Edge("Handler", "Call", RelationshipKind.CALLS, line: 2);
        Edge("Handler", "Call", RelationshipKind.CALLS, line: 3);
        Edge("Handler", "Getter", RelationshipKind.CALLS);
        Edge("Handler", "Setter", RelationshipKind.CALLS);
        Edge("Handler", "get_Name", RelationshipKind.CALLS); // A normal method with an accessor-like name.
        Edge("Handler", "Message", RelationshipKind.DISPATCHES);
        Edge("Handler", "Message", RelationshipKind.PUBLISHES);
        Edge("Message", "Consumer", RelationshipKind.POSSIBLE_TARGET, Confidence.Inferred);
        Edge("Message", "Behavior", RelationshipKind.POSSIBLE_BEHAVIOR, Confidence.Inferred);
        Edge("Message", null, RelationshipKind.POSSIBLE_TARGET, Confidence.Unresolved);
        Edge("Handler", "Http", RelationshipKind.INVOKES_HTTP);
        Edge("Http", "Remote", RelationshipKind.POSSIBLE_ENDPOINT, Confidence.Inferred);
        Edge("Http", null, RelationshipKind.POSSIBLE_ENDPOINT, Confidence.Unresolved);
        Edge("Call", "Handler", RelationshipKind.CALLS); // Cycle.
        Edge("Incoming", "Handler", RelationshipKind.CALLS);
        foreach (var kind in Enum.GetValues<RelationshipKind>().Except(DeliveryOptions.BackendEdgeTypes))
            Edge("Handler", "Ignored", kind);
        var graph = original with { Entities = [.. original.Entities, .. entities], Relationships = builder.Relationships, Occurrences = builder.Occurrences };
        new GraphStore(Database).Replace(graph);
        return graph;
    }

    [Fact]
    public void BackendTraversalPreservesFlowEvidenceAndUncertaintyWithoutIncomingOrStructuralEdges()
    {
        var source = BackendFixture();
        var store = new GraphStore(Database);
        var graph = store.ReadDelivery("Endpoint", BackendOptions);
        Assert.Equal(new[] { "Behavior", "Call", "Consumer", "Endpoint", "Handler", "Http", "Message", "Remote", "get_Name" },
            graph.Nodes.Select(n => n.Symbol.Id).Order(StringComparer.Ordinal));
        Assert.Equal(DeliveryOptions.BackendEdgeTypes.Order(), graph.Edges.Select(e => e.Relationship.Kind).Distinct().Order());
        Assert.Equal(2, graph.Edges.Count(e => e.Relationship.Target is null));
        Assert.Equal(3, graph.Edges.Count(e => e.Relationship.Confidence == Confidence.Inferred));
        Assert.All(graph.Edges, e =>
        {
            Assert.Contains(e.Relationship, source.Relationships);
            Assert.All(e.Evidence, o => Assert.Equal(
                JsonSerializer.Serialize(source.Occurrences.Single(original => original.Id == o.Id), GraphJson.Options),
                JsonSerializer.Serialize(o, GraphJson.Options)));
        });
        Assert.Equal(new[] { "evidenceLimit" }, graph.Reasons);
        Assert.Equal(graph.Edges.Length, graph.ExaminedEdges);
        var complete = store.ReadDelivery("Endpoint", BackendOptions with { EvidenceLimit = 3 });
        Assert.Empty(complete.Reasons);
        Assert.Equal(complete.Nodes.Length, complete.Nodes.Select(n => n.Symbol.Id).Distinct().Count());
        foreach (var (options, reason) in new[]
        {
            (BackendOptions with { MaxNodes = 1 }, "maxNodes"), (BackendOptions with { Limit = 1 }, "limit"),
            (BackendOptions with { MaxEdges = 1 }, "maxEdges"), (BackendOptions with { Depth = 0 }, "depth")
        })
        {
            var bounded = store.ReadDelivery("Endpoint", options);
            Assert.Contains(reason, bounded.Reasons);
            Assert.All(bounded.Edges.Where(e => e.Relationship.Target is not null), e =>
                Assert.Contains(bounded.Nodes, n => n.Symbol.Id == e.Relationship.Target!.Id));
        }
        Assert.Throws<GraphException>(() => store.ReadDelivery(null, BackendOptions));
    }

    [Fact]
    public void BackendPropertySelectionPrecedesEveryBudgetAndUsesFormalIdentity()
    {
        BackendFixture();
        var store = new GraphStore(Database);
        var properties = store.ReadDelivery("Handler", BackendOptions with { Calls = "only-properties", MaxNodes = 3, Limit = 2, MaxEdges = 2 });
        Assert.Empty(properties.Reasons);
        Assert.Equal(new[] { "Getter", "Setter" }, properties.Edges.Select(e => e.Relationship.Target!.Id).Order());
        Assert.Equal(2, properties.ExaminedEdges);
        var all = store.ReadDelivery("Handler", BackendOptions with { Calls = "all", Depth = 1 });
        var excluded = store.ReadDelivery("Handler", BackendOptions with { Depth = 1 });
        Assert.Equal(2, all.Edges.Length - excluded.Edges.Length);
        Assert.Contains(excluded.Edges, e => e.Relationship.Target?.Id == "get_Name");
        Assert.DoesNotContain(excluded.Nodes, n => n.Symbol.Id is "Getter" or "Setter");
    }

    [Fact]
    public async Task BackendFormatsDefaultsSelectionAndEscapingAreDeterministic()
    {
        var graph = BackendFixture();
        const string label = "Öğe 雪 🚗 \" ] --> injected[ <tag>& # % \\ \n\r\t\u0001";
        var store = new GraphStore(Database);
        graph = graph with { Entities = graph.Entities.Select(e => e.Id == "Endpoint" ? e with { QualifiedName = label } : e).ToArray() };
        store.Replace(graph);
        foreach (var format in new[] { "json", "ndjson", "mermaid", "dot" })
        {
            var args = new[] { "export", "--profile", "backend", "--symbol", "Endpoint", "--format", format, "--db", Database };
            var result = await Run(args);
            Assert.True(result.Code == 0, result.Error);
            Assert.Equal(result.Output, (await Run(args)).Output);
            using var metadata = JsonDocument.Parse(format == "json" ? result.Output : result.Output.Split('\n')[0][(format == "ndjson" ? 0 : 3)..]);
            var meta = metadata.RootElement.GetProperty(format == "json" ? "metadata" : "data");
            Assert.Equal("backend", meta.GetProperty("selection").GetProperty("profile").GetString());
            Assert.Equal("exclude-properties", meta.GetProperty("selection").GetProperty("calls").GetString());
            var limits = meta.GetProperty("limits");
            foreach (var (key, value) in new[] { ("maxNodes", 40), ("limit", 80), ("depth", 4), ("maxEdges", 1000), ("evidenceLimit", 2) })
                Assert.Equal(value, limits.GetProperty(key).GetInt32());
            if (format == "mermaid")
            {
                var drawing = result.Output[(result.Output.IndexOf("flowchart LR", StringComparison.Ordinal))..];
                Assert.Contains(" -.->|\"POSSIBLE_TARGET #47; inferred\"|", drawing);
                Assert.Contains(" -->|\"HANDLED_BY #47; exact\"|", drawing);
                Assert.Contains("([\"Öğe 雪", drawing);
                Assert.Contains("{{\"Fixture.Message", drawing);
                Assert.Contains("classDef root", drawing);
                Assert.Contains("unresolved#58; 1 collected", drawing);
                Assert.Contains("Freshness#58; notChecked", drawing);
                Assert.Contains("coverage#58; partial", drawing);
                Assert.Contains("Truncated#58; evidenceLimit", drawing);
                Assert.Contains("#34; #93;", drawing);
                Assert.DoesNotContain("--> injected[", drawing);
                Assert.DoesNotContain('\uFFFD', drawing);
            }
            store.Replace(graph with { Entities = graph.Entities.Reverse().ToArray(), Relationships = graph.Relationships.Reverse().ToArray(), Occurrences = graph.Occurrences.Reverse().ToArray() });
            Assert.Equal(result.Output, (await Run(args)).Output);
        }
        // Replace above tests insertion ordering; the read-only byte invariant is verified separately below.
        var unchanged = File.ReadAllBytes(Database);
        foreach (var (symbol, exit) in new[] { ("Overload", 7), ("Missing", 8) })
        foreach (var format in new[] { "json", "ndjson", "mermaid", "dot" })
        {
            var output = Path.Combine(directory, symbol + "." + format);
            var result = await Run("export", "--profile", "backend", "--symbol", symbol, "--limit", "1", "--format", format, "--output", output, "--db", Database);
            Assert.Equal(exit, result.Code);
            Assert.Contains(exit == 7 ? "selectionRequired" : "symbolNotFound", result.Output);
            Assert.False(File.Exists(output));
        }
        Assert.Equal(unchanged, File.ReadAllBytes(Database));
    }

    [Theory]
    [InlineData("graph", "json")]
    [InlineData("graph", "ndjson")]
    [InlineData("graph", "mermaid")]
    [InlineData("graph", "dot")]
    [InlineData("backend", "json")]
    [InlineData("backend", "ndjson")]
    [InlineData("backend", "mermaid")]
    [InlineData("backend", "dot")]
    public async Task ExportFileMatchesStdoutAndNeverOverwrites(string profile, string format)
    {
        BackendFixture();
        var before = File.ReadAllBytes(Database);
        var args = new[] { "export", "--profile", profile, "--symbol", "Endpoint", "--format", format, "--db", Database };
        var stdout = await Run(args);
        Assert.Equal(0, stdout.Code);
        var path = Path.Combine(directory, "flow." + format);
        var file = await Run([.. args, "--output", path]);
        Assert.True(file.Code == 0, file.Error);
        Assert.Empty(file.Output);
        Assert.Contains("Exported " + format, file.Error);
        Assert.Equal(Encoding.UTF8.GetBytes(stdout.Output), File.ReadAllBytes(path));
        Assert.Equal(6, (await Run([.. args, "--output", path])).Code);
        Assert.Equal(stdout.Output, File.ReadAllText(path));
        foreach (var target in new[] { path, Path.Combine(directory, "absent-target") })
        {
            var link = Path.Combine(directory, Guid.NewGuid() + ".link");
            File.CreateSymbolicLink(link, target);
            Assert.Equal(6, (await Run([.. args, "--output", link])).Code);
            Assert.Equal(target, new FileInfo(link).LinkTarget);
            if (target != path) Assert.False(File.Exists(target));
        }
        foreach (var invalid in new[] { directory, Path.Combine(directory, "absent", "flow"), Path.Combine(path, "flow"), Path.Combine(directory, new string('x', 300)) })
        {
            var failed = await Run([.. args, "--output", invalid]);
            Assert.Equal(6, failed.Code);
            Assert.Empty(failed.Output);
        }
        Assert.Empty(Directory.GetFiles(directory, ".uographs-export-*"));
        Assert.Equal(before, File.ReadAllBytes(Database));
        var stalePath = Path.Combine(directory, "stale-output");
        Assert.Equal(9, (await Run([.. args, "--require-fresh", "--output", stalePath])).Code);
        Assert.False(File.Exists(stalePath));
        ExecuteSql("UPDATE analysis SET json = '{broken'");
        var badPath = Path.Combine(directory, "bad-output");
        Assert.Equal(6, (await Run([.. args, "--output", badPath])).Code);
        Assert.False(File.Exists(badPath));
    }

    [Fact]
    public async Task ExportOutputReportsWritePermissionFailureWithoutCreatingDestination()
    {
        if (OperatingSystem.IsWindows()) return; // Unix directory permissions; other output failures run on every platform.
        BackendFixture();
        var denied = Path.Combine(directory, "read-only");
        Directory.CreateDirectory(denied);
        File.SetUnixFileMode(denied, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var output = Path.Combine(denied, "flow.mmd");
            var result = await Run("export", "--profile", "backend", "--symbol", "Endpoint", "--output", output, "--db", Database);
            Assert.Equal(6, result.Code);
            Assert.Empty(result.Output);
            Assert.False(File.Exists(output));
            Assert.Empty(Directory.GetFiles(denied));
        }
        finally { File.SetUnixFileMode(denied, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
    }

    [Theory]
    [InlineData("export", "--profile", "backend")]
    [InlineData("export", "--profile", "other")]
    [InlineData("export", "--calls", "all")]
    [InlineData("export", "--profile", "backend", "--symbol", "A", "--calls", "invalid")]
    [InlineData("export", "--profile", "backend", "--symbol", "A", "--view", "compact")]
    [InlineData("export", "--profile", "backend", "--symbol", "A", "--after", "cursor")]
    [InlineData("context", "A", "--profile", "backend")]
    [InlineData("context", "A", "--output", "output.json")]
    [InlineData("query", "symbol", "A", "--profile", "graph")]
    public async Task BackendOptionsRejectInvalidCombinationsBeforeReading(params string[] args)
    {
        var result = await Run([.. args, "--db", Database]);
        Assert.Equal(2, result.Code);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(Database));
    }

    [Fact]
    public async Task BackendRealRoslynPropertyFiltersAlsoExcludeCustomAccessorBodies()
    {
        var graph = await CompactFixture();
        var root = graph.Entities.Single(e => e.DocumentationId == "M:AiWorkflow.Workflow.Run(AiWorkflow.IGuard)");
        var store = new GraphStore(Database);
        var excluded = store.ReadDelivery(root.Id, BackendOptions with { Depth = 1 });
        Assert.Equal(7, excluded.Edges.Length);
        var properties = store.ReadDelivery(root.Id, BackendOptions with { Calls = "only-properties", Depth = 1, MaxNodes = 100, Limit = 100 });
        Assert.Equal(62, properties.Edges.Length);
        Assert.Contains(properties.Edges, e => e.Relationship.Target!.DeclarationId.Contains("Custom", StringComparison.Ordinal));
        Assert.DoesNotContain(excluded.Edges, e => e.Relationship.Target!.DeclarationId.StartsWith("accessor:", StringComparison.Ordinal));
    }
}
