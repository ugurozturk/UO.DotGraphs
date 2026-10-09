using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UO.Graphs.Core;
using UO.Graphs.Roslyn;

namespace UO.Graphs.UnitTests;

public sealed class RelationshipTests
{
    private const string Caller = "M:SemanticFixture.Worker.Calls(SemanticFixture.IContract,SemanticFixture.Base)";
    private static readonly Lazy<Task<Result>> Valid = new(() => Analyze(false));
    private static readonly Lazy<Task<Result>> Broken = new(() => Analyze(true));

    [Fact]
    public void CentralContractCoversEveryKindAndPinsDirections()
    {
        var expected = new Dictionary<RelationshipKind, (string Source, string Target)>
        {
            [RelationshipKind.DECLARES] = ("container", "declaration"),
            [RelationshipKind.PROJECT_REFERENCES] = ("referencing project", "referenced project"),
            [RelationshipKind.INHERITS] = ("derived type", "direct base type or interface"),
            [RelationshipKind.IMPLEMENTS] = ("implementing type or member", "interface or interface member"),
            [RelationshipKind.OVERRIDES] = ("overriding member", "overridden member"),
            [RelationshipKind.USES_TYPE] = ("containing declaration", "bound type"),
            [RelationshipKind.REFERENCES] = ("containing declaration", "bound symbol"),
            [RelationshipKind.CALLS] = ("caller", "compile-time target"),
            [RelationshipKind.REGISTERS] = ("registration declaration", "service type"),
            [RelationshipKind.IMPLEMENTED_BY] = ("registration declaration", "implementation type"),
            [RelationshipKind.USES_FACTORY] = ("registration declaration", "factory method or lambda"),
            [RelationshipKind.INJECTS] = ("constructor or handler", "parameter service type"),
            [RelationshipKind.HANDLED_BY] = ("endpoint declaration", "action or handler"),
            [RelationshipKind.ACTIVATES] = ("controller action", "constructor candidate"),
            [RelationshipKind.HANDLES] = ("handler method", "message contract"),
            [RelationshipKind.CONSUMES] = ("consumer method", "message contract"),
            [RelationshipKind.DISPATCHES] = ("caller", "message contract"),
            [RelationshipKind.PUBLISHES] = ("caller", "message contract"),
            [RelationshipKind.POSSIBLE_TARGET] = ("message contract", "handler or consumer method"),
            [RelationshipKind.BEHAVIOR_FOR] = ("behavior method", "message contract"),
            [RelationshipKind.POSSIBLE_BEHAVIOR] = ("message contract", "behavior method"),
            [RelationshipKind.APP_SERVICE_CONTRACT] = ("application service", "application service interface"),
            [RelationshipKind.MODULE_DEPENDS_ON] = ("ABP module", "declared module dependency"),
            [RelationshipKind.REPOSITORY_ENTITY] = ("repository use or declaration", "entity type"),
            [RelationshipKind.CONTEXT_ENTITY] = ("DbContext or DbSet member", "entity type"),
            [RelationshipKind.CONFIGURES_ENTITY] = ("configuration class", "entity type"),
            [RelationshipKind.MAPS_ENTITY] = ("model mapping declaration", "entity type"),
            [RelationshipKind.HTTP_CONTRACT] = ("Refit interface method", "HTTP contract declaration"),
            [RelationshipKind.INVOKES_HTTP] = ("caller", "Refit HTTP contract declaration"),
            [RelationshipKind.POSSIBLE_ENDPOINT] = ("HTTP client contract", "endpoint candidate")
        };
        Assert.Equal(Enum.GetValues<RelationshipKind>().Length, expected.Count);
        Assert.Equal(expected.Count, RelationshipContract.Definitions.Count);
        foreach (var (kind, roles) in expected)
        {
            var actual = RelationshipContract.Definitions[kind];
            Assert.Equal(roles.Source, actual.SourceRole);
            Assert.Equal(roles.Target, actual.TargetRole);
            Assert.NotEmpty(actual.Meaning);
            Assert.Equal("\"" + kind + "\"", JsonSerializer.Serialize(kind, GraphJson.Options));
        }
    }

    [Fact]
    public async Task OverloadsGenericsExtensionsConstructorsAndExternalTargetsBindExactly()
    {
        var r = await Valid.Value;
        Assert.Empty(r.Errors);
        foreach (var target in new[]
        {
            "M:SemanticFixture.Worker.Overload(System.Int32)", "M:SemanticFixture.Worker.Overload(System.String)",
            "M:SemanticFixture.Worker.Generic``1(``0)", "M:SemanticFixture.Extensions.Extend(SemanticFixture.Worker)",
            "M:SemanticFixture.Worker.#ctor(System.Int32)", "M:System.Console.WriteLine(System.Int32)"
        }) Exact(r, RelationshipKind.CALLS, Caller, target);
        Absent(r, RelationshipKind.CALLS, Caller, "M:SemanticFixture.Worker.#ctor");
        Absent(r, RelationshipKind.CALLS, Caller, "M:System.Console.WriteLine(System.String)");
        var repeated = Exact(r, RelationshipKind.CALLS, Caller, "M:SemanticFixture.Worker.Overload(System.Int32)");
        var sites = r.Builder.Occurrences.Where(o => o.RelationshipId == repeated.Id).ToArray();
        Assert.Equal(2, sites.Length);
        Assert.NotEqual(sites[0].Source.StartLine, sites[1].Source.StartLine);
        Assert.NotEqual(sites[0].Id, sites[1].Id);
        Assert.Equal(new[] { "Overload(1)", "Overload(2)" }, sites.Select(o =>
            File.ReadAllLines(o.Source.File)[o.Source.StartLine - 1][(o.Source.StartColumn - 1)..(o.Source.EndColumn - 1)]).Order());
        Assert.All(sites, o => Assert.Equal(GraphIdentity.Hash(File.ReadAllText(o.Source.File)), o.Source.Sha256));
        var generic = Exact(r, RelationshipKind.CALLS, Caller, "M:SemanticFixture.Worker.Generic``1(``0)");
        Assert.Contains(r.Builder.Occurrences, o => o.RelationshipId == generic.Id && o.BoundTarget!.Contains("Generic<int>", StringComparison.Ordinal));
        var external = Exact(r, RelationshipKind.CALLS, Caller, "M:System.Console.WriteLine(System.Int32)");
        Assert.Null(external.Target!.ProjectId);
        Assert.Contains("System.Console", external.Target.AssemblyIdentity);
        Exact(r, RelationshipKind.CALLS, "M:SemanticFixture.Worker.#ctor", "M:SemanticFixture.Worker.#ctor(System.Int32)");
        Exact(r, RelationshipKind.CALLS, "M:SemanticFixture.Worker.FromOtherPart", "M:SemanticFixture.Worker.Overload(System.Int32)");
        Assert.Equal(2, Assert.Single(r.Entities.Values, e => e.DocumentationId == "T:SemanticFixture.Worker").Sources.Length);
    }

    [Fact]
    public async Task InterfaceAndVirtualTargetsAreSlotsAndNeverAllImplementations()
    {
        var r = await Valid.Value;
        Exact(r, RelationshipKind.CALLS, Caller, "M:SemanticFixture.IRoot.Run(System.Int32)");
        var virtualCall = Exact(r, RelationshipKind.CALLS, Caller, "M:SemanticFixture.Base.Virtual");
        Assert.Equal(2, r.Builder.Occurrences.Count(o => o.RelationshipId == virtualCall.Id));
        var concreteCall = Exact(r, RelationshipKind.CALLS, Caller, "M:SemanticFixture.Worker.Virtual");
        var concreteSite = Assert.Single(r.Builder.Occurrences, o => o.RelationshipId == concreteCall.Id);
        Assert.Contains("new Worker(1).Virtual()", File.ReadAllLines(concreteSite.Source.File)[concreteSite.Source.StartLine - 1]);
        Absent(r, RelationshipKind.CALLS, Caller, "M:SemanticFixture.Worker.SemanticFixture#IRoot#Run(System.Int32)");
        Absent(r, RelationshipKind.CALLS, Caller, "M:SemanticFixture.Worker.Run(System.String)");
        Exact(r, RelationshipKind.INHERITS, "T:SemanticFixture.Worker", "T:SemanticFixture.Base");
        Exact(r, RelationshipKind.INHERITS, "T:SemanticFixture.IContract", "T:SemanticFixture.IRoot");
        Exact(r, RelationshipKind.IMPLEMENTS, "T:SemanticFixture.Worker", "T:SemanticFixture.IContract");
        Exact(r, RelationshipKind.IMPLEMENTS, "M:SemanticFixture.Worker.SemanticFixture#IRoot#Run(System.Int32)", "M:SemanticFixture.IRoot.Run(System.Int32)");
        Absent(r, RelationshipKind.IMPLEMENTS, "M:SemanticFixture.Worker.Run(System.String)", "M:SemanticFixture.IRoot.Run(System.Int32)");
        Exact(r, RelationshipKind.OVERRIDES, "M:SemanticFixture.Worker.Virtual", "M:SemanticFixture.Base.Virtual");
        Exact(r, RelationshipKind.OVERRIDES, "P:SemanticFixture.Worker.Number", "P:SemanticFixture.Base.Number");
        Absent(r, RelationshipKind.INHERITS, "T:SemanticFixture.Base", "T:SemanticFixture.Worker");
        Absent(r, RelationshipKind.OVERRIDES, "M:SemanticFixture.Base.Virtual", "M:SemanticFixture.Worker.Virtual");
        Assert.DoesNotContain(r.Builder.Relationships, e => e.Confidence == Confidence.Inferred);
    }

    [Fact]
    public async Task AccessorsMethodGroupsLambdaAndLocalOwnersHaveDistinctMeaning()
    {
        var r = await Valid.Value;
        var get = Exact(r, RelationshipKind.CALLS, Caller, "accessor:P:SemanticFixture.Worker.Value:PropertyGet");
        var set = Exact(r, RelationshipKind.CALLS, Caller, "accessor:P:SemanticFixture.Worker.Value:PropertySet");
        Assert.Equal(2, r.Builder.Occurrences.Count(o => o.RelationshipId == get.Id));
        Assert.Equal(2, r.Builder.Occurrences.Count(o => o.RelationshipId == set.Id));
        Exact(r, RelationshipKind.REFERENCES, Caller, "M:SemanticFixture.Worker.Callback");
        Absent(r, RelationshipKind.CALLS, Caller, "M:SemanticFixture.Worker.Callback");
        Absent(r, RelationshipKind.CALLS, Caller, "M:SemanticFixture.Worker.LambdaOnly");
        Absent(r, RelationshipKind.CALLS, Caller, "M:SemanticFixture.Worker.LocalOnly");
        var lambda = Assert.Single(r.Entities.Values, e => e.Kind == "lambda");
        var local = Assert.Single(r.Entities.Values, e => e.Kind == "localFunction");
        Exact(r, RelationshipKind.CALLS, lambda.DocumentationId, "M:SemanticFixture.Worker.LambdaOnly");
        Exact(r, RelationshipKind.CALLS, local.DocumentationId, "M:SemanticFixture.Worker.LocalOnly");
        Exact(r, RelationshipKind.CALLS, Caller, local.DocumentationId);
        Absent(r, RelationshipKind.CALLS, Caller, lambda.DocumentationId);
        var unresolved = Assert.Single(r.Builder.Relationships, e => e.Kind == RelationshipKind.CALLS && e.Source.DeclarationId == Caller && e.Confidence == Confidence.Unresolved);
        Assert.Null(unresolved.Target);
        Assert.Equal(2, r.Builder.Occurrences.Count(o => o.RelationshipId == unresolved.Id));
        Exact(r, RelationshipKind.USES_TYPE, Caller, "T:SemanticFixture.IContract");
        Exact(r, RelationshipKind.REFERENCES, Caller, "P:SemanticFixture.Worker.Value");
        string[] expectedCalls = [
            "M:SemanticFixture.Worker.Overload(System.Int32)", "M:SemanticFixture.Worker.Overload(System.String)",
            "M:SemanticFixture.Worker.Generic``1(``0)", "M:SemanticFixture.Extensions.Extend(SemanticFixture.Worker)",
            "M:SemanticFixture.IRoot.Run(System.Int32)", "M:SemanticFixture.Base.Virtual", "M:SemanticFixture.Worker.Virtual",
            "M:SemanticFixture.Worker.#ctor(System.Int32)", "accessor:P:SemanticFixture.Worker.Value:PropertyGet",
            "accessor:P:SemanticFixture.Worker.Value:PropertySet", local.DocumentationId, "M:System.Console.WriteLine(System.Int32)"
        ];
        Assert.Equal(expectedCalls.Order(StringComparer.Ordinal), r.Builder.Relationships
            .Where(e => e.Kind == RelationshipKind.CALLS && e.Source.DeclarationId == Caller && e.Confidence == Confidence.Exact)
            .Select(e => e.Target!.DeclarationId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task DynamicReflectionAndBrokenBindingDoNotInventExactTargets()
    {
        var valid = await Valid.Value;
        var dynamic = Assert.Single(valid.Builder.Relationships, e => e.Kind == RelationshipKind.CALLS && e.Source.DeclarationId == "M:SemanticFixture.Worker.Unknown(System.Object)");
        Assert.Equal(Confidence.Unresolved, dynamic.Confidence);
        Assert.Null(dynamic.Target);
        var reflected = valid.Builder.Relationships.Where(e => e.Kind == RelationshipKind.CALLS && e.Source.DeclarationId == "M:SemanticFixture.Worker.Reflected(System.Reflection.MethodInfo)").ToArray();
        Assert.Equal(2, reflected.Length);
        Assert.Single(reflected, e => e.Confidence == Confidence.Exact && e.Target!.DeclarationId == "M:System.Reflection.MethodBase.Invoke(System.Object,System.Object[])");
        Assert.Single(reflected, e => e.Confidence == Confidence.Unresolved && e.Target is null);
        var broken = await Broken.Value;
        Assert.NotEmpty(broken.Errors);
        foreach (var owner in new[] { "M:SemanticFixture.Worker.Broken", "M:SemanticFixture.Worker.Ambiguous" })
        {
            Assert.DoesNotContain(broken.Builder.Relationships, e => e.Source.DeclarationId == owner && e.Confidence == Confidence.Exact);
            var unresolved = Assert.Single(broken.Builder.Relationships, e => e.Source.DeclarationId == owner && e.Kind == RelationshipKind.CALLS);
            Assert.Equal(Confidence.Unresolved, unresolved.Confidence);
            Assert.Null(unresolved.Target);
        }
        // A broken member does not poison an unrelated, successfully bound member.
        Exact(broken, RelationshipKind.CALLS, "M:SemanticFixture.Worker.FromOtherPart", "M:SemanticFixture.Worker.Overload(System.Int32)");
    }

    [Fact]
    public async Task CompileTimeNameofDelegateCreationAndAccessorWritesDoNotInventCalls()
    {
        var r = await Analyze(false, """
            namespace Additional;
            public class Accessors
            {
                private int backing;
                public int Property { get; set; }
                public int this[int index] { get => backing; set => backing = value; }
                public ref int Ref => ref backing;
                public void Target() { }
                public void Use()
                {
                    this[0] = 1;
                    this[0] += 2;
                    var read = this[0];
                    Ref = 3;
                    var name = nameof(Property);
                    System.Action group = new System.Action(Target);
                    group();
                    (Property, backing) = (1, 2);
                }
                public void DynamicConstructor(dynamic argument) => _ = new SemanticFixture.Worker(argument);
            }
            """);
        Assert.Empty(r.Errors);
        const string owner = "M:Additional.Accessors.Use";
        var get = Exact(r, RelationshipKind.CALLS, owner, "accessor:P:Additional.Accessors.Item(System.Int32):PropertyGet");
        var set = Exact(r, RelationshipKind.CALLS, owner, "accessor:P:Additional.Accessors.Item(System.Int32):PropertySet");
        Assert.Equal(2, r.Builder.Occurrences.Count(o => o.RelationshipId == get.Id));
        Assert.Equal(2, r.Builder.Occurrences.Count(o => o.RelationshipId == set.Id));
        Exact(r, RelationshipKind.CALLS, owner, "accessor:P:Additional.Accessors.Ref:PropertyGet");
        Absent(r, RelationshipKind.CALLS, owner, "accessor:P:Additional.Accessors.Ref:PropertySet");
        Absent(r, RelationshipKind.CALLS, owner, "accessor:P:Additional.Accessors.Property:PropertyGet");
        Absent(r, RelationshipKind.CALLS, owner, "accessor:P:Additional.Accessors.Property:PropertySet");
        Absent(r, RelationshipKind.CALLS, owner, "M:Additional.Accessors.Target");
        Exact(r, RelationshipKind.REFERENCES, owner, "M:Additional.Accessors.Target");
        var dynamic = Assert.Single(r.Builder.Relationships, e => e.Kind == RelationshipKind.CALLS &&
            e.Source.DeclarationId == "M:Additional.Accessors.DynamicConstructor(System.Object)");
        Assert.Null(dynamic.Target);
        Assert.Equal(Confidence.Unresolved, dynamic.Confidence);
    }

    [Fact]
    public async Task EveryOccurrenceRetainsContextHashSpanAndRule()
    {
        var r = await Valid.Value;
        Assert.All(r.Builder.Occurrences, o =>
        {
            Assert.Equal("analysis", o.AnalysisId);
            Assert.Equal("project", o.ContextId);
            Assert.NotEmpty(o.Reason);
            Assert.Equal(64, o.Source.Sha256.Length);
            Assert.True(o.Source.StartLine > 0 && o.Source.StartColumn > 0);
            Assert.True(o.Source.EndLine >= o.Source.StartLine);
            Assert.Contains(r.Builder.Relationships, e => e.Id == o.RelationshipId);
        });
    }

    private static GraphRelationship Exact(Result result, RelationshipKind kind, string source, string target) =>
        Assert.Single(result.Builder.Relationships, r => r.Kind == kind && r.Source.DeclarationId == source && r.Target?.DeclarationId == target && r.Confidence == Confidence.Exact);
    private static void Absent(Result result, RelationshipKind kind, string source, string target) =>
        Assert.DoesNotContain(result.Builder.Relationships, r => r.Kind == kind && r.Source.DeclarationId == source && r.Target?.DeclarationId == target);

    private static async Task<Result> Analyze(bool broken, string? additionalSource = null)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "UO.Graphs.slnx"))) root = root.Parent;
        var path = Path.Combine(root!.FullName, "samples/Phase3");
        var trees = Directory.GetFiles(path, "*.cs").Order().Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f),
            new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: broken ? ["BROKEN_SCENARIO"] : []), f)).ToArray();
        if (additionalSource is not null) trees = [.. trees, CSharpSyntaxTree.ParseText(additionalSource,
            new CSharpParseOptions(LanguageVersion.CSharp14), Path.Combine(path, "Additional.cs"))];
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(f => MetadataReference.CreateFromFile(f));
        var compilation = CSharpCompilation.Create("SemanticFixture", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var builder = new RelationshipBuilder("analysis");
        var entities = new Dictionary<string, GraphEntity>();
        var collector = new RelationshipCollector(compilation, "project", path,
            new Dictionary<IAssemblySymbol, string>(SymbolEqualityComparer.Default) { [compilation.Assembly] = "project" }, builder);
        foreach (var tree in trees)
        {
            await SymbolCollector.CollectAsync(compilation, tree, "project", path, false, entities, []);
            await collector.CollectAsync(tree, false);
        }
        return new(builder, entities, compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray());
    }
    private sealed record Result(RelationshipBuilder Builder, Dictionary<string, GraphEntity> Entities, Diagnostic[] Errors);
}
