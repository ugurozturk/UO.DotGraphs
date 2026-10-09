using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using UO.Graphs.Core;
using static UO.Graphs.Roslyn.FrameworkExtractionContext;

namespace UO.Graphs.Roslyn;

internal sealed class MicrosoftDiExtractor : IFrameworkExtractor
{
    public string Id => "microsoft-di-v1";
    public void Extract(FrameworkExtractionContext context)
    {
        var collection = context.Compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");
        if (!FrameworkType(collection, "Microsoft.Extensions.DependencyInjection.IServiceCollection", "Microsoft.Extensions.DependencyInjection.Abstractions")) return;
        foreach (var call in context.Invocations())
        {
            var method = call.TargetMethod;
            var known = FrameworkType(method.ContainingType, "Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions",
                "Microsoft.Extensions.DependencyInjection.Abstractions") && method.Name is "AddSingleton" or "AddScoped" or "AddTransient";
            if (!known)
            {
                // A bound service-collection operation is evidence of an opaque registration/mutation candidate.
                // Do not match arbitrary methods by names such as Add/Scan.
                if (IsCollection(call.Instance?.Type) || call.Arguments.Any(a => IsCollection(a.Value.Type)))
                {
                    var unknown = context.AddNode("registration", "Unresolved DI: " + method.Name, call.Syntax);
                    context.Entities[unknown.Id] = unknown with { Registration = new("unknown", "opaque", Conditional(call.Syntax), null, null, false,
                        ["Opaque service-collection call; scanning, wrappers, descriptors and mutation are not expanded.", "Runtime registration set and order are unknown."]) };
                    context.Emit(RelationshipKind.REGISTERS, Symbol(unknown), null, call.Syntax, "Opaque service-collection call: " + method.ToDisplayString());
                    context.Report("diOpaque", call.Syntax, "Service-collection operation is not expanded: " + method.ToDisplayString());
                }
                continue;
            }
            var factory = Argument(call, "implementationFactory");
            var instance = Argument(call, "implementationInstance");
            var service = method.TypeArguments.FirstOrDefault() ?? TypeArgument(Argument(call, "serviceType"));
            var implementation = method.TypeArguments.Length == 2 ? method.TypeArguments[1] : TypeArgument(Argument(call, "implementationType"));
            var form = factory is not null ? "factory" : instance is not null ? "instance" : "type";
            if (factory is not null) implementation = null;
            if (form == "type" && Argument(call, "implementationType") is null) implementation ??= service;
            // For instance registrations only an explicit new expression proves a concrete type.
            if (Unwrap(instance) is IObjectCreationOperation creation) implementation = creation.Type;
            var limitations = new List<string> { "Static declaration only; runtime execution, container identity, order and implementation selection are not proven." };
            if (Conditional(call.Syntax)) limitations.Add("Conditional registration: branch/iteration outcome is unknown.");
            if (form == "factory") limitations.Add("Factory return implementation is unresolved; factory code is not executed or return-flow analyzed.");
            if (service is null || implementation is null) limitations.Add("Service or implementation cannot be statically resolved.");
            var entity = context.AddNode("registration", method.Name + " " + (service?.ToDisplayString() ?? "<unresolved>"), call.Syntax);
            context.Entities[entity.Id] = entity with { Registration = new(method.Name[3..], form, Conditional(call.Syntax),
                service?.ToDisplayString(), implementation?.ToDisplayString(),
                service is INamedTypeSymbol { IsUnboundGenericType: true } || implementation is INamedTypeSymbol { IsUnboundGenericType: true }, limitations.ToArray()) };
            context.Emit(RelationshipKind.REGISTERS, Symbol(entity), service, call.Syntax, "Microsoft DI service type from bound overload. " + string.Join(" ", limitations));
            context.Emit(RelationshipKind.IMPLEMENTED_BY, Symbol(entity), implementation, call.Syntax,
                form == "factory" ? "Factory result type unresolved; no container resolution or return-flow inference." : "Declared implementation type; not runtime selection.");
            if (factory is not null) context.Emit(RelationshipKind.USES_FACTORY, Symbol(entity), Handler(factory), factory.Syntax,
                "Bound factory method/lambda; delegate values and returned implementation are not resolved.");
            if (service is not null && !context.Invalid(call.Syntax)) context.RegisteredServices.Add(service);
            if (service is null || implementation is null || Conditional(call.Syntax)) context.Report("diUncertainty", call.Syntax, string.Join(" ", limitations));
        }
        foreach (var (tree, _) in context.Documents)
        {
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
                if (context.Model(invocation).GetOperation(invocation) is IDynamicInvocationOperation)
                    context.Report("dynamicFramework", invocation, "Dynamic invocation may hide registration/HTTP behavior; it is not classified or expanded.");
            foreach (var parameter in tree.GetRoot().DescendantNodes().OfType<ParameterSyntax>())
            {
                if (context.Model(parameter).GetDeclaredSymbol(parameter) is not IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } constructor } symbol
                    || constructor.IsStatic || constructor.DeclaredAccessibility != Accessibility.Public) continue;
                context.Emit(RelationshipKind.INJECTS, context.Resolve(constructor, parameter), symbol.Type, parameter,
                    "Public constructor parameter '" + symbol.Name + "' has this declared type. DI eligibility only; constructor use, service availability and runtime implementation are unknown.");
            }
        }
        bool IsCollection(ITypeSymbol? type) => SymbolEqualityComparer.Default.Equals(type, collection) ||
            type is INamedTypeSymbol named && named.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, collection));
    }
    private static ITypeSymbol? TypeArgument(IOperation? value) => Unwrap(value) is ITypeOfOperation t ? t.TypeOperand : null;
}
