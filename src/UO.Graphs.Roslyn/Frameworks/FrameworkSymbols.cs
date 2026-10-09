using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using UO.Graphs.Core;

namespace UO.Graphs.Roslyn;

internal static class FrameworkSymbols
{
    internal static bool Is(INamedTypeSymbol? type, string name, string assembly) => type is not null &&
        type.ContainingNamespace + "." + type.MetadataName == name && type.ContainingAssembly.Name == assembly &&
        !type.Locations.Any(l => l.IsInSource);
    internal static IEnumerable<INamedTypeSymbol> Bases(INamedTypeSymbol? type)
    {
        for (; type is not null; type = type.BaseType) yield return type;
    }
    internal static IEnumerable<(INamedTypeSymbol Type, TypeDeclarationSyntax Syntax)> Types(FrameworkExtractionContext c) =>
        c.Documents.SelectMany(d => d.Tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            .Select(n => (c.Model(n).GetDeclaredSymbol(n) as INamedTypeSymbol, n)).Where(p => p.Item1 is not null)
            .Select(p => (p.Item1!, p.n));
    internal static RelationshipSymbol Owner(FrameworkExtractionContext c, SyntaxNode node) =>
        c.Resolve(c.Model(node).GetEnclosingSymbol(node.SpanStart) ?? c.Compilation.Assembly, node);
    internal static string? Constant(IOperation? op) => op?.ConstantValue is { HasValue: true, Value: string s } ? s : null;
    internal static bool Open(ITypeSymbol type) => type is ITypeParameterSymbol || type is INamedTypeSymbol n &&
        (n.IsUnboundGenericType || n.TypeArguments.Any(Open));
    internal static AttributeData? InheritedAttribute(INamedTypeSymbol type, string name, string assembly) =>
        Bases(type).SelectMany(t => t.GetAttributes().Where(a => Is(a.AttributeClass, name, assembly))).FirstOrDefault();
    internal static void Annotate(FrameworkExtractionContext c, INamedTypeSymbol type, SyntaxNode node,
        string framework, string role, INamedTypeSymbol api, params string[] limitations)
    {
        var symbol = c.Resolve(type, node);
        if (c.Entities.TryGetValue(symbol.Id, out var entity)) c.Entities[entity.Id] = entity with {
            Framework = new(framework, role, api.ContainingAssembly.Identity.ToString(), limitations) };
    }
}
