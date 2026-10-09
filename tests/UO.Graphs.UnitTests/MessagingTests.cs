using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UO.Graphs.Core;
using UO.Graphs.Roslyn;

namespace UO.Graphs.UnitTests;

public sealed class MessagingTests
{
    private static readonly string Root = FindRoot();
    private static readonly string Cache = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages");
    private static readonly Lazy<Task<Result>> Uo = new(() => Analyze(File.ReadAllText(Path.Combine(Root, "samples/Phase8/Uo/Flows.cs")))) ;
    private static readonly Lazy<Task<Result>> Packages = new(() => Analyze(File.ReadAllText(Path.Combine(Root, "samples/Phase8/Contracts/Flows.cs"))));

    [Fact]
    public async Task UoRealPackageResponseAndNoResponseContractsAndNestedDispatchAreBound()
    {
        var r = await Uo.Value;
        Assert.Empty(r.Errors);
        var a = Message(r, "uoMediator", "UoFlows.RequestA");
        var b = Message(r, "uoMediator", "UoFlows.RequestB");
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.DISPATCHES && e.Source.DisplayName.Contains("Service.Run") && e.Target?.Id == a.Id && e.Confidence == Confidence.Exact);
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.DISPATCHES && e.Source.DisplayName.Contains("HandlerA.HandleAsync") && e.Target?.Id == b.Id);
        Assert.Equal(2, r.Edges.Count(e => e.Kind == RelationshipKind.HANDLES && e.Target?.Id == b.Id));
        Assert.DoesNotContain(r.Edges, e => e.Kind == RelationshipKind.PUBLISHES);
    }

    [Fact]
    public async Task ConstructedGenericMessagesDoNotCollapseAndOpenBehaviorsStayUnresolved()
    {
        var r = await Uo.Value;
        var a = Message(r, "uoMediator", "UoFlows.GenericRequest<int>");
        var b = Message(r, "uoMediator", "UoFlows.GenericRequest<string>");
        Assert.NotEqual(a.Id, b.Id);
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.POSSIBLE_TARGET && e.Source.Id == a.Id && e.Target?.DisplayName.Contains("IntHandler") == true);
        Assert.DoesNotContain(r.Edges, e => e.Kind == RelationshipKind.POSSIBLE_TARGET && e.Source.Id == a.Id && e.Target?.DisplayName.Contains("StringHandler") == true);
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.BEHAVIOR_FOR && e.Source.DisplayName.Contains("GenericBehavior") && e.Target is null);
    }

    [Fact]
    public async Task RegistrationAndPipelineEvidenceNeverGuaranteesRuntimeSelection()
    {
        var r = await Uo.Value;
        Assert.Contains(r.Builder.Occurrences, o => o.Reason.Contains("Bound Microsoft DI registration") && o.Reason.Contains("conditional=True"));
        Assert.All(r.Edges.Where(e => e.Kind is RelationshipKind.POSSIBLE_TARGET or RelationshipKind.POSSIBLE_BEHAVIOR), e => Assert.NotEqual(Confidence.Exact, e.Confidence));
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.POSSIBLE_BEHAVIOR && e.Target?.DisplayName.Contains("AuditBehavior") == true && e.Confidence == Confidence.Inferred);
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.DISPATCHES && e.Source.DisplayName.Contains("Service.Unknown") && e.Target is null);
        var missing = Message(r, "uoMediator", "UoFlows.Missing");
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.POSSIBLE_TARGET && e.Source.Id == missing.Id && e.Target is null);
        Assert.Contains("do not prove absence", missing.Message!.Scope);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("massTransit")]
    public async Task RealPackagesPublishOneEventWithMultipleConsumersAndSeparateFrameworks(string framework)
    {
        var r = await Packages.Value;
        Assert.Empty(r.Errors);
        var message = Message(r, framework, "ContractFlows.Event");
        Assert.Equal(2, r.Edges.Count(e => e.Kind == RelationshipKind.CONSUMES && e.Target?.Id == message.Id));
        Assert.Equal(2, r.Edges.Count(e => e.Kind == RelationshipKind.POSSIBLE_TARGET && e.Source.Id == message.Id && e.Target is not null));
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.PUBLISHES && e.Target?.Id == message.Id);
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.POSSIBLE_TARGET && e.Source.Id == message.Id && e.Target is null);
        Assert.All(r.Builder.Occurrences, o => { Assert.Equal("analysis", o.AnalysisId); Assert.Equal(64, o.Source.Sha256.Length); });
    }

    [Fact]
    public async Task RealPackagesNoResponseNotificationPipelineAndUnknownTypeOverloadsAreDistinct()
    {
        var r = await Packages.Value;
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.HANDLES && e.Source.DisplayName.Contains("CommandHandler"));
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.BEHAVIOR_FOR && e.Source.DisplayName.Contains("Behavior.Handle"));
        var unknown = r.Edges.Where(e => e.Source.DisplayName.Contains("Dispatch.Unknown") && e.Target is null && e.Kind is RelationshipKind.DISPATCHES or RelationshipKind.PUBLISHES).Select(e => e.Id).ToHashSet();
        Assert.Equal(2, unknown.Count);
        Assert.Equal(3, r.Builder.Occurrences.Count(o => unknown.Contains(o.RelationshipId)));
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.PUBLISHES && e.Source.DisplayName.Contains("Dispatch.Explicit") && e.Target?.Id == Message(r, "massTransit", "ContractFlows.Event").Id);
        Assert.Contains(r.Edges, e => e.Kind == RelationshipKind.PUBLISHES && e.Target?.Id == Message(r, "massTransit", "ContractFlows.IEvent").Id);
        Assert.DoesNotContain(r.Edges, e => e.Source.DisplayName.Contains("Lookalike") && e.Kind is RelationshipKind.DISPATCHES or RelationshipKind.PUBLISHES);
    }

    [Fact]
    public async Task SameSimpleNameDifferentNamespaceCannotCrossBind()
    {
        var r = await Packages.Value;
        var other = Message(r, "massTransit", "Other.Event");
        Assert.Single(r.Edges, e => e.Kind == RelationshipKind.CONSUMES && e.Target?.Id == other.Id);
        Assert.DoesNotContain(r.Edges, e => e.Kind == RelationshipKind.POSSIBLE_TARGET && e.Source.Id == other.Id && e.Target?.DisplayName.Contains("Consumer1") == true);
    }

    [Fact]
    public async Task SameQualifiedMessageNameInDifferentAssembliesCannotCrossBind()
    {
        var first = Reference("First", "namespace Shared; public sealed class Event { }").WithAliases(ImmutableArray.Create("first"));
        var second = Reference("Second", "namespace Shared; public sealed class Event { }").WithAliases(ImmutableArray.Create("second"));
        var r = await Analyze("""
            extern alias first;
            extern alias second;
            public sealed class One : MassTransit.IConsumer<first::Shared.Event> {
                public System.Threading.Tasks.Task Consume(MassTransit.ConsumeContext<first::Shared.Event> context) => System.Threading.Tasks.Task.CompletedTask;
            }
            public sealed class Two : MassTransit.IConsumer<second::Shared.Event> {
                public System.Threading.Tasks.Task Consume(MassTransit.ConsumeContext<second::Shared.Event> context) => System.Threading.Tasks.Task.CompletedTask;
            }
            public static class Senders {
                public static System.Threading.Tasks.Task Send(MassTransit.IPublishEndpoint bus) => bus.Publish(new first::Shared.Event());
            }
            """, extra: [first, second]);
        Assert.Empty(r.Errors);
        var messages = r.Entities.Values.Where(e => e.Message?.ConstructedType == "Shared.Event").ToArray();
        Assert.Equal(2, messages.Length);
        Assert.NotEqual(messages[0].Id, messages[1].Id);
        var sent = Assert.Single(r.Edges, e => e.Kind == RelationshipKind.PUBLISHES).Target!;
        var target = Assert.Single(r.Edges, e => e.Kind == RelationshipKind.POSSIBLE_TARGET && e.Source.Id == sent.Id && e.Target is not null);
        Assert.Contains("One.Consume", target.Target!.DisplayName);
    }

    [Fact]
    public async Task ContractFixturesAreSeparateFromRealPackageCompatibilityButGiveSameFlowShapes()
    {
        var real = await Packages.Value;
        var fixture = await Analyze(File.ReadAllText(Path.Combine(Root, "samples/Phase8/Contracts/Flows.cs")), contractFixture: true);
        Assert.Empty(fixture.Errors);
        static string[] Shapes(Result r) => r.Edges.Where(e => e.Kind is RelationshipKind.HANDLES or RelationshipKind.CONSUMES or RelationshipKind.PUBLISHES or RelationshipKind.DISPATCHES or RelationshipKind.BEHAVIOR_FOR)
            .Select(e => $"{e.Kind}|{e.Source.DisplayName}|{e.Target?.DisplayName}|{e.Confidence}").Order().ToArray();
        Assert.Equal(Shapes(real), Shapes(fixture));
    }

    [Fact]
    public async Task BrokenCompilationCannotCreateExactDispatchOrCandidates()
    {
        var r = await Analyze("""
            public static class Broken {
                public static async System.Threading.Tasks.Task Run(MassTransit.IPublishEndpoint bus) {
                    await bus.Publish(new Event()); Missing();
                }
            }
            public sealed class Event { }
            """);
        Assert.NotEmpty(r.Errors);
        Assert.Null(Assert.Single(r.Edges, e => e.Kind == RelationshipKind.PUBLISHES).Target);
    }

    private sealed record Result(Dictionary<string, GraphEntity> Entities, RelationshipBuilder Builder, Diagnostic[] Errors)
    { public GraphRelationship[] Edges => Builder.Relationships; }
    private static GraphEntity Message(Result r, string framework, string type) => Assert.Single(r.Entities.Values, e => e.Message?.Framework == framework && e.Message.ConstructedType == type);
    private static IEnumerable<MetadataReference> Platform() => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(f => MetadataReference.CreateFromFile(f));
    private static PortableExecutableReference Reference(string assembly, string source)
    {
        var compilation = CSharpCompilation.Create(assembly, [CSharpSyntaxTree.ParseText(source)], Platform(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        Assert.True(compilation.Emit(stream).Success);
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
    private static async Task<Result> Analyze(string source, bool contractFixture = false, MetadataReference[]? extra = null)
    {
        var references = Platform().ToList();
        references.Add(MetadataReference.CreateFromFile(Path.Combine(Cache, "uo.mediator.shared/2.4.2/lib/net10.0/UO.Mediator.Shared.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(Cache, "mediatr.contracts/2.0.1/lib/netstandard2.0/MediatR.Contracts.dll")));
        foreach (var (folder, file, package) in new[] { ("MediatR", "MediatR.dll", "mediatr/12.4.1/lib/net6.0/MediatR.dll"), ("MassTransit", "MassTransit.Abstractions.dll", "masstransit.abstractions/8.3.6/lib/net9.0/MassTransit.Abstractions.dll") })
            references.Add(MetadataReference.CreateFromFile(contractFixture ? Path.Combine(Root, "samples/Phase8/ContractFixtures", folder, "bin/Debug/net10.0", file) : Path.Combine(Cache, package)));
        references.AddRange(extra ?? []);
        var trees = new[] { CSharpSyntaxTree.ParseText(source, path: "/fixture/Flows.cs"), CSharpSyntaxTree.ParseText("global using System; global using System.Threading; global using System.Threading.Tasks;", path: "/fixture/Usings.cs") };
        var compilation = CSharpCompilation.Create("MessagingFixture", trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var entities = new Dictionary<string, GraphEntity>();
        var builder = new RelationshipBuilder("analysis");
        var collector = new RelationshipCollector(compilation, "project", "/fixture", new Dictionary<IAssemblySymbol, string>(SymbolEqualityComparer.Default) { [compilation.Assembly] = "project" }, builder);
        foreach (var tree in trees)
        {
            await SymbolCollector.CollectAsync(compilation, tree, "project", "/fixture", false, entities, []);
            await collector.CollectAsync(tree, false);
        }
        FrameworkExtractors.Extract(new(compilation, "project", "/fixture", trees.Select(t => (t, false)).ToArray(), collector, builder, entities, []));
        return new(entities, builder, compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray());
    }
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UO.Graphs.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
}
