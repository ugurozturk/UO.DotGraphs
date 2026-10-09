using System.Text.Json;
using UO.Graphs.Core;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests
{
    [Fact]
    public async Task UoRealPackageEndpointServiceRequestHandlerChainSurvivesPathAndContext()
    {
        var graph = await IndexPath(Path.Combine(Root, "samples/Phase8/Uo/Uo.csproj"), "--full");
        Assert.DoesNotContain(graph.Analysis.Diagnostics, d => d.StartsWith("compilation", StringComparison.Ordinal) && d.Contains("error CS", StringComparison.Ordinal));
        var endpoint = Assert.Single(graph.Entities, e => e.Name == "GET /phase8/start");
        var handler = Assert.Single(graph.Entities, e => e.DocumentationId == "M:UoFlows.HandlerB.HandleAsync(UoFlows.RequestB)");
        const string edges = "HANDLED_BY,CALLS,DISPATCHES,POSSIBLE_TARGET";
        var query = await Run("query", "path", endpoint.Id, handler.Id, "--edge-types", edges, "--depth", "10", "--include-inferred", "--db", Database);
        Assert.True(query.Code == 0, query.Error + query.Output);
        using var json = JsonDocument.Parse(query.Output);
        var path = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray()).GetProperty("path");
        Assert.Equal(6, path.GetArrayLength());
        Assert.Contains("DISPATCHES", query.Output);
        Assert.Contains("POSSIBLE_TARGET", query.Output);
        Assert.Contains("inferred", query.Output);
        var exactOnly = await Run("query", "path", endpoint.Id, handler.Id, "--edge-types", edges, "--depth", "10", "--db", Database);
        Assert.Equal(0, exactOnly.Code);
        using var exactJson = JsonDocument.Parse(exactOnly.Output);
        Assert.Empty(exactJson.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("notFound", exactJson.RootElement.GetProperty("outcome").GetString());
        var context = await Run("context", endpoint.Id, "--depth", "8", "--max-nodes", "300", "--limit", "800", "--max-edges", "10000", "--max-chars", "900000", "--db", Database);
        Assert.True(context.Code == 0, context.Error);
        Assert.Contains("UoFlows.HandlerB", context.Output);
        Assert.Contains("POSSIBLE_TARGET", context.Output);
        Assert.Contains("other services/repositories", context.Output);
        Assert.Equal("partial", graph.Analysis.Coverage);
        Assert.All(graph.Entities.Where(e => e.Message is not null), e => Assert.NotEmpty(e.Sources));
    }

    [Fact]
    public async Task RealMediatrAndMassTransitPackagesKeepMultipleConsumersAndUnresolvedTargets()
    {
        var graph = await IndexPath(Path.Combine(Root, "samples/Phase8/PackageCompatibility/PackageCompatibility.csproj"), "--full");
        Assert.DoesNotContain(graph.Analysis.Diagnostics, d => d.StartsWith("compilation", StringComparison.Ordinal) && d.Contains("error CS", StringComparison.Ordinal));
        foreach (var framework in new[] { "mediatr", "massTransit" })
        {
            var message = Assert.Single(graph.Entities, e => e.Message?.Framework == framework && e.Message.ConstructedType == "ContractFlows.Event");
            Assert.Equal(2, graph.Relationships.Count(r => r.Kind == RelationshipKind.CONSUMES && r.Target?.Id == message.Id));
            Assert.Equal(2, graph.Relationships.Count(r => r.Kind == RelationshipKind.POSSIBLE_TARGET && r.Source.Id == message.Id && r.Target is not null));
            Assert.Contains(graph.Relationships, r => r.Kind == RelationshipKind.POSSIBLE_TARGET && r.Source.Id == message.Id && r.Target is null);
        }
        var export = await Run("export", "--format", "json", "--max-nodes", "1000", "--limit", "10000", "--db", Database);
        Assert.True(export.Code == 0, export.Error);
        Assert.Contains("CONSUMES", export.Output);
        Assert.Contains("contractIdentity", export.Output);
        Assert.Contains("AssemblyIdentity", JsonSerializer.Serialize(graph, GraphJson.Options), StringComparison.OrdinalIgnoreCase);
    }
}
