using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using UO.Graphs.Core;

namespace UO.Graphs.Roslyn;

internal static class SymbolCollector
{
    internal static async Task CollectAsync(Compilation compilation, SyntaxTree tree, string projectId,
        string rootPath, bool generated, Dictionary<string, GraphEntity> entities, List<string> diagnostics)
    {
        var model = compilation.GetSemanticModel(tree);
        var root = await tree.GetRootAsync();
        var hash = GraphIdentity.Hash((await tree.GetTextAsync()).ToString());
        foreach (var node in root.DescendantNodes().Where(n => n is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax
                     or BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax or VariableDeclaratorSyntax
                     or EnumMemberDeclarationSyntax or BaseNamespaceDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax or AnonymousFunctionExpressionSyntax))
        {
            var symbol = node is AnonymousFunctionExpressionSyntax ? (model.GetOperation(node) as Microsoft.CodeAnalysis.Operations.IAnonymousFunctionOperation)?.Symbol : model.GetDeclaredSymbol(node);
            if (symbol is not (INamedTypeSymbol or IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol or INamespaceSymbol)) continue;
            if (symbol is IFieldSymbol { IsImplicitlyDeclared: true }) continue;
            var docId = DeclarationKey(symbol, node, rootPath);
            if (docId is null)
            {
                diagnostics.Add($"symbol: unresolved identity: {tree.FilePath}: {node.Kind()}");
                continue;
            }
            Add(symbol, docId, node);
            if (symbol is INamedTypeSymbol type && node is TypeDeclarationSyntax { ParameterList: not null })
            {
                foreach (var constructor in type.InstanceConstructors.Where(c => c.DeclaringSyntaxReferences.Any(r =>
                             r.SyntaxTree == node.SyntaxTree && r.Span == node.Span)))
                    Add(constructor, DeclarationKey(constructor, node, rootPath)!, node);
            }
            // Expression-bodied and automatic accessors have no independent declaration node.
            if (symbol is IPropertySymbol property)
            {
                if (property.GetMethod is { } get) Add(get, DeclarationKey(get, node, rootPath)!, node);
                if (property.SetMethod is { } set) Add(set, DeclarationKey(set, node, rootPath)!, node);
            }
            if (symbol is IEventSymbol evt)
            {
                if (evt.AddMethod is { } add) Add(add, DeclarationKey(add, node, rootPath)!, node);
                if (evt.RemoveMethod is { } remove) Add(remove, DeclarationKey(remove, node, rootPath)!, node);
            }
        }

        void Add(ISymbol symbol, string key, SyntaxNode node)
        {
            var id = GraphIdentity.Symbol(projectId, key);
            var span = node.GetLocation().GetLineSpan();
            var evidence = new SourceEvidence(tree.FilePath,
                span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1,
                span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1, hash, generated);
            var parentKey = symbol.ContainingSymbol is { } parent ? DeclarationKey(parent, node, rootPath) : null;
            var parentId = parentKey is null ? GraphIdentity.Symbol(projectId, "A:" + compilation.Assembly.Identity) : GraphIdentity.Symbol(projectId, parentKey);
            var kind = symbol switch
            {
                INamespaceSymbol => "namespace", INamedTypeSymbol => "type", IPropertySymbol => "property",
                IFieldSymbol => "field", IEventSymbol => "event",
                IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } => "constructor",
                IMethodSymbol { MethodKind: MethodKind.LocalFunction } => "localFunction",
                IMethodSymbol { MethodKind: MethodKind.AnonymousFunction } => "lambda",
                IMethodSymbol { AssociatedSymbol: not null } => "accessor", _ => "method"
            };
            if (entities.TryGetValue(id, out var existing))
                entities[id] = existing with { Sources = existing.Sources.Append(evidence).Distinct().ToArray() };
            else entities.Add(id, new(id, kind, symbol.Name, symbol.ToDisplayString(), key, projectId,
                Confidence.Exact, [evidence], GraphIdentity.Canonical("symbol", projectId, key), parentId));
            // Include implicit namespace ancestors, including the global namespace.
            if (symbol.ContainingSymbol is INamespaceSymbol ns)
            {
                while (true)
                {
                    var nsKey = ns.GetDocumentationCommentId() ?? "N:";
                    var nsId = GraphIdentity.Symbol(projectId, nsKey);
                    entities.TryAdd(nsId, new(nsId, "namespace", ns.IsGlobalNamespace ? "<global>" : ns.Name,
                        ns.ToDisplayString(), nsKey, projectId, Confidence.Exact, [], GraphIdentity.Canonical("symbol", projectId, nsKey),
                        ns.IsGlobalNamespace ? GraphIdentity.Symbol(projectId, "A:" + compilation.Assembly.Identity)
                            : GraphIdentity.Symbol(projectId, ns.ContainingNamespace.GetDocumentationCommentId() ?? "N:")));
                    if (ns.IsGlobalNamespace) break;
                    ns = ns.ContainingNamespace;
                }
            }
        }
    }

    internal static string? DeclarationKey(ISymbol symbol, SyntaxNode node, string rootPath)
    {
        if (symbol is ITypeParameterSymbol parameter)
            return GraphIdentity.Canonical("type-parameter", DeclarationKey(parameter.ContainingSymbol, node, rootPath) ?? "", parameter.TypeParameterKind.ToString(), parameter.Ordinal.ToString());
        if (symbol is IAssemblySymbol assembly) return "A:" + assembly.Identity;
        if (symbol is IMethodSymbol { AssociatedSymbol: { } owner } method)
            return "accessor:" + DeclarationKey(owner, node, rootPath) + ":" + method.MethodKind;
        if (symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction or MethodKind.AnonymousFunction } local)
        {
            var syntax = local.DeclaringSyntaxReferences.First().GetSyntax();
            // Child-node ordinals are independent of trivia/line numbers. Include lexical scope to
            // distinguish same-signature local functions in disjoint blocks of one member.
            var path = new List<int>();
            var current = syntax;
            while (current.Parent is { } parent)
            {
                path.Add(parent.ChildNodes().TakeWhile(n => n != current).Count());
                current = parent;
            }
            path.Reverse();
            return GraphIdentity.Canonical(local.MethodKind == MethodKind.AnonymousFunction ? "lambda" : "local", DeclarationKey(local.ContainingSymbol, node, rootPath) ?? "unresolved-owner",
                Path.GetRelativePath(rootPath, syntax.SyntaxTree.FilePath).Replace('\\', '/'), string.Join(".", path),
                local.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMemberOptions(
                    SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType |
                    SymbolDisplayMemberOptions.IncludeContainingType).WithGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeParameters)));
        }
        var key = symbol.GetDocumentationCommentId();
        // File-local types with the same source name in two files are distinct symbols.
        for (var type = symbol as INamedTypeSymbol ?? symbol.ContainingType; type is not null; type = type.ContainingType)
            if (type.IsFileLocal)
                return GraphIdentity.Canonical("file-local", Path.GetRelativePath(rootPath, node.SyntaxTree.FilePath).Replace('\\', '/'), key ?? symbol.ToDisplayString());
        return key;
    }
}
