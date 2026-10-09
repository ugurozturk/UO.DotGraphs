using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using UO.Graphs.Core;

namespace UO.Graphs.Roslyn;

// Deliberately internal and synchronous: no discovery, loading, host execution or storage access.
internal interface IFrameworkExtractor
{
    string Id { get; }
    void Extract(FrameworkExtractionContext context);
}

internal static class FrameworkExtractors
{
    internal static void Extract(FrameworkExtractionContext context)
    {
        IFrameworkExtractor[] modules = [new MicrosoftDiExtractor(), new AspNetCoreExtractor(), new MessagingExtractor(), new AbpExtractor(), new EfCoreExtractor(), new RefitExtractor()];
        foreach (var module in modules) module.Extract(context);
    }
}

internal sealed class FrameworkExtractionContext(Compilation compilation, string projectId, string rootPath,
    IReadOnlyList<(SyntaxTree Tree, bool Generated)> documents, RelationshipCollector resolver,
    RelationshipBuilder relationships, Dictionary<string, GraphEntity> entities, List<string> diagnostics)
{
    public Compilation Compilation { get; } = compilation;
    public string ProjectId { get; } = projectId;
    public IReadOnlyList<(SyntaxTree Tree, bool Generated)> Documents { get; } = documents;
    public Dictionary<string, GraphEntity> Entities { get; } = entities;
    public HashSet<ITypeSymbol> RegisteredServices { get; } = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<SyntaxTree, string> hashes = [];
    private readonly Diagnostic[] errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    public SemanticModel Model(SyntaxNode node) => Compilation.GetSemanticModel(node.SyntaxTree);
    public RelationshipSymbol Resolve(ISymbol symbol, SyntaxNode node) => resolver.Resolve(symbol, node);
    public void EmitSymbol(RelationshipKind kind, RelationshipSymbol source, RelationshipSymbol? target,
        SyntaxNode node, string reason, Confidence confidence = Confidence.Exact)
    {
        var invalid = Invalid(node);
        relationships.Add(kind, source, invalid ? null : target, ProjectId,
            invalid || target is null ? Confidence.Unresolved : confidence, Evidence(node),
            invalid ? "Compilation error; target not asserted. " + reason : reason,
            target?.DisplayName, invalid && target is not null ? [target] : []);
    }
    public static RelationshipSymbol Symbol(GraphEntity entity) => new(entity.Id, entity.DocumentationId,
        entity.QualifiedName, "", entity.ProjectId, entity.Id);
    public SourceEvidence Evidence(SyntaxNode node)
    {
        if (!hashes.TryGetValue(node.SyntaxTree, out var hash)) hashes[node.SyntaxTree] = hash = GraphIdentity.Hash(node.SyntaxTree.GetText().ToString());
        var span = node.GetLocation().GetLineSpan();
        return new(node.SyntaxTree.FilePath, span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1,
            span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1, hash,
            Documents.Any(d => d.Tree == node.SyntaxTree && d.Generated));
    }
    public bool Invalid(SyntaxNode node)
    {
        var scope = node.AncestorsAndSelf().FirstOrDefault(n => n is BaseMethodDeclarationSyntax
            or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax) ?? node;
        return errors.Any(d => d.Location.SourceTree == node.SyntaxTree && d.Location.SourceSpan.IntersectsWith(scope.Span));
    }
    public void Emit(RelationshipKind kind, RelationshipSymbol source, ISymbol? target, SyntaxNode node,
        string reason, Confidence confidence = Confidence.Exact)
    {
        var invalid = Invalid(node) || target is ITypeSymbol { TypeKind: TypeKind.Error };
        relationships.Add(kind, source, target is null || invalid ? null : Resolve(target, node), ProjectId,
            target is null || invalid ? Confidence.Unresolved : confidence, Evidence(node),
            invalid ? "Compilation error; target not asserted. " + reason : reason, target?.ToDisplayString(),
            invalid && target is not null ? [Resolve(target, node)] : []);
    }
    public string Site(SyntaxNode node)
    {
        var path = new List<int>();
        for (var n = node; n.Parent is { } parent; n = parent)
            path.Add(parent.ChildNodes().TakeWhile(c => c != n).Count());
        path.Reverse();
        return GraphIdentity.Canonical(Path.GetRelativePath(rootPath, node.SyntaxTree.FilePath).Replace('\\', '/'), string.Join('.', path));
    }
    public GraphEntity AddNode(string kind, string name, SyntaxNode node, params string[] identity)
    {
        var canonical = GraphIdentity.Canonical([kind, ProjectId, .. identity, Site(node)]);
        var entity = new GraphEntity(GraphIdentity.Hash(canonical), kind, name, name, "", ProjectId,
            Invalid(node) ? Confidence.Unresolved : Confidence.Exact, [Evidence(node)], canonical, ProjectId);
        Entities[entity.Id] = entity;
        return entity;
    }
    public void Report(string code, SyntaxNode node, string message) => diagnostics.Add(
        $"framework:{code}: {node.SyntaxTree.FilePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {message}");
    public IEnumerable<IInvocationOperation> Invocations() => Documents.SelectMany(d => d.Tree.GetRoot().DescendantNodes()
        .OfType<InvocationExpressionSyntax>().Select(n => Model(n).GetOperation(n)).OfType<IInvocationOperation>());
    public static IOperation? Unwrap(IOperation? operation) => operation switch
    {
        IConversionOperation c => Unwrap(c.Operand),
        IDelegateCreationOperation d => Unwrap(d.Target),
        IParenthesizedOperation p => Unwrap(p.Operand),
        _ => operation
    };
    public static IMethodSymbol? Handler(IOperation? operation) => Unwrap(operation) switch
    {
        IAnonymousFunctionOperation a => a.Symbol, IMethodReferenceOperation m => m.Method, _ => null
    };
    public static IOperation? Argument(IInvocationOperation call, string name) =>
        call.Arguments.FirstOrDefault(a => a.Parameter?.Name == name)?.Value;
    public static bool Conditional(SyntaxNode node) => node.Ancestors().Any(n => n is IfStatementSyntax
        or SwitchStatementSyntax or SwitchExpressionSyntax or ConditionalExpressionSyntax or ForStatementSyntax
        or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax or CatchClauseSyntax or ConditionalAccessExpressionSyntax
        || n.IsKind(SyntaxKind.LogicalAndExpression) || n.IsKind(SyntaxKind.LogicalOrExpression) || n.IsKind(SyntaxKind.CoalesceExpression));
    public static bool FrameworkType(INamedTypeSymbol? type, string metadataName, string assembly) =>
        type?.ToDisplayString() == metadataName && type.ContainingAssembly.Name == assembly &&
        !type.Locations.Any(l => l.IsInSource);
    public static bool Attribute(AttributeData attribute, string name) => FrameworkType(attribute.AttributeClass,
        "Microsoft.AspNetCore.Mvc." + name, "Microsoft.AspNetCore.Mvc.Core") || FrameworkType(attribute.AttributeClass,
        "Microsoft.AspNetCore.Mvc." + name, "Microsoft.AspNetCore.Mvc.Abstractions");
}
