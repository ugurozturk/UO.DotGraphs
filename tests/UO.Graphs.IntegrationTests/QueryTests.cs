using System.Text.Json;
using Microsoft.Data.Sqlite;
using UO.Graphs.Core;
using UO.Graphs.Storage.Sqlite;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests
{
    private GraphSnapshot QueryFixture(bool partial = false, int fanout = 0)
    {
        var analysis = new AnalysisSnapshot("query-fixture", DateTimeOffset.UnixEpoch, "/fixture.csproj", "0.4.0", "5.0.0",
            partial ? "partial" : "completeWithinDeclaredScope", "notChecked", ["Static fixture scope"],
            partial ? ["Missing reference"] : [], []);
        var ids = new[] { "A", "B", "C", "D", "Isolated", "ReferenceOnly", "Container", "Inferred", "Overload1", "Overload2" }
            .Concat(Enumerable.Range(0, fanout).Select(i => $"Wide{i:D5}")).ToArray();
        var entities = ids.Select(id => new GraphEntity(id, "method", id.StartsWith("Overload", StringComparison.Ordinal) ? "Overload" : id,
            "Fixture." + id, "M:Fixture." + id, "project", Confidence.Exact, [new("fixture.cs", 1, 1, 1, 2, "hash")])).ToArray();
        RelationshipSymbol Symbol(string id) => new(id, "M:Fixture." + id, "Fixture." + id, "Fixture", "project", id);
        var builder = new RelationshipBuilder(analysis.Id);
        void Edge(string from, string to, RelationshipKind kind = RelationshipKind.CALLS, Confidence confidence = Confidence.Exact, int line = 1) =>
            builder.Add(kind, Symbol(from), Symbol(to), "project", confidence, new("fixture.cs", line, 1, line, 2, "hash"), "Fixture bound " + kind);
        Edge("A", "B"); Edge("A", "C"); Edge("B", "D"); Edge("C", "D");
        Edge("D", "A"); Edge("A", "A");
        Edge("ReferenceOnly", "D", RelationshipKind.REFERENCES);
        Edge("Container", "D", RelationshipKind.DECLARES);
        Edge("Inferred", "D", confidence: Confidence.Inferred);
        Edge("B", "D", line: 2); Edge("B", "D", line: 3);
        foreach (var id in ids.Where(id => id.StartsWith("Wide", StringComparison.Ordinal))) Edge("Isolated", id);
        var graph = new GraphSnapshot(analysis, entities) { Relationships = builder.Relationships, Occurrences = builder.Occurrences };
        new GraphStore(Database).Replace(graph);
        return graph;
    }

    private QueryEnvelope Query(string command, string id, int depth = 5, string? to = null,
        TraversalDirection? direction = null, int limit = 100, bool inferred = false, int maxEdges = 10000, int evidenceLimit = 20) =>
        new GraphStore(Database).Query(command, id, to, QueryOptions.Defaults(command) with
        { Depth = depth, Direction = direction ?? QueryOptions.Defaults(command).Direction, Limit = limit,
            IncludeInferred = inferred, MaxEdges = maxEdges, EvidenceLimit = evidenceLimit }).Envelope;

    private static string[] Ids(QueryEnvelope envelope) => envelope.Items.Select(i => i.Symbol.Id).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public void TraversalHandlesDirectionCycleSelfCallAndDiamondWithExactEvidence()
    {
        var graph = QueryFixture();
        Assert.Equal(new[] { "A", "B", "C" }, Ids(Query("callees", "A", depth: 1)));
        Assert.Equal(new[] { "A", "D" }, Ids(Query("callers", "A", depth: 1)));
        Assert.Equal(new[] { "A", "D" }, Ids(Query("callees", "A", depth: 1, direction: TraversalDirection.Incoming)));
        Assert.Equal(new[] { "A", "B", "C", "D" }, Ids(Query("callees", "A", depth: 100)));
        Assert.False(Query("callees", "A", depth: 100).Truncated);
        var both = Query("dependencies", "A", depth: 1, direction: TraversalDirection.Both);
        Assert.Equal(new[] { "A", "B", "C", "D" }, Ids(both));
        Assert.True(Assert.Single(both.Items, i => i.Symbol.Id == "D").Path[0].Reversed);
        var diamond = Query("callees", "A", depth: 2);
        var d = Assert.Single(diamond.Items, i => i.Symbol.Id == "D");
        Assert.Equal(2, d.Distance);
        Assert.Equal("A", d.Path[0].FromId);
        Assert.Equal(d.Path[0].ToId, d.Path[1].FromId);
        Assert.Equal("D", d.Path[1].ToId);
        Assert.All(diamond.Items.SelectMany(i => i.Path), step =>
        {
            Assert.Contains(step.Relationship, graph.Relationships);
            Assert.All(step.Evidence, site => Assert.Contains(graph.Occurrences, original => original.Id == site.Id && original.Source == site.Source && original.Reason == site.Reason));
        });
    }

    [Fact]
    public void PathIsBoundedShortestAndDeterministicNotAllPaths()
    {
        QueryFixture();
        var found = Query("path", "A", to: "D");
        Assert.Equal("found", found.Outcome);
        Assert.Equal(2, Assert.Single(found.Items).Path.Length);
        Assert.Equal(JsonSerializer.Serialize(found, GraphJson.Options), JsonSerializer.Serialize(Query("path", "A", to: "D"), GraphJson.Options));
        Assert.Single(Query("path", "D", to: "A").Items[0].Path);
        Assert.Single(Query("path", "A", to: "D", direction: TraversalDirection.Incoming).Items[0].Path);
        Assert.Empty(Query("path", "A", to: "A", depth: 0).Items[0].Path);
        var bounded = Query("path", "A", to: "D", depth: 1);
        Assert.Empty(bounded.Items);
        Assert.True(bounded.Truncated);
        Assert.Contains("depth", bounded.Limits.Reasons);
        Assert.Equal("boundedSearchIncomplete", bounded.Outcome);
        var unreachable = Query("path", "A", to: "Isolated");
        Assert.Empty(unreachable.Items);
        Assert.False(unreachable.Truncated);
        Assert.Equal("notFound", unreachable.Outcome);
    }

    [Fact]
    public void ImpactUsesReverseDependenciesAndProvidesOneReasonPathPerNode()
    {
        QueryFixture();
        var impact = Query("impact", "D");
        Assert.Equal(new[] { "A", "B", "C" }, Ids(impact));
        Assert.Contains(impact.Diagnostics, d => d.Code == "potentialImpact");
        Assert.All(impact.Items, item =>
        {
            Assert.NotEmpty(item.Path);
            Assert.Equal("D", item.Path[0].FromId);
            Assert.Equal(item.Symbol.Id, item.Path[^1].ToId);
            Assert.All(item.Path, step =>
            {
                Assert.True(step.Reversed);
                Assert.Equal(RelationshipKind.CALLS, step.Relationship.Kind);
                Assert.NotEmpty(step.Evidence);
            });
        });
        Assert.Equal(new[] { "A", "B", "C", "Inferred" }, Ids(Query("impact", "D", inferred: true)));
        Assert.Equal(Confidence.Inferred, Assert.Single(Query("impact", "D", inferred: true).Items, i => i.Symbol.Id == "Inferred").Path[0].Relationship.Confidence);
        Assert.Throws<GraphException>(() => Query("impact", "D", direction: TraversalDirection.Outgoing));
        Assert.Throws<GraphException>(() => new GraphStore(Database).Query("impact", "D", options: QueryOptions.Defaults("impact") with { EdgeTypes = [RelationshipKind.REFERENCES] }));
        Assert.Equal(new[] { "ReferenceOnly" }, Ids(new GraphStore(Database).Query("dependencies", "D", options:
            QueryOptions.Defaults("dependencies") with { Direction = TraversalDirection.Incoming, EdgeTypes = [RelationshipKind.REFERENCES] }).Envelope));
    }

    [Fact]
    public void ResultDepthWorkAndEvidenceLimitsRemainVisibleAndDistinct()
    {
        QueryFixture(fanout: 250);
        var wide = Query("callees", "Isolated", limit: 10);
        Assert.Equal(10, wide.Items.Length);
        Assert.Contains("limit", wide.Limits.Reasons);
        Assert.Equal(10, wide.Limits.Limit);
        Assert.Equal(11, wide.Limits.ExaminedEdges);
        Assert.False(Query("callees", "Isolated", limit: 250).Truncated);
        var work = Query("path", "Isolated", to: "D", maxEdges: 2);
        Assert.Equal(2, work.Limits.ExaminedEdges);
        Assert.Contains("maxEdges", work.Limits.Reasons);
        Assert.Equal("boundedSearchIncomplete", work.Outcome);
        Assert.Contains("depth", Query("callees", "A", depth: 0).Limits.Reasons);
        var evidence = Query("path", "B", to: "D", evidenceLimit: 1);
        var step = Assert.Single(Assert.Single(evidence.Items).Path);
        Assert.True(step.EvidenceTruncated);
        Assert.Single(step.Evidence);
        Assert.Contains("evidenceLimit", evidence.Limits.Reasons);
        Assert.Equal("found", evidence.Outcome);
        Assert.Equal(3, Query("path", "B", to: "D").Items[0].Path[0].Evidence.Length);
    }

    [Fact]
    public async Task AllCommandsEmitVersionedParseableJsonAndAmbiguityNeverSelectsFirst()
    {
        QueryFixture(partial: true);
        var before = await File.ReadAllBytesAsync(Database);
        foreach (var args in new[] { new[] { "find", "A" }, ["symbol", "A"], ["callers", "A"], ["callees", "A"],
            ["dependencies", "A"], ["path", "A", "D"], ["impact", "D"] })
        {
            var result = await Run(new[] { "query" }.Concat(args).Concat(["--db", Database]).ToArray());
            Assert.Equal(0, result.Code);
            Assert.Empty(result.Error);
            using var json = JsonDocument.Parse(result.Output);
            var root = json.RootElement;
            Assert.Equal(5, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("query-fixture", root.GetProperty("snapshotId").GetString());
            Assert.Equal("partial", root.GetProperty("completeness").GetProperty("coverage").GetString());
            Assert.Equal("notChecked", root.GetProperty("stale").GetString());
            Assert.Equal(JsonValueKind.Array, root.GetProperty("items").ValueKind);
            Assert.Equal(JsonValueKind.Array, root.GetProperty("diagnostics").ValueKind);
            Assert.True(root.TryGetProperty("analysisContext", out _));
            Assert.True(root.TryGetProperty("truncated", out _));
        }
        var ambiguous = await Run("query", "callers", "Overload", "--limit", "1", "--db", Database);
        Assert.Equal(7, ambiguous.Code);
        using var candidates = JsonDocument.Parse(ambiguous.Output);
        Assert.Equal("ambiguousSymbol", candidates.RootElement.GetProperty("outcome").GetString());
        Assert.Single(candidates.RootElement.GetProperty("items").EnumerateArray());
        Assert.True(candidates.RootElement.GetProperty("truncated").GetBoolean());
        Assert.Equal(2, new GraphStore(Database).Query("symbol", "Overload").Envelope.Items.Length);
        Assert.Equal(0, (await Run("query", "symbol", "Overload1", "--db", Database)).Code);
        var missing = await Run("query", "symbol", "Absent", "--db", Database);
        Assert.Equal(8, missing.Code);
        Assert.Equal("symbolNotFound", JsonDocument.Parse(missing.Output).RootElement.GetProperty("outcome").GetString());
        Assert.Equal(before, await File.ReadAllBytesAsync(Database));
    }

    [Theory]
    [InlineData("--depth", "101")]
    [InlineData("--limit", "0")]
    [InlineData("--max-edges", "0")]
    [InlineData("--evidence-limit", "0")]
    [InlineData("--direction", "sideways")]
    [InlineData("--edge-types", "CALLS,INVALID")]
    [InlineData("--edge-types", "7")]
    public async Task QueryRejectsInvalidOptions(string option, string value)
    {
        var result = await Run("query", "callers", "A", option, value, "--db", Database);
        Assert.Equal(2, result.Code);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(Database));
    }

    [Fact]
    public async Task InputsAreLiteralAndNeverExecutedAsShellOrSql()
    {
        QueryFixture();
        var marker = Path.Combine(directory, "should-not-exist");
        foreach (var text in new[] { "' OR 1=1 --", "%_", "$(touch " + marker + ")", "; touch " + marker, "--literal" })
        {
            var result = await Run("query", "--db", Database, "find", "--", text);
            Assert.Equal(0, result.Code);
            using var json = JsonDocument.Parse(result.Output);
            Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
            Assert.False(File.Exists(marker));
        }
        Assert.Single(new GraphStore(Database).Query("symbol", "Fixture.A").Envelope.Items);
    }

    [Fact]
    public async Task CorruptDatabaseAndCorruptStoredJsonFailWithoutMutation()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Database, "not a SQLite database");
        var bad = await Run("query", "find", "A", "--db", Database);
        Assert.Equal(6, bad.Code);
        Assert.Empty(bad.Output);
        Assert.Equal("not a SQLite database", await File.ReadAllTextAsync(Database));
        File.Delete(Database);
        QueryFixture();
        ExecuteSql("UPDATE analysis SET json = '{broken'");
        var before = await File.ReadAllBytesAsync(Database);
        var malformed = await Run("query", "find", "A", "--db", Database);
        Assert.Equal(6, malformed.Code);
        Assert.Empty(malformed.Output);
        Assert.Equal(before, await File.ReadAllBytesAsync(Database));
    }

    [Fact]
    public void ReferentialIntegrityIndexesAndLegacyUpgradeAreTransactional()
    {
        var graph = QueryFixture();
        Assert.Throws<SqliteException>(() => ExecuteSql("PRAGMA foreign_keys=ON; UPDATE relationships SET target_id = 'missing'"));
        Assert.Throws<SqliteException>(() => ExecuteSql("PRAGMA foreign_keys=ON; UPDATE nodes SET entity_id = 'missing'"));
        using (var connection = new SqliteConnection($"Data Source={Database};Mode=ReadOnly;Pooling=False"))
        {
            connection.Open();
            using var sql = connection.CreateCommand();
            sql.CommandText = "EXPLAIN QUERY PLAN SELECT id FROM relationships WHERE target_id = 'D' AND kind = 'CALLS' AND confidence = 'exact' ORDER BY id LIMIT 10";
            using var reader = sql.ExecuteReader();
            var details = new List<string>();
            while (reader.Read()) details.Add(reader.GetString(3));
            Assert.Contains(details, d => d.Contains("ix_relationships_target", StringComparison.Ordinal));
        }
        ExecuteSql("DROP TABLE occurrences; DROP TABLE relationships; DROP TABLE nodes; CREATE TABLE relationships(id TEXT PRIMARY KEY, json TEXT NOT NULL); CREATE TABLE occurrences(id TEXT PRIMARY KEY, relationship_id TEXT REFERENCES relationships(id), json TEXT NOT NULL); PRAGMA user_version=2;");
        Assert.Equal(2, new GraphStore(Database).Status().SchemaVersion);
        Assert.Equal(4, Assert.Throws<GraphException>(() => new GraphStore(Database).Query("find", "A")).ExitCode);
        Assert.Throws<SqliteException>(() => new GraphStore(Database).Replace(graph with { Occurrences = [graph.Occurrences[0] with { RelationshipId = "missing" }] }));
        Assert.Equal(2, new GraphStore(Database).Status().SchemaVersion);
        new GraphStore(Database).Replace(graph);
        Assert.Equal(3, new GraphStore(Database).Status().SchemaVersion);
        Assert.Equal(new[] { "A", "B", "C" }, Ids(Query("impact", "D")));
    }

    [Fact]
    public async Task ActualRoslynGraphQueriesReturnExpectedCrossProjectPathAndImpact()
    {
        var graph = await IndexPath(Path.Combine(Root, "samples/HelloGraph/HelloGraph.slnx"), "--allow-generators");
        var welcome = Assert.Single(graph.Entities, e => e.Name == "Welcome");
        var greet = Assert.Single(graph.Entities, e => e.Name == "Greet");
        var callers = Query("callers", greet.Id);
        Assert.Equal(new[] { welcome.Id }, Ids(callers));
        Assert.Contains(Query("callees", welcome.Id).Items, i => i.Symbol.Id == greet.Id);
        var path = Assert.Single(Query("path", welcome.Id, to: greet.Id).Items);
        Assert.Single(path.Path);
        Assert.Equal(RelationshipKind.CALLS, path.Path[0].Relationship.Kind);
        var evidence = Assert.Single(path.Path[0].Evidence);
        var source = File.ReadAllLines(evidence.Source.File)[evidence.Source.StartLine - 1];
        Assert.Equal("new Greeter().Greet(name)", source[(evidence.Source.StartColumn - 1)..(evidence.Source.EndColumn - 1)]);
        var impact = Query("impact", greet.Id);
        Assert.Equal(new[] { welcome.Id }, Ids(impact));
        Assert.True(impact.Items[0].Path[0].Reversed);
        Assert.Equal(evidence.Id, impact.Items[0].Path[0].Evidence[0].Id);
    }

    private void ExecuteSql(string text)
    {
        using var connection = new SqliteConnection($"Data Source={Database};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = text;
        command.ExecuteNonQuery();
    }

    [Fact]
    public void ExternalEndpointsRemainDistinctAndUnresolvedCandidatesAreNeverTraversed()
    {
        var graph = QueryFixture();
        var builder = new RelationshipBuilder(graph.Analysis.Id);
        var a = new RelationshipSymbol("A", "M:Fixture.A", "Fixture.A", "Fixture", "project", "A");
        var external = new RelationshipSymbol("external", "M:Library.Run", "Library.Run()", "Library", null);
        builder.Add(RelationshipKind.CALLS, a, external, "project", Confidence.Exact,
            new("fixture.cs", 10, 1, 10, 5, "hash"), "Bound metadata invocation");
        builder.Add(RelationshipKind.CALLS, a, null, "project", Confidence.Unresolved,
            new("fixture.cs", 11, 1, 11, 5, "hash"), "Unknown runtime target", relatedSymbols: [external]);
        new GraphStore(Database).Replace(graph with { Relationships = [.. graph.Relationships, .. builder.Relationships],
            Occurrences = [.. graph.Occurrences, .. builder.Occurrences] });
        var target = Assert.Single(Query("path", "A", to: "external").Items);
        Assert.Null(target.Entity);
        Assert.Equal(external, target.Symbol);
        Assert.Single(target.Path);
        Assert.Equal(Confidence.Exact, target.Path[0].Relationship.Confidence);
        Assert.All(Query("callees", "A", inferred: true).Items.SelectMany(i => i.Path), step =>
            Assert.NotEqual(Confidence.Unresolved, step.Relationship.Confidence));
        Assert.Null(Assert.Single(new GraphStore(Database).Query("symbol", "external").Envelope.Items).Entity);
        Assert.Equal(new[] { "A" }, Ids(Query("callers", "external", depth: 1)));
        var invalid = builder.Relationships.Single(r => r.Target is not null) with { Target = external with { EntityId = "absent" } };
        Assert.Throws<SqliteException>(() => new GraphStore(Database).Replace(graph with { Relationships = [invalid] }));
        Assert.Equal(external, Assert.Single(Query("path", "A", to: "external").Items).Symbol);
    }

    [Fact]
    public void QueriesDoNotDeserializeUnrelatedGraphRowsAndReportSelectedCorruption()
    {
        QueryFixture(fanout: 1);
        // Intentionally bypass CHECK to simulate corruption in an unrelated component.
        ExecuteSql("PRAGMA ignore_check_constraints=ON; UPDATE relationships SET json = '{broken' WHERE source_id = 'Isolated';");
        Assert.Equal(new[] { "A", "B", "C" }, Ids(Query("impact", "D")));
        Assert.Equal(6, Assert.Throws<GraphException>(() => Query("callees", "Isolated")).ExitCode);
        ExecuteSql("PRAGMA ignore_check_constraints=ON; UPDATE nodes SET json = 'null' WHERE id = 'Isolated';");
        Assert.Single(new GraphStore(Database).Query("find", "Fixture.A").Envelope.Items);
        Assert.Equal(6, Assert.Throws<GraphException>(() => new GraphStore(Database).Query("symbol", "Isolated")).ExitCode);
    }

    [Fact]
    public async Task RepeatedCliQueriesAreByteStableAndBothPathArgumentsRequireSelection()
    {
        QueryFixture();
        var args = new[] { "query", "path", "A", "D", "--db", Database };
        var first = await Run(args);
        var second = await Run(args);
        Assert.Equal(0, first.Code);
        Assert.Equal(0, second.Code);
        Assert.Equal(first.Output, second.Output);
        Assert.Equal(7, (await Run("query", "path", "A", "Overload", "--db", Database)).Code);
        Assert.Equal(8, (await Run("query", "path", "A", "Absent", "--db", Database)).Code);
        var selected = await Run("query", "dependencies", "D", "--direction", "incoming", "--edge-types", "REFERENCES", "--db", Database);
        Assert.Equal(0, selected.Code);
        using var json = JsonDocument.Parse(selected.Output);
        Assert.Equal("ReferenceOnly", Assert.Single(json.RootElement.GetProperty("items").EnumerateArray()).GetProperty("symbol").GetProperty("id").GetString());
        var inferred = await Run("query", "impact", "D", "--include-inferred", "--db", Database);
        Assert.Equal(0, inferred.Code);
        using var included = JsonDocument.Parse(inferred.Output);
        Assert.Contains(included.RootElement.GetProperty("items").EnumerateArray(), i => i.GetProperty("symbol").GetProperty("id").GetString() == "Inferred");
    }
}
