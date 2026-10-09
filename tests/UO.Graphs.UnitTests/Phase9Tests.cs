using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UO.Graphs.Core;
using UO.Graphs.Roslyn;

namespace UO.Graphs.UnitTests;

public sealed class Phase9Tests
{
    private static readonly string Root = FindRoot();
    private static readonly Lazy<Task<Result>> Abp = new(() => Analyze("Abp", "Services.cs"));
    private static readonly Lazy<Task<Result>> Ef = new(() => Analyze("EfCore", "Models.cs"));
    private static readonly Lazy<Task<Result>> Refit = new(() => Analyze("Refit", "Clients.cs"));

    [Fact]
    public async Task AbpContractsModulesAndGenericRepositoryUseRealPackageSymbols()
    {
        var r = await Abp.Value;
        Assert.Empty(r.Errors);
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.APP_SERVICE_CONTRACT && e.Source.DisplayName == "AbpFixture.BookAppService" && e.Target?.DisplayName == "AbpFixture.IBookAppService");
        Assert.Equal(2, r.Edges.Count(e => e.Kind == RelationshipKind.MODULE_DEPENDS_ON));
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.REPOSITORY_ENTITY && e.Source.DisplayName.Contains("BookAppService.GetAsync") && e.Target?.DisplayName == "AbpFixture.Book");
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.REPOSITORY_ENTITY && e.Source.DisplayName.Contains("OtherUse.Get") && e.Target?.DisplayName == "AbpFixture.Author");
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.REPOSITORY_ENTITY && e.Source.DisplayName.Contains("GenericUse") && e.Target is null);
        Assert.DoesNotContain(r.Entities.Values, e => e.ModelMapping is not null);
    }
    [Fact]
    public async Task AbpExposureOverridesMetadataAndConfigurationStaySeparate()
    {
        var r = await Abp.Value;
        var candidates = r.Entities.Values.Where(e => e.HttpEndpoint?.Framework == "abp").ToArray();
        Assert.NotEmpty(candidates);
        Assert.Contains(candidates, e => e.Name.Contains("BookAppService.StillRemote"));
        Assert.Contains(candidates, e => e.Name.Contains("InterfaceAttributeAppService.Run"));
        Assert.DoesNotContain(candidates, e => e.Name.Contains("Hidden") || e.Name.Contains("Utility"));
        Assert.All(candidates, e => { Assert.Equal(Confidence.Inferred, e.Confidence); Assert.Null(e.HttpEndpoint!.RoutePattern); Assert.True(e.Sources.Length >= 2); });
        Assert.All(r.Edges.Where(e => e.Kind == RelationshipKind.HANDLED_BY && candidates.Any(c => c.Id == e.Source.Id)), e => Assert.Equal(Confidence.Inferred, e.Confidence));
        Assert.Contains(r.Diagnostics, d => d.Contains("TypePredicate"));
    }
    [Fact]
    public async Task AbpWithoutStaticAssemblyRegistrationNeverInventsEndpoint()
    {
        var r = await Analyze("Abp", "Services.cs", source => source.Replace("typeof(BookAppService).Assembly", "Assembly.GetExecutingAssembly()"));
        Assert.Empty(r.Errors);
        Assert.DoesNotContain(r.Entities.Values, e => e.HttpEndpoint?.Framework == "abp");
        Assert.Contains(r.Diagnostics, d => d.Contains("abpExposure"));
    }
    [Fact]
    public async Task AbpDiConventionsRespectLifetimeExposureAndDisableOverrides()
    {
        var r = await Abp.Value;
        var registration = Assert.Single(r.Entities.Values, e => e.Registration?.Form == "abpConvention" && e.Registration.ImplementationType == "AbpFixture.BookAppService");
        Assert.Equal("Scoped", registration.Registration!.Lifetime);
        Assert.Equal(Confidence.Inferred, registration.Confidence);
        var service = Assert.Single(r.Edges, e => e.Source.Id == registration.Id && e.Kind == RelationshipKind.REGISTERS);
        Assert.Equal("AbpFixture.IBookAppService", service.Target!.DisplayName);
        Assert.DoesNotContain(r.Entities.Values, e => e.Registration?.ImplementationType == "AbpFixture.ManualAppService");
    }
    [Fact]
    public async Task EfContextDbSetsAndConfigurationContractsAreNotEffectiveModelClaims()
    {
        var r = await Ef.Value;
        Assert.Empty(r.Errors);
        Assert.Contains(r.Entities.Values, e => e.Framework?.Role == "dbContext" && e.Name == "LibraryContext");
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.CONTEXT_ENTITY && e.Source.DisplayName == "EfFixture.LibraryContext" && e.Target?.DisplayName == "EfFixture.Book");
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.CONFIGURES_ENTITY && e.Source.DisplayName == "EfFixture.UnusedConfiguration" && e.Target?.DisplayName == "EfFixture.Author");
        Assert.DoesNotContain(r.Edges, e => e.Kind == RelationshipKind.CONTEXT_ENTITY && e.Target?.DisplayName.Contains("Configuration") == true);
        Assert.DoesNotContain(r.Edges, e => e.Kind == RelationshipKind.MAPS_ENTITY && e.Source.DisplayName.Contains("Search"));
    }
    [Fact]
    public async Task EfExplicitMappingsPreserveAnnotationAndFluentOverrideDeclarations()
    {
        var r = await Ef.Value;
        var mappings = r.Entities.Values.Select(e => e.ModelMapping).OfType<ModelMappingDeclaration>().ToArray();
        Assert.Contains(mappings, m => m.Form == "TableAttribute" && m.Table == "annotated_books" && m.Schema == "catalog");
        Assert.Contains(mappings, m => m.Form == "ToTable" && m.Table == "books" && m.Schema == "library");
        Assert.Contains(mappings, m => m.Form == "HasColumnName" && m.Member == "Title" && m.Column == "title" && m.EntityType == "EfFixture.Book");
        Assert.Contains(mappings, m => m.Form == "ColumnAttribute" && m.Column == "title_attr");
        Assert.All(mappings.Where(m => m.Table == "authors"), m => Assert.Null(m.Schema));
        Assert.All(mappings, m => Assert.Contains(m.Limitations, n => n.Contains("effective model")));
    }
    [Fact]
    public async Task EfRuntimeConfigurationScanningAndUnsupportedBuilderCannotBecomeExactMapping()
    {
        var r = await Ef.Value;
        Assert.Contains(r.Entities.Values, e => e.ModelMapping?.Form == "ApplyConfigurationsFromAssembly" && e.Confidence == Confidence.Unresolved);
        Assert.Contains(r.Entities.Values, e => e.ModelMapping?.Form == "Custom" && e.Confidence == Confidence.Unresolved);
        Assert.Contains(r.Entities.Values, e => e.ModelMapping?.Form == "OwnsOne" && e.Confidence == Confidence.Unresolved);
        Assert.Contains(r.Entities.Values, e => e.ModelMapping?.Form == "ToTable" && e.ModelMapping.Table is null && e.Confidence == Confidence.Unresolved);
        Assert.Contains(r.Entities.Values, e => e.ModelMapping?.Table == "tenant_books" && e.ModelMapping.Conditional && e.ModelMapping.Schema is null);
        Assert.DoesNotContain(r.Entities.Values, e => e.ModelMapping?.Table == "shared");
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.MAPS_ENTITY && e.Target is null);
    }
    [Fact]
    public async Task RefitSamePathDifferentServicesStayDistinctAndCallsBindToContract()
    {
        var r = await Refit.Value;
        Assert.Empty(r.Errors);
        var same = r.Entities.Values.Where(e => e.HttpContract?.Route == "/shared/{id}").ToArray();
        Assert.Equal(2, same.Length);
        Assert.NotEqual(same[0].Id, same[1].Id);
        foreach (var name in new[] { "First", "Second", "Inherited", "Shadow" })
            Assert.Single(r.Edges, e => e.Kind == RelationshipKind.INVOKES_HTTP && e.Source.DisplayName.Contains("Caller." + name + "("));
        var first = Assert.Single(r.Edges, e => e.Kind == RelationshipKind.INVOKES_HTTP && e.Source.DisplayName.Contains("Caller.First("));
        var inherited = Assert.Single(r.Edges, e => e.Kind == RelationshipKind.INVOKES_HTTP && e.Source.DisplayName.Contains("Caller.Inherited("));
        Assert.Equal(first.Target!.Id, inherited.Target!.Id);
        Assert.All(r.Edges.Where(e => e.Kind == RelationshipKind.POSSIBLE_ENDPOINT), e => Assert.Null(e.Target));
        Assert.Contains(r.Entities.Values, e => e.HttpEndpoint?.RoutePattern == "/shared/{id}");
    }
    [Fact]
    public async Task RefitRuntimeBaseAddressCustomAttributeAndNonAbstractMethodsRemainUnknown()
    {
        var r = await Refit.Value;
        Assert.All(r.Entities.Values.Where(e => e.HttpContract is not null), e => Assert.Null(e.HttpContract!.BaseAddress));
        Assert.Contains(r.Entities.Values, e => e.HttpContract?.ContractMethod.Contains("ICustomApi.Run") == true && e.Confidence == Confidence.Unresolved && e.HttpContract.Route is null);
        Assert.DoesNotContain(r.Entities.Values, e => e.HttpContract?.ContractMethod.Contains("IDefaultApi") == true || e.HttpContract?.ContractMethod.Contains("IFakeApi") == true);
        Assert.Contains(r.Diagnostics, d => d.Contains("refitConfiguration"));
    }
    [Fact]
    public async Task EfComputedAndCapturedPropertyExpressionsAndRuntimeColumnAreUnresolved()
    {
        var r = await Ef.Value;
        var mappings = r.Entities.Values.Where(e => e.ModelMapping?.Form == "HasColumnName").ToArray();
        Assert.Equal(4, mappings.Length);
        Assert.Equal(3, mappings.Count(e => e.Confidence == Confidence.Unresolved));
        Assert.DoesNotContain(mappings, e => e.ModelMapping!.Column is "wrong_receiver" or "computed");
        Assert.Contains(mappings, e => e.ModelMapping!.Limitations.Any(n => n.Contains("Runtime column")));
    }
    [Fact]
    public async Task RefitMultipleAndStaticHttpAttributesAreNotAssertedAsExecutableContracts()
    {
        var r = await Analyze("Refit", "Clients.cs", s => s.Replace("[Get(\"/shared/{id}\")] Task<string> GetAsync", "[Get(\"/shared/{id}\"), Post(\"/other\")] Task<string> GetAsync")
            .Replace("static Task<string> StaticAsync", "[Get(\"/static\")] static Task<string> StaticAsync"));
        Assert.Empty(r.Errors); // Real package metadata compilation; source generator is intentionally not run here.
        Assert.DoesNotContain(r.Entities.Values, e => e.HttpContract?.Route == "/shared/{id}" || e.HttpContract?.Route == "/static");
        Assert.Contains(r.Entities.Values, e => e.HttpContract?.Limitations.Any(n => n.Contains("Multiple HTTP")) == true && e.Confidence == Confidence.Unresolved);
    }

    [Theory]
    [InlineData("Abp", "Services.cs")]
    [InlineData("EfCore", "Models.cs")]
    [InlineData("Refit", "Clients.cs")]
    public async Task EachModuleRetainsEvidenceAndCompilationErrorsCannotAssertTargets(string module, string file)
    {
        var r = await Analyze(module, file);
        Assert.Empty(r.Errors);
        Assert.NotEmpty(r.Builder.Occurrences);
        Assert.All(r.Builder.Occurrences, o => { Assert.Equal("analysis", o.AnalysisId); Assert.Equal(64, o.Source.Sha256.Length); Assert.NotEmpty(o.Reason); });
        var broken = await Analyze(module, file, s => s.Replace("repository.GetAsync(id)", "repository.GetAsync(Missing())").Replace(".ToTable(\"books\", \"library\")", ".ToTable(Missing())").Replace("api.GetAsync(1)", "api.GetAsync(Missing())"));
        Assert.NotEmpty(broken.Errors);
        Assert.DoesNotContain(broken.Edges.Where(e => e.Kind is RelationshipKind.INVOKES_HTTP or RelationshipKind.REPOSITORY_ENTITY), e =>
            e.Confidence == Confidence.Exact && broken.Builder.Occurrences.Any(o => o.RelationshipId == e.Id && o.Reason.StartsWith("Compilation error")));
    }

    private sealed record Result(Dictionary<string, GraphEntity> Entities, RelationshipBuilder Builder, Diagnostic[] Errors, List<string> Diagnostics)
    { public GraphRelationship[] Edges => Builder.Relationships; }
    private static async Task<Result> Analyze(string module, string file, Func<string, string>? transform = null)
    {
        var folder = Path.Combine(Root, "samples/Phase9", module);
        var source = File.ReadAllText(Path.Combine(folder, file));
        if (transform is not null) source = transform(source);
        using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "obj/project.assets.json")));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(f => Path.GetFileName(f), f => f, StringComparer.OrdinalIgnoreCase);
        var cache = assets.RootElement.GetProperty("packageFolders").EnumerateObject().First().Name;
        foreach (var library in assets.RootElement.GetProperty("targets").EnumerateObject().First().Value.EnumerateObject())
            if (library.Value.TryGetProperty("compile", out var compile))
                foreach (var asset in compile.EnumerateObject().Where(a => a.Name.EndsWith(".dll", StringComparison.Ordinal)))
                    references[Path.GetFileName(asset.Name)] = Path.Combine(cache, library.Name.ToLowerInvariant(), asset.Name);
        var trees = new[] { CSharpSyntaxTree.ParseText(source, path: Path.Combine(folder, file)),
            CSharpSyntaxTree.ParseText("global using System; global using System.Linq; global using System.Threading.Tasks; global using System.Net.Http;", path: Path.Combine(folder, "Usings.cs")) };
        var compilation = CSharpCompilation.Create("Phase9Fixture", trees, references.Values.Select(f => MetadataReference.CreateFromFile(f)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var entities = new Dictionary<string, GraphEntity>();
        var builder = new RelationshipBuilder("analysis");
        var diagnostics = new List<string>();
        var collector = new RelationshipCollector(compilation, "project", folder, new Dictionary<IAssemblySymbol, string>(SymbolEqualityComparer.Default) { [compilation.Assembly] = "project" }, builder);
        foreach (var tree in trees)
        {
            await SymbolCollector.CollectAsync(compilation, tree, "project", folder, false, entities, []);
            await collector.CollectAsync(tree, false);
        }
        FrameworkExtractors.Extract(new(compilation, "project", folder, trees.Select(t => (t, false)).ToArray(), collector, builder, entities, diagnostics));
        return new(entities, builder, compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray(), diagnostics);
    }
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UO.Graphs.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
}
