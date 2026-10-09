using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UO.Graphs.Core;
using UO.Graphs.Roslyn;

namespace UO.Graphs.UnitTests;

public sealed class FrameworkTests
{
    private static readonly Lazy<Task<Result>> Fixture = new(() => Analyze());
    [Fact]
    public async Task RealFrameworkCompilationHasNoErrorsAndLookalikesAreIgnored()
    {
        var r = await Fixture.Value;
        Assert.Empty(r.Errors);
        Assert.DoesNotContain(r.Entities.Values, e => e.HttpEndpoint?.RoutePattern == "/fake");
        Assert.DoesNotContain(r.Entities.Values.Where(e => e.Registration is not null), e => e.QualifiedName.Contains("Lookalikes", StringComparison.Ordinal));
        Assert.All(r.Builder.Occurrences, o => { Assert.Equal("analysis", o.AnalysisId); Assert.Equal("project", o.ContextId); Assert.Equal(64, o.Source.Sha256.Length); });
    }
    [Fact]
    public async Task LifetimesMultipleRegistrationsAndOpenGenericsAreSeparateDeclarations()
    {
        var r = await Fixture.Value;
        var registrations = r.Entities.Values.Where(e => e.Registration is not null).ToArray();
        Assert.Equal(10, registrations.Length);
        Assert.Equal(7, registrations.Count(e => e.Registration!.ServiceType == "FrameworkFixture.IMessage"));
        Assert.Equal(new[] { "Scoped", "Singleton", "Transient", "unknown" }, registrations.Select(e => e.Registration!.Lifetime).Distinct().Order());
        var generic = Assert.Single(registrations, e => e.Registration!.OpenGeneric);
        var edge = Assert.Single(r.Builder.Relationships, e => e.Source.Id == generic.Id && e.Kind == RelationshipKind.IMPLEMENTED_BY);
        Assert.Equal("T:FrameworkFixture.Repository`1", edge.Target!.DeclarationId);
        Assert.Equal(Confidence.Exact, edge.Confidence);
        Assert.Equal("FrameworkFixture.Repository<>", generic.Registration!.ImplementationType);
    }
    [Fact]
    public async Task ConditionalFactoryScanningAndDynamicTypesRetainUncertainty()
    {
        var r = await Fixture.Value;
        var conditional = Assert.Single(r.Entities.Values, e => e.Registration?.Conditional == true);
        Assert.Contains(conditional.Registration!.Limitations, l => l.Contains("Conditional"));
        Assert.Equal(Confidence.Exact, Assert.Single(r.Builder.Relationships, e => e.Source.Id == conditional.Id && e.Kind == RelationshipKind.REGISTERS).Confidence);
        var factories = r.Entities.Values.Where(e => e.Registration?.Form == "factory").ToArray();
        Assert.Equal(2, factories.Length);
        foreach (var factory in factories)
        {
            Assert.Null(Assert.Single(r.Builder.Relationships, e => e.Source.Id == factory.Id && e.Kind == RelationshipKind.IMPLEMENTED_BY).Target);
            Assert.NotNull(Assert.Single(r.Builder.Relationships, e => e.Source.Id == factory.Id && e.Kind == RelationshipKind.USES_FACTORY).Target);
        }
        var opaque = Assert.Single(r.Entities.Values, e => e.Registration?.Form == "opaque");
        Assert.Null(Assert.Single(r.Builder.Relationships, e => e.Source.Id == opaque.Id && e.Kind == RelationshipKind.REGISTERS).Target);
        var dynamicType = Assert.Single(r.Entities.Values, e => e.Registration is { Form: "type", ImplementationType: null });
        Assert.Null(Assert.Single(r.Builder.Relationships, e => e.Source.Id == dynamicType.Id && e.Kind == RelationshipKind.IMPLEMENTED_BY).Target);
    }
    [Fact]
    public async Task ConstructorInjectionTargetsTypesAndPreservesConstructedSignature()
    {
        var r = await Fixture.Value;
        var injection = r.Builder.Relationships.Where(e => e.Kind == RelationshipKind.INJECTS && e.Source.DeclarationId.StartsWith("M:FrameworkFixture.Consumer.#ctor", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, injection.Length);
        Assert.All(injection, e => Assert.Equal(Confidence.Exact, e.Confidence));
        Assert.Contains(injection, e => e.Target?.DeclarationId == "T:FrameworkFixture.IMessage");
        var generic = Assert.Single(injection, e => e.Target?.DeclarationId == "T:FrameworkFixture.IRepository`1");
        Assert.Contains(r.Builder.Occurrences, o => o.RelationshipId == generic.Id && o.BoundTarget == "FrameworkFixture.IRepository<string>");
        Assert.DoesNotContain(injection, e => e.Target?.DeclarationId is "T:FrameworkFixture.Message" or "T:FrameworkFixture.OtherMessage");
    }
    [Fact]
    public async Task MinimalRoutesGroupsVerbsDuplicatesAndRuntimeValuesAreDistinct()
    {
        var r = await Fixture.Value;
        var endpoints = r.Entities.Values.Where(e => e.HttpEndpoint is { Framework: "minimalApi" }).ToArray();
        Assert.Equal(15, endpoints.Length);
        Assert.Contains(endpoints, e => e.Name == "GET /api/v1/messages");
        Assert.Contains(endpoints, e => e.Name == "POST /api/v1/messages");
        Assert.Equal(new[] { "GET", "POST" }, endpoints.Where(e => e.HttpEndpoint!.RoutePattern == "/multi").Select(e => e.HttpEndpoint!.HttpMethod).Order());
        Assert.Equal(new[] { "PATCH", "PUT" }, endpoints.Where(e => e.HttpEndpoint!.RoutePattern == "/collection").Select(e => e.HttpEndpoint!.HttpMethod).Order());
        var duplicates = endpoints.Where(e => e.Name == "GET /duplicate").ToArray();
        Assert.Equal(2, duplicates.Length);
        Assert.NotEqual(duplicates[0].Id, duplicates[1].Id);
        Assert.Equal(duplicates[0].HttpEndpoint!.CollisionKey, duplicates[1].HttpEndpoint!.CollisionKey);
        Assert.All(duplicates, e => Assert.Contains(e.HttpEndpoint!.Limitations, l => l.Contains("Duplicate")));
        Assert.DoesNotContain(Assert.Single(endpoints, e => e.Name == "POST /duplicate").HttpEndpoint!.Limitations, l => l.Contains("Duplicate"));
        Assert.Equal(3, endpoints.Count(e => e.HttpEndpoint!.RoutePattern is null));
        Assert.All(endpoints.Where(e => e.HttpEndpoint!.RoutePattern is null), e => { Assert.Equal(Confidence.Unresolved, e.Confidence); Assert.Null(e.HttpEndpoint!.CollisionKey); });
        Assert.All(endpoints, e => Assert.Equal("notProven", e.HttpEndpoint!.Activation));
        var delegated = Assert.Single(endpoints, e => e.Name == "GET /delegate");
        Assert.Null(Assert.Single(r.Builder.Relationships, e => e.Source.Id == delegated.Id && e.Kind == RelationshipKind.HANDLED_BY).Target);
    }
    [Fact]
    public async Task ControllerActionsHandlersAndServicesFormEvidencePaths()
    {
        var r = await Fixture.Value;
        var endpoints = r.Entities.Values.Where(e => e.HttpEndpoint is { Framework: "controller" }).ToArray();
        Assert.Equal(new[] { "GET /absolute", "GET /api/Messages/{id}", "GET /api/Messages/aliases/a", "GET /api/Messages/aliases/b", "POST /api/Messages/{id}" }, endpoints.Select(e => e.Name).Order());
        var endpoint = Assert.Single(endpoints, e => e.Name == "GET /api/Messages/{id}");
        var handler = Assert.Single(r.Builder.Relationships, e => e.Source.Id == endpoint.Id && e.Kind == RelationshipKind.HANDLED_BY).Target!;
        var constructor = Assert.Single(r.Builder.Relationships, e => e.Source.Id == handler.Id && e.Kind == RelationshipKind.ACTIVATES);
        Assert.Equal(Confidence.Inferred, constructor.Confidence);
        Assert.Contains(r.Builder.Relationships, e => e.Source.Id == constructor.Target!.Id && e.Kind == RelationshipKind.INJECTS && e.Target!.DeclarationId == "T:FrameworkFixture.IMessage");
        Assert.Contains(r.Builder.Relationships, e => e.Source.Id == handler.Id && e.Kind == RelationshipKind.CALLS && e.Target!.DeclarationId == "M:FrameworkFixture.IMessage.Read");
        Assert.Contains(r.Builder.Relationships, e => e.Kind == RelationshipKind.INJECTS && e.Confidence == Confidence.Inferred);
    }
    [Fact]
    public async Task ContextAndRelocationIdentitySeparateRoutesWithoutAbsolutePaths()
    {
        var original = await Fixture.Value;
        var relocated = await Analyze(root: "/relocated");
        var release = await Analyze(project: "release");
        var left = original.Entities.Values.Where(e => e.HttpEndpoint is not null).Select(e => e.Id).Order().ToArray();
        Assert.Equal(left, relocated.Entities.Values.Where(e => e.HttpEndpoint is not null).Select(e => e.Id).Order());
        Assert.Empty(left.Intersect(release.Entities.Values.Where(e => e.HttpEndpoint is not null).Select(e => e.Id)));
    }
    [Fact]
    public async Task BrokenBindingsDoNotPromoteRegistrationOrEndpointKeys()
    {
        var r = await Analyze(additional: """
            public static class Broken {
                public static void Use(Microsoft.Extensions.DependencyInjection.IServiceCollection services, Microsoft.AspNetCore.Builder.WebApplication app) {
                    services.AddSingleton<FrameworkFixture.IMessage, FrameworkFixture.Message>();
                    app.MapGet("/broken", () => "ok");
                    Missing();
                }
            }
            """);
        Assert.NotEmpty(r.Errors);
        var endpoint = Assert.Single(r.Entities.Values, e => e.HttpEndpoint is not null && e.Sources[0].File.EndsWith("Broken.cs", StringComparison.Ordinal));
        Assert.Null(endpoint.HttpEndpoint!.RoutePattern);
        Assert.Null(Assert.Single(r.Builder.Relationships, e => e.Source.Id == endpoint.Id && e.Kind == RelationshipKind.HANDLED_BY).Target);
    }
    [Theory]
    [InlineData("app.MapMethods(\"/unknown-methods\", methods, () => \"ok\");", "/unknown-methods", null)]
    [InlineData("app.MapGroup(prefix).MapPost(\"/child\", () => \"ok\");", null, "POST")]
    [InlineData("app.MapGet(prefix + \"/computed\", () => \"ok\");", null, "GET")]
    public async Task RuntimeRouteOrMethodInputsNeverBecomeConcreteKeys(string statement, string? route, string? method)
    {
        var r = await Analyze(additional: "public static class More { public static void Use(WebApplication app, string prefix, string[] methods) { " + statement + " } }");
        Assert.Empty(r.Errors);
        var e = Assert.Single(r.Entities.Values, e => e.HttpEndpoint is not null && e.Sources[0].File.EndsWith("Broken.cs", StringComparison.Ordinal));
        Assert.Equal(route, e.HttpEndpoint!.RoutePattern);
        Assert.Equal(method, e.HttpEndpoint.HttpMethod);
        Assert.Null(e.HttpEndpoint.CollisionKey);
        Assert.Equal(Confidence.Unresolved, e.Confidence);
    }
    [Fact]
    public async Task ConventionalAndInheritedRoutesAreUnresolvedAndIdenticalPrefixesStayDistinct()
    {
        var r = await Analyze(additional: """
            [Microsoft.AspNetCore.Mvc.Route("same"), Microsoft.AspNetCore.Mvc.Route("same")]
            public class TwiceController {
                [Microsoft.AspNetCore.Mvc.HttpGet("action")] public string Action() => "ok";
            }
            public class ConventionalController {
                [Microsoft.AspNetCore.Mvc.HttpGet] public string Action() => "ok";
            }
            [Microsoft.AspNetCore.Mvc.Route("base")]
            public class BaseController { }
            public class DerivedController : BaseController {
                [Microsoft.AspNetCore.Mvc.HttpGet("action")] public string Action() => "ok";
            }
            """);
        Assert.Empty(r.Errors);
        var added = r.Entities.Values.Where(e => e.HttpEndpoint is not null && e.Sources[0].File.EndsWith("Broken.cs", StringComparison.Ordinal)).ToArray();
        var duplicates = added.Where(e => e.Name == "GET /same/action").ToArray();
        Assert.Equal(2, duplicates.Length);
        Assert.NotEqual(duplicates[0].Id, duplicates[1].Id);
        Assert.Equal(2, added.Count(e => e.HttpEndpoint!.RoutePattern is null));
    }
    [Fact]
    public async Task GenericFactoryAndNonconstantServiceTypesDoNotInventImplementations()
    {
        var r = await Analyze(additional: """
            public static class More {
                public static void Use(IServiceCollection services, Type serviceType, Type implementationType) {
                    services.AddSingleton<FrameworkFixture.IMessage, FrameworkFixture.Message>(sp => new FrameworkFixture.Message());
                    services.AddTransient(serviceType, implementationType);
                }
            }
            """);
        Assert.Empty(r.Errors);
        var added = r.Entities.Values.Where(e => e.Registration is not null && e.Sources[0].File.EndsWith("Broken.cs", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, added.Length);
        Assert.All(added, e => Assert.Null(Assert.Single(r.Builder.Relationships, edge => edge.Source.Id == e.Id && edge.Kind == RelationshipKind.IMPLEMENTED_BY).Target));
        var unknownService = Assert.Single(added, e => e.Registration!.ServiceType is null);
        Assert.Null(Assert.Single(r.Builder.Relationships, e => e.Source.Id == unknownService.Id && e.Kind == RelationshipKind.REGISTERS).Target);
    }
    [Fact]
    public async Task ExternalBaseAndInheritedExclusionsCannotInventRoutes()
    {
        var r = await Analyze(additional: """
            public class ExternalBaseController : Exception {
                [Microsoft.AspNetCore.Mvc.HttpGet("external")] public string Action() => "ok";
            }
            [Microsoft.AspNetCore.Mvc.NonController]
            public class ExcludedBase { }
            public class ExcludedController : ExcludedBase {
                [Microsoft.AspNetCore.Mvc.HttpGet("excluded")] public string Action() => "ok";
            }
            public class NonActionBase {
                [Microsoft.AspNetCore.Mvc.NonAction] public virtual string Action() => "ok";
            }
            public class OverrideController : NonActionBase {
                [Microsoft.AspNetCore.Mvc.HttpGet("excluded-action")] public override string Action() => "ok";
            }
            """);
        Assert.Empty(r.Errors);
        var endpoint = Assert.Single(r.Entities.Values, e => e.HttpEndpoint is not null && e.Sources[0].File.EndsWith("Broken.cs", StringComparison.Ordinal));
        Assert.Null(endpoint.HttpEndpoint!.RoutePattern);
        Assert.Equal(Confidence.Unresolved, endpoint.Confidence);
    }
    private sealed record Result(Dictionary<string, GraphEntity> Entities, RelationshipBuilder Builder, Diagnostic[] Errors);
    private static async Task<Result> Analyze(string root = "/fixture", string project = "project", string? additional = null)
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "UO.Graphs.slnx"))) repo = repo.Parent;
        var code = File.ReadAllText(Path.Combine(repo!.FullName, "samples/Phase7/Frameworks.cs"));
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        var trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(code, parse, root + "/Frameworks.cs"),
            CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic; global using System.Linq; global using Microsoft.Extensions.DependencyInjection; global using Microsoft.AspNetCore.Builder;", parse, root + "/Usings.cs") };
        if (additional is not null) trees.Add(CSharpSyntaxTree.ParseText(additional, parse, root + "/Broken.cs"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(f => MetadataReference.CreateFromFile(f));
        var compilation = CSharpCompilation.Create("FrameworkFixture", trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var builder = new RelationshipBuilder("analysis");
        var entities = new Dictionary<string, GraphEntity>();
        var collector = new RelationshipCollector(compilation, project, root,
            new Dictionary<IAssemblySymbol, string>(SymbolEqualityComparer.Default) { [compilation.Assembly] = project }, builder);
        foreach (var tree in trees)
        {
            await SymbolCollector.CollectAsync(compilation, tree, project, root, false, entities, []);
            await collector.CollectAsync(tree, false);
        }
        FrameworkExtractors.Extract(new(compilation, project, root, trees.Select(t => (t, false)).ToArray(), collector, builder, entities, []));
        return new(entities, builder, compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray());
    }
}
