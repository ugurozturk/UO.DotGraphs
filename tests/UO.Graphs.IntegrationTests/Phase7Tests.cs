using System.Text.Json;
using UO.Graphs.Core;
using UO.Graphs.Storage.Sqlite;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests
{
    [Fact]
    public async Task MinimalWebSdkTopLevelFixtureIndexesWithoutStartingApplication()
    {
        var graph = await IndexPath(Path.Combine(Root, "samples/Phase7/MinimalWeb/MinimalWeb.csproj"), "--full");
        Assert.DoesNotContain(graph.Analysis.Diagnostics, d => d.StartsWith("compilation", StringComparison.Ordinal) && d.Contains("error CS", StringComparison.Ordinal));
        Assert.Equal(new[] { "GET /api/time", "POST /api/time" }, graph.Entities.Where(e => e.HttpEndpoint is not null).Select(e => e.Name).Order());
        Assert.Single(graph.Entities, e => e.Registration?.ServiceType == "IClock");
        foreach (var endpoint in graph.Entities.Where(e => e.HttpEndpoint is not null))
        {
            var handler = Assert.Single(graph.Relationships, r => r.Source.Id == endpoint.Id && r.Kind == RelationshipKind.HANDLED_BY);
            Assert.NotNull(handler.Target!.EntityId);
            Assert.Contains(graph.Relationships, r => r.Source.Id == handler.Target.Id && r.Kind == RelationshipKind.INJECTS && r.Target!.DeclarationId == "T:IClock");
            Assert.Equal("notProven", endpoint.HttpEndpoint!.Activation);
        }
    }

    [Fact]
    public async Task FrameworkDeclarationsSurviveIndexStorageQueryAndExportWithoutRunningHost()
    {
        var graph = await IndexPath(Path.Combine(Root, "samples/Phase7/Frameworks.csproj"), "--full");
        Assert.DoesNotContain(graph.Analysis.Diagnostics, d => d.StartsWith("compilation", StringComparison.Ordinal) && d.Contains("error CS", StringComparison.Ordinal));
        Assert.Equal(20, graph.Entities.Count(e => e.HttpEndpoint is not null));
        Assert.Equal(10, graph.Entities.Count(e => e.Registration is not null));
        var endpoint = Assert.Single(graph.Entities, e => e.Name == "GET /api/v1/messages");
        Assert.Equal(3, endpoint.Sources.Length);
        var handler = Assert.Single(graph.Relationships, r => r.Source.Id == endpoint.Id && r.Kind == RelationshipKind.HANDLED_BY);
        Assert.NotNull(handler.Target!.EntityId);
        var injections = graph.Relationships.Where(r => r.Source.Id == handler.Target.Id && r.Kind == RelationshipKind.INJECTS).ToArray();
        Assert.Contains(injections, r => r.Target?.DeclarationId == "T:FrameworkFixture.IMessage" && r.Confidence == Confidence.Exact);
        var query = await Run("query", "dependencies", endpoint.Id, "--depth", "3", "--db", Database);
        Assert.Equal(0, query.Code);
        Assert.Contains("HANDLED_BY", query.Output);
        Assert.Contains("INJECTS", query.Output);
        Assert.Contains("host activation", query.Output);
        var found = await Run("query", "find", endpoint.Name, "--db", Database);
        Assert.Equal(0, found.Code);
        Assert.Contains("notProven", found.Output);
        var export = await Run("export", "--format", "json", "--symbol", endpoint.Id, "--db", Database);
        Assert.True(export.Code == 0, export.Error);
        using var json = JsonDocument.Parse(export.Output);
        Assert.Contains("/api/v1/messages", json.RootElement.GetRawText());
        Assert.Contains("Static endpoint declaration", export.Output);
        Assert.Contains(graph.Entities, e => e.HttpEndpoint is { RoutePattern: null, CollisionKey: null });
        Assert.Equal(2, graph.Entities.Count(e => e.Name == "GET /duplicate"));
        var factory = graph.Entities.First(e => e.Registration?.Form == "factory");
        Assert.Null(Assert.Single(graph.Relationships, r => r.Source.Id == factory.Id && r.Kind == RelationshipKind.IMPLEMENTED_BY).Target);
        Assert.Equal("partial", graph.Analysis.Coverage);
        Assert.Equal(graph.Occurrences.Length, new GraphStore(Database).Status().OccurrenceCount);
    }
}
