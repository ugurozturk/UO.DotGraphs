using System.Text.Json;
using UO.Graphs.Core;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests
{
    [Fact]
    public async Task Phase9AbpRealPackagePersistsContractsRepositoriesAndInferredCandidates()
    {
        var graph = await IndexPath(Path.Combine(Root, "samples/Phase9/Abp/Abp.csproj"), "--full");
        Phase9Valid(graph);
        Assert.Contains(graph.Entities, e => e.Framework?.Role == "applicationService");
        Assert.Contains(graph.Relationships, e => e.Kind == RelationshipKind.MODULE_DEPENDS_ON && e.Confidence == Confidence.Exact);
        var endpoint = Assert.Single(graph.Entities, e => e.HttpEndpoint?.Framework == "abp" && e.Name.Contains("BookAppService.GetAsync"));
        var book = Assert.Single(graph.Entities, e => e.DocumentationId == "T:AbpFixture.Book");
        var path = await Run("query", "path", endpoint.Id, book.Id, "--edge-types", "HANDLED_BY,REPOSITORY_ENTITY", "--include-inferred", "--db", Database);
        Assert.True(path.Code == 0, path.Error);
        Assert.Contains("REPOSITORY_ENTITY", path.Output);
        Assert.Contains("inferred", path.Output);
        var export = await Run("export", "--format", "json", "--max-nodes", "1000", "--limit", "10000", "--db", Database);
        Assert.True(export.Code == 0, export.Error);
        Assert.Contains("candidateOnly", export.Output);
        Assert.DoesNotContain(graph.Entities, e => e.ModelMapping is not null);
    }
    [Fact]
    public async Task Phase9EfRealPackagePersistsMappingsAndUnresolvedRuntimeConfiguration()
    {
        var graph = await IndexPath(Path.Combine(Root, "samples/Phase9/EfCore/EfCore.csproj"), "--full");
        Phase9Valid(graph);
        Assert.Contains(graph.Entities, e => e.ModelMapping?.Table == "books" && e.ModelMapping.Schema == "library");
        Assert.Contains(graph.Entities, e => e.ModelMapping?.Form == "ApplyConfigurationsFromAssembly" && e.Confidence == Confidence.Unresolved);
        var context = Assert.Single(graph.Entities, e => e.Framework?.Role == "dbContext");
        var query = await Run("query", "dependencies", context.Id, "--edge-types", "CONTEXT_ENTITY", "--db", Database);
        Assert.True(query.Code == 0, query.Error);
        Assert.Contains("EfFixture.Book", query.Output);
        var export = await Run("export", "--format", "json", "--max-nodes", "1000", "--limit", "10000", "--db", Database);
        Assert.True(export.Code == 0, export.Error);
        Assert.Contains("modelMapping", export.Output);
        Assert.Contains("effective model", export.Output);
    }
    [Fact]
    public async Task Phase9RefitRealPackageKeepsSamePathServicesSeparateInContextAndExport()
    {
        var graph = await IndexPath(Path.Combine(Root, "samples/Phase9/Refit/Refit.csproj"), "--full");
        Phase9Valid(graph);
        Assert.Equal(2, graph.Entities.Count(e => e.HttpContract?.Route == "/shared/{id}"));
        Assert.All(graph.Relationships.Where(r => r.Kind == RelationshipKind.POSSIBLE_ENDPOINT), r => Assert.Null(r.Target));
        var caller = Assert.Single(graph.Entities, e => e.DocumentationId == "M:RefitFixture.Caller.First(RefitFixture.IFirstApi)");
        var query = await Run("context", caller.Id, "--depth", "2", "--max-chars", "50000", "--db", Database);
        Assert.True(query.Code == 0, query.Error);
        Assert.Contains("INVOKES_HTTP", query.Output);
        Assert.Contains("Base address", query.Output);
        var export = await Run("export", "--format", "json", "--max-nodes", "1000", "--limit", "10000", "--db", Database);
        Assert.True(export.Code == 0, export.Error);
        Assert.Contains("httpContract", export.Output);
    }
    private static void Phase9Valid(GraphSnapshot graph)
    {
        Assert.DoesNotContain(graph.Analysis.Diagnostics, d => d.Contains("error CS", StringComparison.Ordinal));
        Assert.Equal("partial", graph.Analysis.Coverage);
        Assert.All(graph.Occurrences, o => { Assert.NotEmpty(o.Reason); Assert.Equal(64, o.Source.Sha256.Length); });
        var roundtrip = JsonSerializer.Deserialize<GraphSnapshot>(JsonSerializer.Serialize(graph, GraphJson.Options), GraphJson.Options)!;
        Assert.Equal(graph.Entities.Count(e => e.Framework is not null), roundtrip.Entities.Count(e => e.Framework is not null));
        Assert.Equal(graph.Entities.Count(e => e.ModelMapping is not null), roundtrip.Entities.Count(e => e.ModelMapping is not null));
        Assert.Equal(graph.Entities.Count(e => e.HttpContract is not null), roundtrip.Entities.Count(e => e.HttpContract is not null));
    }
}
