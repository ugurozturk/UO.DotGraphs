using System.Text.Json;
using UO.Graphs.Core;
using UO.Graphs.Storage.Sqlite;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests
{
    [Fact]
    public async Task SemanticEvidenceSurvivesSQLiteAndSeparateCliProcess()
    {
        var graph = await IndexPath(Path.Combine(Root, "samples/Phase3/Semantics.csproj"), "--allow-generators");
        Assert.Equal("completeWithinDeclaredScope", graph.Analysis.Coverage);
        const string caller = "M:SemanticFixture.Worker.Calls(SemanticFixture.IContract,SemanticFixture.Base)";
        var edge = Assert.Single(graph.Relationships, r => r.Kind == RelationshipKind.CALLS && r.Source.DeclarationId == caller &&
            r.Target?.DeclarationId == "M:SemanticFixture.Worker.Overload(System.Int32)");
        Assert.Equal(Confidence.Exact, edge.Confidence);
        Assert.Equal(2, graph.Occurrences.Count(o => o.RelationshipId == edge.Id));
        Assert.NotNull(edge.Source.EntityId);
        Assert.NotNull(edge.Target!.EntityId);
        var query = await Run("query", "callees", edge.Source.Id, "--format", "json", "--db", Database);
        Assert.Equal(0, query.Code);
        using var json = JsonDocument.Parse(query.Output);
        var steps = json.RootElement.GetProperty("items").EnumerateArray().SelectMany(i => i.GetProperty("path").EnumerateArray()).ToArray();
        var relationships = steps.Select(s => JsonSerializer.Deserialize<GraphRelationship>(s.GetProperty("relationship"), GraphJson.Options)!).ToArray();
        var occurrences = steps.SelectMany(s => JsonSerializer.Deserialize<RelationshipOccurrence[]>(s.GetProperty("evidence"), GraphJson.Options)!).ToArray();
        Assert.Equal(edge, Assert.Single(relationships, r => r.Id == edge.Id));
        var persisted = occurrences.Where(o => o.RelationshipId == edge.Id).ToArray();
        Assert.Equal(2, persisted.Length);
        foreach (var occurrence in persisted)
        {
            var original = Assert.Single(graph.Occurrences, o => o.Id == occurrence.Id);
            Assert.Equal(JsonSerializer.Serialize(original, GraphJson.Options), JsonSerializer.Serialize(occurrence, GraphJson.Options));
            Assert.Equal(graph.Analysis.Id, occurrence.AnalysisId);
            Assert.Contains(graph.Analysis.Projects, p => p.Id == occurrence.ContextId);
        }
        var status = new GraphStore(Database).Status();
        Assert.Equal(graph.Relationships.Length, status.RelationshipCount);
        Assert.Equal(graph.Occurrences.Length, status.OccurrenceCount);
        var lambda = Assert.Single(graph.Entities, e => e.Kind == "lambda");
        Assert.Contains(graph.Relationships, r => r.Kind == RelationshipKind.DECLARES && r.Source.Id == edge.Source.Id && r.Target?.Id == lambda.Id);
        var worker = Assert.Single(graph.Entities, e => e.DocumentationId == "T:SemanticFixture.Worker");
        var declaration = Assert.Single(graph.Relationships, r => r.Kind == RelationshipKind.DECLARES && r.Target?.Id == worker.Id);
        Assert.Equal(2, graph.Occurrences.Count(o => o.RelationshipId == declaration.Id));
        Assert.All(graph.Relationships, r => Assert.Contains(graph.Occurrences, o => o.RelationshipId == r.Id));
    }

    [Fact]
    public async Task BrokenSemanticCompilationPersistsUnresolvedCandidatesWithoutFalseExactCalls()
    {
        var fixture = CopyFixture("broken-semantics");
        await File.WriteAllTextAsync(Path.Combine(fixture, "Library/Semantics.cs"),
            "#define BROKEN_SCENARIO\n" + await File.ReadAllTextAsync(Path.Combine(Root, "samples/Phase3/Semantics.cs")));
        File.Copy(Path.Combine(Root, "samples/Phase3/Partial.cs"), Path.Combine(fixture, "Library/SemanticPartial.cs"));
        var graph = await IndexPath(Path.Combine(fixture, "Library/Library.csproj"), "--framework", "net10.0", "--allow-generators");
        Assert.Equal("partial", graph.Analysis.Coverage);
        Assert.Contains(graph.Analysis.Diagnostics, d => d.Contains("CS1503", StringComparison.Ordinal));
        Assert.Contains(graph.Analysis.Diagnostics, d => d.Contains("CS0121", StringComparison.Ordinal));
        foreach (var owner in new[] { "M:SemanticFixture.Worker.Broken", "M:SemanticFixture.Worker.Ambiguous" })
        {
            var edge = Assert.Single(graph.Relationships, r => r.Kind == RelationshipKind.CALLS && r.Source.DeclarationId == owner);
            Assert.Equal(Confidence.Unresolved, edge.Confidence);
            Assert.Null(edge.Target);
            string[] expectedSites = owner.EndsWith("Broken", StringComparison.Ordinal)
                ? ["Overload(new object())", "new object()", "Generic<Missing>(null)", "new Worker(\"wrong\")", "MissingCall()", "Overload(1)"]
                : ["Choose(null)"];
            Assert.Equal(expectedSites.Order(StringComparer.Ordinal), graph.Occurrences.Where(o => o.RelationshipId == edge.Id)
                .Select(o => File.ReadAllLines(o.Source.File)[o.Source.StartLine - 1][(o.Source.StartColumn - 1)..(o.Source.EndColumn - 1)])
                .Order(StringComparer.Ordinal));
            Assert.DoesNotContain(graph.Relationships, r => r.Kind == RelationshipKind.CALLS && r.Source.DeclarationId == owner && r.Confidence == Confidence.Exact);
        }
        Assert.Contains(graph.Occurrences, o => o.RelatedSymbols.Any(s => s.DeclarationId == "M:SemanticFixture.Worker.Choose(System.Uri)") &&
            o.RelatedSymbols.Any(s => s.DeclarationId == "M:SemanticFixture.Worker.Choose(System.Version)"));
        Assert.Contains(graph.Relationships, r => r.Kind == RelationshipKind.CALLS && r.Confidence == Confidence.Exact &&
            r.Source.DeclarationId == "M:SemanticFixture.Worker.FromOtherPart" && r.Target?.DeclarationId == "M:SemanticFixture.Worker.Overload(System.Int32)");
        var status = await Run("status", "--db", Database);
        Assert.Equal(0, status.Code);
        Assert.Contains("partial", status.Output);
    }

    [Fact]
    public async Task CrossProjectCallsUseReferencedSymbolIdentityAndCorrectDirection()
    {
        var graph = await IndexPath(Path.Combine(Root, "samples/HelloGraph/HelloGraph.slnx"), "--allow-generators");
        var app = Assert.Single(graph.Analysis.Projects, p => p.AssemblyName == "HelloGraph.App");
        var domain = Assert.Single(graph.Analysis.Projects, p => p.AssemblyName == "HelloGraph.Domain");
        var reference = Assert.Single(graph.Relationships, r => r.Kind == RelationshipKind.PROJECT_REFERENCES);
        Assert.Equal(app.Id, reference.Source.Id);
        Assert.Equal(domain.Id, reference.Target!.Id);
        var greet = Assert.Single(graph.Entities, e => e.DocumentationId == "M:HelloGraph.Domain.Greeter.Greet(System.String)");
        var welcome = Assert.Single(graph.Entities, e => e.Name == "Welcome");
        var call = Assert.Single(graph.Relationships, r => r.Kind == RelationshipKind.CALLS && r.Target?.DeclarationId == greet.DocumentationId);
        Assert.Equal(welcome.Id, call.Source.Id);
        Assert.Equal(greet.Id, call.Target!.Id);
        Assert.Equal(greet.Id, call.Target.EntityId);
        Assert.Equal(domain.Id, call.Target.ProjectId);
        Assert.Equal(app.Id, call.ContextId);
        Assert.DoesNotContain(graph.Relationships, r => r.Kind == RelationshipKind.CALLS && r.Source.Id == greet.Id && r.Target?.Id == welcome.Id);
    }
}
