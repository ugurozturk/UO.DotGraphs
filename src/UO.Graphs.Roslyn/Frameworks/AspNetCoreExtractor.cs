using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using UO.Graphs.Core;
using static UO.Graphs.Roslyn.FrameworkExtractionContext;

namespace UO.Graphs.Roslyn;

internal sealed class AspNetCoreExtractor : IFrameworkExtractor
{
    public string Id => "aspnet-core-v1";
    public void Extract(FrameworkExtractionContext context)
    {
        foreach (var call in context.Invocations().Where(c => IsMap(c, "MapGet", "MapPost", "MapMethods")))
        {
            var route = Constant(Argument(call, "pattern"));
            var prefixEvidence = new List<SyntaxNode>();
            var prefix = Prefix(Argument(call, "endpoints") ?? call.Instance, 0, prefixEvidence);
            var full = route is not null && prefix is not null ? Combine(prefix, route) : null;
            string?[]? methods = call.TargetMethod.Name == "MapMethods" ? Methods(Argument(call, "httpMethods")) : [call.TargetMethod.Name[3..].ToUpperInvariant()];
            var handlerValue = Argument(call, "handler") ?? Argument(call, "requestDelegate");
            var handler = Handler(handlerValue);
            foreach (var method in methods ?? [null])
                Endpoint(method, full, handler, call.Syntax, "minimalApi", Conditional(call.Syntax),
                    full is null ? ["Nonconstant route or unresolved/mutated route-group prefix."] : [], handlerValue?.Syntax, prefixEvidence);
        }
        foreach (var (tree, _) in context.Documents)
        foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (context.Model(declaration).GetDeclaredSymbol(declaration) is not INamedTypeSymbol type ||
                type.IsAbstract || type.IsGenericType || type.DeclaredAccessibility != Accessibility.Public ||
                NonController(type)) continue;
            var recognized = type.Name.EndsWith("Controller", StringComparison.Ordinal) ||
                type.GetAttributes().Any(a => Attribute(a, "ControllerAttribute") || Attribute(a, "ApiControllerAttribute"));
            for (var b = type.BaseType; b is not null; b = b.BaseType)
                recognized |= FrameworkType(b, "Microsoft.AspNetCore.Mvc.ControllerBase", "Microsoft.AspNetCore.Mvc.Core");
            if (!recognized) continue;
            var inherited = false;
            for (var b = type.BaseType; b is not null && b.SpecialType != SpecialType.System_Object; b = b.BaseType)
                if (!FrameworkType(b, "Microsoft.AspNetCore.Mvc.ControllerBase", "Microsoft.AspNetCore.Mvc.Core") &&
                    !FrameworkType(b, "Microsoft.AspNetCore.Mvc.Controller", "Microsoft.AspNetCore.Mvc.ViewFeatures"))
                {
                    inherited = true;
                    context.Report("mvcInheritance", declaration, "Inherited controller routes/actions are outside this extractor's direct-declaration scope: " + b.ToDisplayString());
                }
            var controllerRoutes = type.GetAttributes().Where(a => Attribute(a, "RouteAttribute")).ToArray();
            foreach (var methodNode in declaration.Members.OfType<MethodDeclarationSyntax>())
            {
                if (context.Model(methodNode).GetDeclaredSymbol(methodNode) is not IMethodSymbol action ||
                    action.IsStatic || action.IsGenericMethod || action.DeclaredAccessibility != Accessibility.Public ||
                    NonAction(action)) continue;
                var attrs = action.GetAttributes();
                var customRoutes = type.GetAttributes().Concat(attrs).Where(IsCustomRoute).ToArray();
                if (customRoutes.Length > 0) context.Report("mvcCustomRoute", methodNode, "Custom route/HTTP attribute providers are not evaluated.");
                var verbs = attrs.Where(a => HttpVerb(a) is not null || Attribute(a, "AcceptVerbsAttribute")).ToArray();
                var routes = attrs.Where(a => Attribute(a, "RouteAttribute")).ToArray();
                // HTTP attributes with templates are independent selectors; template-less verbs constrain Route selectors.
                var selectors = new List<(string? Route, string Method, AttributeData? Evidence)>();
                foreach (var verb in verbs.Where(v => Template(v) is not null))
                    foreach (var http in AttributeMethods(verb)) selectors.Add((Template(verb), http, verb));
                var silent = verbs.Where(v => Template(v) is null).ToArray();
                if (routes.Length > 0)
                    foreach (var routeAttr in routes)
                        foreach (var http in silent.Length == 0 ? ["*"] : silent.SelectMany(AttributeMethods).Distinct())
                            selectors.Add((Template(routeAttr), http, routeAttr));
                else if (silent.Length > 0 || verbs.Length == 0 && controllerRoutes.Length > 0)
                    foreach (var http in silent.Length == 0 ? ["*"] : silent.SelectMany(AttributeMethods).Distinct())
                        selectors.Add((controllerRoutes.Length == 0 ? null : "", http, silent.FirstOrDefault()));
                if (selectors.Count == 0 && customRoutes.Length > 0) selectors.Add((null, "*", customRoutes[0]));
                foreach (var selector in selectors)
                {
                    var prefixes = controllerRoutes.Length == 0 ? new (string? Route, AttributeData? Attribute)[] { ("", null) } : controllerRoutes.Select(a => (Route: Template(a), Attribute: (AttributeData?)a)).ToArray();
                    if (selector.Route?.StartsWith('/') == true || selector.Route?.StartsWith("~/", StringComparison.Ordinal) == true) prefixes = [("", null)];
                    foreach (var controllerPrefix in prefixes)
                    {
                        var pattern = inherited || customRoutes.Length > 0 || controllerPrefix.Route is null || selector.Route is null ? null : Tokens(Combine(controllerPrefix.Route, selector.Route), type, action);
                        var source = selector.Evidence?.ApplicationSyntaxReference?.GetSyntax() ?? methodNode;
                        Endpoint(selector.Method, pattern, action, source, "controller", false,
                            ["Controller discovery, application parts, MVC conventions and host activation are not evaluated."], methodNode, new[] { controllerPrefix.Attribute?.ApplicationSyntaxReference?.GetSyntax() }.OfType<SyntaxNode>());
                    }
                }
            }
        }
        // Keep declarations distinct even when their method/pattern/context are identical.
        foreach (var group in context.Entities.Values.Where(e => e.ProjectId == context.ProjectId && e.HttpEndpoint?.CollisionKey is not null)
                     .GroupBy(e => e.HttpEndpoint!.CollisionKey).Where(g => g.Count() > 1).ToArray())
            foreach (var e in group)
            {
                context.Entities[e.Id] = e with { HttpEndpoint = e.HttpEndpoint! with {
                    Limitations = [.. e.HttpEndpoint.Limitations, "Duplicate static HTTP method/route in this analysis context; runtime ambiguity is not evaluated."] } };
            }

        void Endpoint(string? http, string? route, IMethodSymbol? handler, SyntaxNode source, string framework,
            bool conditional, string[] limitations, SyntaxNode? handlerSyntax, IEnumerable<SyntaxNode> supportingEvidence)
        {
            if (context.Invalid(source)) { http = null; route = null; limitations = [.. limitations, "Compilation error; endpoint key unresolved."]; }
            var notes = new List<string>(limitations) { "Static endpoint declaration only; host activation and request reachability are not proven." };
            if (http is null) notes.Add("HTTP method sequence is not a constant inline collection.");
            if (route is null) notes.Add("Route pattern unresolved; no concrete route identity is asserted.");
            if (handler is null) notes.Add("Handler delegate value is unresolved.");
            if (conditional) notes.Add("Conditional declaration; execution outcome unknown.");
            var key = http is null || route is null ? null : GraphIdentity.Canonical("http-route", context.ProjectId, http, route);
            var supportingNodes = supportingEvidence.ToArray();
            var entity = context.AddNode("endpoint", (http ?? "<unknown-method>") + " " + (route ?? "<unknown-route>"), source, http ?? "", route ?? "", handler?.ToDisplayString() ?? "", GraphIdentity.Canonical(supportingNodes.Select(context.Site).ToArray()));
            context.Entities[entity.Id] = entity with { Confidence = key is null ? Confidence.Unresolved : entity.Confidence,
                Sources = new[] { context.Evidence(source) }.Concat(supportingNodes.Select(context.Evidence)).Distinct().ToArray(),
                HttpEndpoint = new(http, route, key, framework, conditional, notes.ToArray()) };
            context.Emit(RelationshipKind.HANDLED_BY, Symbol(entity), handler, source, "ASP.NET Core bound endpoint declaration. " + string.Join(" ", notes));
            if (route is null || http is null || handler is null || conditional) context.Report("httpUncertainty", source, string.Join(" ", notes));
            if (handler is null) return;
            var handlerSource = handlerSyntax ?? source;
            foreach (var parameter in handler.Parameters)
            {
                var explicitService = parameter.GetAttributes().Any(a => Attribute(a, "FromServicesAttribute"));
                var otherBinding = parameter.GetAttributes().Any(a => a.AttributeClass?.ContainingNamespace.ToDisplayString() == "Microsoft.AspNetCore.Mvc" && a.AttributeClass.Name.StartsWith("From", StringComparison.Ordinal) && !Attribute(a, "FromServicesAttribute"));
                var implicitService = !otherBinding && context.RegisteredServices.Contains(parameter.Type);
                if (!explicitService && !implicitService) continue;
                var parameterSource = parameter.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? handlerSource;
                context.Emit(RelationshipKind.INJECTS, context.Resolve(handler, handlerSource), parameter.Type, parameterSource,
                    explicitService ? "Explicit FromServices parameter; declared service type exact, resolution unknown." : "Inferred HTTP service parameter candidate from a static registration; binding precedence, container membership and runtime selection unknown.",
                    explicitService ? Confidence.Exact : Confidence.Inferred);
            }
            if (framework == "controller")
                foreach (var constructor in handler.ContainingType.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public))
                    context.Emit(RelationshipKind.ACTIVATES, context.Resolve(handler, handlerSource), constructor, source,
                        "Controller action requires an instance; this public constructor is only a candidate. Custom activators and constructor selection are not evaluated.", Confidence.Inferred);
        }
        string? Prefix(IOperation? receiver, int depth, List<SyntaxNode> evidence)
        {
            if (depth > 16) return null;
            receiver = Unwrap(receiver);
            if (receiver is IInvocationOperation group && IsMap(group, "MapGroup"))
            {
                evidence.Add(group.Syntax);
                var outer = Prefix(Argument(group, "endpoints") ?? group.Instance, depth + 1, evidence);
                var inner = Constant(Argument(group, "prefix"));
                return outer is null || inner is null ? null : Combine(outer, inner);
            }
            if (receiver is ILocalReferenceOperation local && local.Local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: { } initializer })
            {
                var root = initializer.SyntaxTree.GetRoot();
                // Alias recovery is deliberately limited to locals with no writes/ref escape anywhere in the tree.
                foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
                    if (SymbolEqualityComparer.Default.Equals(context.Model(identifier).GetSymbolInfo(identifier).Symbol, local.Local) &&
                        (identifier.Ancestors().OfType<AssignmentExpressionSyntax>().Any(a => a.Left.Span.Contains(identifier.Span)) ||
                         identifier.Parent is ArgumentSyntax { RefKindKeyword.RawKind: not 0 })) return null;
                return Prefix(context.Model(initializer).GetOperation(initializer), depth + 1, evidence);
            }
            return FrameworkType(receiver?.Type as INamedTypeSymbol, "Microsoft.AspNetCore.Builder.WebApplication", "Microsoft.AspNetCore") ? "" : null;
        }
    }
    private static bool NonController(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (current.GetAttributes().Any(a => Attribute(a, "NonControllerAttribute"))) return true;
        return false;
    }
    private static bool NonAction(IMethodSymbol action)
    {
        for (var current = action; current is not null; current = current.OverriddenMethod)
            if (current.GetAttributes().Any(a => Attribute(a, "NonActionAttribute"))) return true;
        return false;
    }
    private static bool IsMap(IInvocationOperation call, params string[] names) => names.Contains(call.TargetMethod.Name) &&
        FrameworkType(call.TargetMethod.ContainingType, "Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions", "Microsoft.AspNetCore.Routing");
    private static string? Constant(IOperation? value) => value?.ConstantValue is { HasValue: true, Value: string s } ? s : null;
    private static string[]? Methods(IOperation? value)
    {
        value = Unwrap(value);
        var elements = value switch
        {
            IArrayCreationOperation { Initializer: { } initializer } => initializer.ElementValues.ToArray(),
            ICollectionExpressionOperation collection => collection.Elements.ToArray(),
            _ => null
        };
        if (elements is null || elements.Length == 0 || elements.Any(e => Constant(e) is null)) return null;
        return elements.Select(e => Constant(e)!.ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToArray();
    }
    private static string Combine(string prefix, string route) => "/" + string.Join('/',
        new[] { prefix.Trim('/'), route.StartsWith("~/", StringComparison.Ordinal) ? route[2..].TrimStart('/') : route.TrimStart('/') }.Where(s => s.Length > 0));
    private static bool IsCustomRoute(AttributeData attribute) =>
        attribute.AttributeClass?.AllInterfaces.Any(i => i.ToDisplayString() is "Microsoft.AspNetCore.Mvc.Routing.IRouteTemplateProvider" or "Microsoft.AspNetCore.Mvc.Routing.IActionHttpMethodProvider") == true &&
        !Attribute(attribute, "RouteAttribute") && !Attribute(attribute, "AcceptVerbsAttribute") && HttpVerb(attribute) is null;
    private static string? HttpVerb(AttributeData attribute) => new[] { "Get", "Post", "Put", "Delete", "Patch", "Head", "Options" }
        .FirstOrDefault(v => Attribute(attribute, "Http" + v + "Attribute"))?.ToUpperInvariant();
    private static IEnumerable<string> AttributeMethods(AttributeData attribute)
    {
        if (HttpVerb(attribute) is { } verb) return [verb];
        return attribute.ConstructorArguments.SelectMany(a => a.Kind == TypedConstantKind.Array ? a.Values : [a])
            .Select(a => a.Value as string).OfType<string>().Select(s => s.ToUpperInvariant());
    }
    private static string? Template(AttributeData attribute) => Attribute(attribute, "AcceptVerbsAttribute")
        ? attribute.NamedArguments.FirstOrDefault(a => a.Key == "Route").Value.Value as string
        : attribute.ConstructorArguments.FirstOrDefault().Value as string;
    private static string? Tokens(string route, INamedTypeSymbol controller, IMethodSymbol action)
    {
        var controllerName = controller.Name.EndsWith("Controller", StringComparison.Ordinal) ? controller.Name[..^10] : controller.Name;
        var actionName = action.GetAttributes().FirstOrDefault(a => Attribute(a, "ActionNameAttribute"))?.ConstructorArguments.FirstOrDefault().Value as string
            ?? (action.Name.EndsWith("Async", StringComparison.Ordinal) ? action.Name[..^5] : action.Name);
        var area = controller.GetAttributes().FirstOrDefault(a => Attribute(a, "AreaAttribute"))?.ConstructorArguments.FirstOrDefault().Value as string;
        var result = route.Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase).Replace("[action]", actionName, StringComparison.OrdinalIgnoreCase);
        if (area is not null) result = result.Replace("[area]", area, StringComparison.OrdinalIgnoreCase);
        return result.Contains('[') || result.Contains(']') ? null : result;
    }
}
