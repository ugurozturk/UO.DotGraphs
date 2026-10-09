using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UO.Graphs.Core;
using static UO.Graphs.Roslyn.FrameworkExtractionContext;
using static UO.Graphs.Roslyn.FrameworkSymbols;

namespace UO.Graphs.Roslyn;

internal sealed class RefitExtractor : IFrameworkExtractor
{
    public string Id => "refit-v1";
    public void Extract(FrameworkExtractionContext c)
    {
        var contracts = new Dictionary<IMethodSymbol, GraphEntity[]>(SymbolEqualityComparer.Default);
        foreach (var (type, syntax) in Types(c).Where(t => t.Type.TypeKind == TypeKind.Interface))
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary && m.IsAbstract && !m.IsStatic))
                Contract(method, method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? syntax);
        foreach (var call in c.Invocations())
        {
            if (call.TargetMethod.ContainingType.TypeKind == TypeKind.Interface)
                foreach (var contract in Contract(call.TargetMethod.OriginalDefinition, call.Syntax))
                    c.EmitSymbol(RelationshipKind.INVOKES_HTTP, Owner(c, call.Syntax), Symbol(contract), call.Syntax,
                        "Bound call to Refit attributed interface method; CALLS retains the compile-time method. Runtime client, base address, headers and request execution are unknown.");
            if (Is(call.TargetMethod.ContainingType, "Refit.RestService", "Refit") && call.TargetMethod.Name == "For")
                c.Report("refitConfiguration", call.Syntax, "Refit client construction observed; base address/settings data flow and HttpClient mutation are not evaluated.");
        }

        GraphEntity[] Contract(IMethodSymbol method, SyntaxNode evidence)
        {
            method = method.OriginalDefinition;
            if (!method.IsAbstract || method.IsStatic) return [];
            if (contracts.TryGetValue(method, out var cached)) return cached;
            var result = new List<GraphEntity>();
            var attributes = method.GetAttributes().Where(a => Bases(a.AttributeClass).Any(t => Is(t, "Refit.HttpMethodAttribute", "Refit"))).ToArray();
            foreach (var attr in attributes)
            {
                var node = attr.ApplicationSyntaxReference?.GetSyntax() ?? evidence;
                var verb = new[] { "Get", "Post", "Put", "Delete", "Patch", "Head", "Options" }
                    .FirstOrDefault(v => Is(attr.AttributeClass, "Refit." + v + "Attribute", "Refit"))?.ToUpperInvariant();
                var route = verb is null ? null : attr.ConstructorArguments.FirstOrDefault().Value as string;
                if (c.Invalid(node) || attributes.Length > 1) { verb = null; route = null; }
                var notes = new[] { "Static HTTP contract template only; parameter substitution, serialization and final request URI are not evaluated.",
                    "Base address, runtime configuration and remote/local server identity unresolved; route similarity does not establish a service connection." };
                if (attributes.Length > 1) notes = [.. notes, "Multiple HTTP attributes; runtime first-attribute selection not modeled."];
                if (verb is null) notes = [.. notes, "Custom HttpMethodAttribute implementation is not executed."];
                var symbol = c.Resolve(method, node);
                // Metadata-only contracts use method identity, not the first call site's identity.
                var canonical = GraphIdentity.Canonical("refit-contract", c.ProjectId, symbol.Id, verb ?? "", route ?? "", attr.AttributeClass!.ToDisplayString());
                var entity = new GraphEntity(GraphIdentity.Hash(canonical), "httpContract", (verb ?? "?") + " " + (route ?? "?"),
                    method.ToDisplayString(), "", c.ProjectId, verb is null || route is null ? Confidence.Unresolved : Confidence.Exact,
                    [c.Evidence(node)], canonical, symbol.EntityId ?? c.ProjectId) {
                    HttpContract = new("refit", verb, route, null, method.ToDisplayString(), attr.AttributeClass.ContainingAssembly.Identity.ToString(), notes) };
                c.Entities[entity.Id] = entity;
                c.EmitSymbol(RelationshipKind.HTTP_CONTRACT, symbol, Symbol(entity), node, "Bound Refit HTTP attribute: " + attr.AttributeClass.ToDisplayString());
                c.Emit(RelationshipKind.POSSIBLE_ENDPOINT, Symbol(entity), null, node, notes[1]);
                c.Report("refitRemoteTarget", node, notes[1]);
                result.Add(entity);
            }
            return contracts[method] = result.ToArray();
        }
    }
}
