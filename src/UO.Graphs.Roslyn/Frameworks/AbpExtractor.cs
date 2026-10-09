using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using UO.Graphs.Core;
using static UO.Graphs.Roslyn.FrameworkExtractionContext;
using static UO.Graphs.Roslyn.FrameworkSymbols;

namespace UO.Graphs.Roslyn;

internal sealed class AbpExtractor : IFrameworkExtractor
{
    public string Id => "abp-v1";
    private const string Core = "Volo.Abp.Core";
    private const string App = "Volo.Abp.Ddd.Application.Contracts";
    private const string Domain = "Volo.Abp.Ddd.Domain";
    public void Extract(FrameworkExtractionContext c)
    {
        if (!Is(c.Compilation.GetTypeByMetadataName("Volo.Abp.Modularity.AbpModule"), "Volo.Abp.Modularity.AbpModule", Core)) return;
        var configs = c.Invocations().Where(call => Is(call.TargetMethod.ContainingType,
            "Volo.Abp.AspNetCore.Mvc.Conventions.AbpConventionalControllerOptions", "Volo.Abp.AspNetCore.Mvc") && call.TargetMethod.Name == "Create").ToArray();
        foreach (var config in configs)
            c.Report("abpControllerConfiguration", config.Syntax, "ConventionalControllers.Create declaration observed; module execution, TypePredicate, controller removal, URL normalizers and custom conventions are not evaluated.");
        foreach (var (type, node) in Types(c))
        {
            var service = type.AllInterfaces.FirstOrDefault(i => Is(i, "Volo.Abp.Application.Services.IApplicationService", App));
            if (Is(type, "Volo.Abp.Application.Services.IApplicationService", App)) service = type;
            if (service is not null)
            {
                Annotate(c, type, node, "abp", type.TypeKind == TypeKind.Interface ? "applicationServiceContract" : "applicationService", service,
                    "Application service contract does not prove registration, endpoint exposure or runtime activation.");
                if (type.TypeKind == TypeKind.Class)
                    foreach (var contract in type.AllInterfaces.Where(i => Is(i, "Volo.Abp.Application.Services.IApplicationService", App) ||
                        i.AllInterfaces.Any(b => Is(b, "Volo.Abp.Application.Services.IApplicationService", App))))
                        c.Emit(RelationshipKind.APP_SERVICE_CONTRACT, c.Resolve(type, node), contract, node, "Bound ABP IApplicationService-derived contract.");
            }
            if (Bases(type.BaseType).FirstOrDefault(t => Is(t, "Volo.Abp.Modularity.AbpModule", Core)) is { } module)
            {
                Annotate(c, type, node, "abp", "module", module, "Module loading and ConfigureServices execution unknown.");
                foreach (var attr in type.GetAttributes().Where(a => Is(a.AttributeClass, "Volo.Abp.Modularity.DependsOnAttribute", Core)))
                    foreach (var arg in attr.ConstructorArguments.SelectMany(a => a.Kind == TypedConstantKind.Array ? a.Values : [a]))
                        c.Emit(RelationshipKind.MODULE_DEPENDS_ON, c.Resolve(type, node), arg.Value as ITypeSymbol,
                            attr.ApplicationSyntaxReference?.GetSyntax() ?? node, "ABP DependsOn typeof declaration; runtime module loading not asserted.");
            }
            foreach (var contract in type.Interfaces) Repository(c.Resolve(type, node), contract, node);
            if (type.TypeKind != TypeKind.Class) continue;
            Di(type, node);
            if (type.IsAbstract || type.IsGenericType || type.DeclaredAccessibility != Accessibility.Public || Disabled(type) || Bases(type).Any(t => t.GetAttributes().Any(a => Attribute(a, "NonControllerAttribute")))) continue;
            var remote = type.AllInterfaces.Any(i => Is(i, "Volo.Abp.IRemoteService", Core));
            if (!remote) continue;
            var registrations = configs.Where(call => call.Arguments.Any(a => Unwrap(a.Value) is IPropertyReferenceOperation p &&
                p.Property.Name == "Assembly" && SymbolEqualityComparer.Default.Equals(p.Property.ContainingType, c.Compilation.GetTypeByMetadataName("System.Type")) &&
                Unwrap(p.Instance) is ITypeOfOperation t && SymbolEqualityComparer.Default.Equals(t.TypeOperand.ContainingAssembly, type.ContainingAssembly))).ToArray();
            if (registrations.Length == 0)
            {
                c.Report("abpExposure", node, "Remote service without a bound typeof(...).Assembly ConventionalControllers.Create declaration in this project; no endpoint candidate emitted. External configuration may exist.");
                continue;
            }
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary &&
                         !m.IsStatic && !m.IsGenericMethod && m.DeclaredAccessibility == Accessibility.Public))
            {
                var source = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? node;
                var attrs = MethodAttributes(method).ToArray();
                if (RemoteDisabled(attrs.FirstOrDefault(a => Is(a.AttributeClass, "Volo.Abp.RemoteServiceAttribute", Core))) || attrs.Any(a => Attribute(a, "NonActionAttribute"))) continue;
                var notes = new[] { "Candidate only: bound assembly configuration and IRemoteService contract; runtime module execution, TypePredicate and discovery are unproven.",
                    "Conventional/custom routes and configuration overrides are unresolved; no concrete HTTP route is asserted." };
                var endpoint = c.AddNode("endpointCandidate", "ABP candidate " + method.ToDisplayString(), source, c.Resolve(method, source).Id);
                c.Entities[endpoint.Id] = endpoint with { Confidence = Confidence.Inferred,
                    Sources = new[] { c.Evidence(source), c.Evidence(node) }.Concat(registrations.Select(r => c.Evidence(r.Syntax))).Distinct().ToArray(),
                    HttpEndpoint = new(null, null, null, "abp", registrations.Any(r => Conditional(r.Syntax)), notes, "candidateOnly") };
                c.Emit(RelationshipKind.HANDLED_BY, Symbol(endpoint), method, source, string.Join(" ", notes), Confidence.Inferred);
                foreach (var config in registrations)
                    c.Emit(RelationshipKind.HANDLED_BY, Symbol(endpoint), method, config.Syntax, "ABP assembly configuration evidence; " + string.Join(" ", notes), Confidence.Inferred);
                c.Report("abpRoute", source, string.Join(" ", notes));
            }
            if (service is not null && type.BaseType?.Locations.Any(l => l.IsInSource) == true)
                c.Report("abpInheritedActions", node, "Inherited service action enumeration is unsupported; direct declarations only.");
        }
        foreach (var (tree, _) in c.Documents)
            foreach (var node in tree.GetRoot().DescendantNodes().Where(n => n is ParameterSyntax or PropertyDeclarationSyntax or VariableDeclaratorSyntax))
            {
                var symbol = c.Model(node).GetDeclaredSymbol(node);
                var type = symbol switch { IParameterSymbol p => p.Type, IPropertySymbol p => p.Type, IFieldSymbol f => f.Type, ILocalSymbol l => l.Type, _ => null };
                if (symbol is not null) Repository(c.Resolve(symbol is IParameterSymbol p ? p.ContainingSymbol : symbol, node), type, node);
            }
        foreach (var call in c.Invocations())
            Repository(Owner(c, call.Syntax), call.Instance?.Type ?? call.TargetMethod.ContainingType, call.Syntax);

        void Repository(RelationshipSymbol source, ITypeSymbol? value, SyntaxNode site)
        {
            if (value is not INamedTypeSymbol type) return;
            foreach (var repo in new[] { type }.Concat(type.AllInterfaces).Where(RepositoryType))
            {
                var entity = repo.TypeArguments[0];
                c.Emit(RelationshipKind.REPOSITORY_ENTITY, source, Open(entity) ? null : entity, site,
                    "Bound ABP repository entity argument: " + repo.ToDisplayString() + ". No DbContext, table, schema, SQL or runtime repository selected.");
                if (Open(entity)) c.Report("abpGenericRepository", site, "Open generic repository entity substitution unresolved: " + repo.ToDisplayString());
            }
        }
        void Di(INamedTypeSymbol type, SyntaxNode site)
        {
            if (type.IsAbstract || type.IsGenericType || InheritedAttribute(type, "Volo.Abp.DependencyInjection.DisableConventionalRegistrationAttribute", Core) is not null) return;
            var lifetime = new[] { ("ITransientDependency", "Transient"), ("ISingletonDependency", "Singleton"), ("IScopedDependency", "Scoped") }
                .FirstOrDefault(p => type.AllInterfaces.Any(i => Is(i, "Volo.Abp.DependencyInjection." + p.Item1, Core))).Item2;
            var dependency = InheritedAttribute(type, "Volo.Abp.DependencyInjection.DependencyAttribute", Core);
            var explicitLifetime = dependency?.ConstructorArguments.FirstOrDefault().Value;
            if (explicitLifetime is int number) lifetime = number switch { 0 => "Singleton", 1 => "Scoped", 2 => "Transient", _ => "unknown" };
            if (lifetime is null) return;
            var exposures = Bases(type).SelectMany(t => t.GetAttributes()).Where(a => Is(a.AttributeClass, "Volo.Abp.DependencyInjection.ExposeServicesAttribute", Core)).ToArray();
            var services = new List<ITypeSymbol>();
            if (exposures.Length == 0) Defaults(true, true);
            foreach (var attr in exposures)
            {
                services.AddRange(attr.ConstructorArguments.SelectMany(a => a.Kind == TypedConstantKind.Array ? a.Values : [a]).Select(a => a.Value).OfType<ITypeSymbol>());
                Defaults(attr.NamedArguments.Any(a => a.Key == "IncludeSelf" && a.Value.Value is true), attr.NamedArguments.Any(a => a.Key == "IncludeDefaults" && a.Value.Value is true));
            }
            var notes = new[] { "Inferred ABP conventional DI candidate only; assembly scanning, custom exposure providers, keyed services, callbacks, ReplaceServices/TryRegister and runtime selection are not evaluated." };
            var registration = c.AddNode("registration", "ABP convention " + type.ToDisplayString(), site, "abp-di");
            c.Entities[registration.Id] = registration with { Confidence = Confidence.Inferred, Registration = new(lifetime, "abpConvention", false, null, type.ToDisplayString(), false, notes) };
            foreach (var contract in services.Distinct<ITypeSymbol>(SymbolEqualityComparer.Default))
                c.Emit(RelationshipKind.REGISTERS, Symbol(registration), contract, site, notes[0], Confidence.Inferred);
            c.Emit(RelationshipKind.IMPLEMENTED_BY, Symbol(registration), type, site, notes[0], Confidence.Inferred);
            c.Report("abpDi", site, notes[0]);
            void Defaults(bool self, bool defaults)
            {
                if (self) services.Add(type);
                if (defaults) services.AddRange(type.AllInterfaces.Where(i => type.Name.EndsWith(i.Name.StartsWith('I') ? i.Name[1..] : i.Name, StringComparison.OrdinalIgnoreCase)));
            }
        }
    }
    private static bool RepositoryType(INamedTypeSymbol t) => t.TypeArguments.Length > 0 && new[] {
        "IRepository`1", "IRepository`2", "IReadOnlyRepository`1", "IReadOnlyRepository`2", "IBasicRepository`1", "IBasicRepository`2",
        "IReadOnlyBasicRepository`1", "IReadOnlyBasicRepository`2" }.Any(n => Is(t, "Volo.Abp.Domain.Repositories." + n, Domain));
    internal static bool Disabled(INamedTypeSymbol type) => RemoteDisabled(InheritedAttribute(type, "Volo.Abp.RemoteServiceAttribute", Core));
    private static bool RemoteDisabled(AttributeData? attr) => attr is not null &&
        (attr.NamedArguments.FirstOrDefault(a => a.Key == "IsEnabled").Value.Value is bool b ? !b : attr.ConstructorArguments.FirstOrDefault().Value is false);
    private static IEnumerable<AttributeData> MethodAttributes(IMethodSymbol method)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
            foreach (var attr in current.GetAttributes()) yield return attr;
    }
}
