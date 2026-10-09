using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using UO.Graphs.Core;
using static UO.Graphs.Roslyn.FrameworkExtractionContext;

namespace UO.Graphs.Roslyn;

// Bound contracts only. No assembly loading, scanning execution, container or broker.
internal sealed class MessagingExtractor : IFrameworkExtractor
{
    public string Id => "messaging-v1";
    private const string Scope = "Indexed source declarations in selected project/reference contexts only; other services/repositories and runtime registrations/topology are not observed. Missing candidates do not prove absence.";
    private const string Runtime = "Runtime registration, selection, pipeline order/short-circuit and broker topology are unresolved; publish never guarantees a single consumer.";

    public void Extract(FrameworkExtractionContext context)
    {
        var invocations = context.Invocations().ToArray();
        foreach (var (tree, _) in context.Documents)
        foreach (var syntax in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (context.Model(syntax).GetDeclaredSymbol(syntax) is not INamedTypeSymbol type || type.TypeKind != TypeKind.Class || type.IsAbstract) continue;
            foreach (var contract in type.AllInterfaces)
            {
                var rule = HandlerRule(contract);
                if (rule is null) continue;
                var (framework, kind, memberName) = rule.Value;
                var messageType = contract.TypeArguments[0];
                foreach (var member in contract.GetMembers(memberName).OfType<IMethodSymbol>())
                {
                    if (type.FindImplementationForInterfaceMember(member) is not IMethodSymbol method) continue;
                    // The UO no-response adapter has a default interface implementation. Prefer the actual class handler.
                    if (method.ContainingType.TypeKind == TypeKind.Interface) continue;
                    var source = context.Resolve(method, syntax);
                    if (context.Invalid(syntax) || Open(messageType) || Open(type))
                    {
                        context.EmitSymbol(kind, source, null, syntax, framework + ": open generic handler/behavior; closed substitutions and constraints are not expanded. " + Runtime);
                        context.Report("messagingOpenGeneric", syntax, framework + ": open generic binding is unresolved.");
                        continue;
                    }
                    var message = Message(context, framework, messageType, syntax);
                    context.EmitSymbol(kind, source, Symbol(message), syntax,
                        framework + ": Roslyn interface implementation " + contract.ToDisplayString() + ". " + Runtime);
                    var candidateKind = kind == RelationshipKind.BEHAVIOR_FOR ? RelationshipKind.POSSIBLE_BEHAVIOR : RelationshipKind.POSSIBLE_TARGET;
                    context.Emit(candidateKind, Symbol(message), method, syntax,
                        framework + ": contract-compatible declaration only; registration not established. " + Scope + " " + Runtime, Confidence.Inferred);
                    // Keep direct DI registration evidence as another occurrence; execution/ordering still cannot be inferred.
                    foreach (var registration in invocations.Where(c => DirectRegistration(c, type, contract)))
                        context.Emit(candidateKind, Symbol(message), method, registration.Syntax,
                            "Bound Microsoft DI registration for this implementation/contract; conditional=" + Conditional(registration.Syntax) + ". " + Runtime, Confidence.Inferred);
                    context.EmitSymbol(candidateKind, Symbol(message), null, syntax, "Runtime candidate set remains open. " + Scope + " " + Runtime);
                    context.Report("messagingScope", syntax, Scope + " " + Runtime);
                }
            }
        }

        foreach (var call in invocations)
        {
            var rule = CallRule(call.TargetMethod);
            if (rule is null) continue;
            var (framework, kind) = rule.Value;
            var caller = context.Model(call.Syntax).GetEnclosingSymbol(call.Syntax.SpanStart);
            if (caller is null) continue;
            var source = context.Resolve(caller, call.Syntax);
            var messageType = CallMessage(call, framework);
            if (context.Invalid(call.Syntax) || messageType is null || Open(messageType) || messageType.TypeKind == TypeKind.Error)
            {
                context.EmitSymbol(kind, source, null, call.Syntax,
                    framework + ": bound API " + call.TargetMethod.ToDisplayString() + "; message runtime type is unresolved (object/interface/type parameter/computed Type). " + Scope);
                context.Report("messagingType", call.Syntax, "Message identity cannot be established for " + call.TargetMethod.ToDisplayString());
                continue;
            }
            var message = Message(context, framework, messageType, call.Syntax);
            context.EmitSymbol(kind, source, Symbol(message), call.Syntax,
                framework + ": bound API " + call.TargetMethod.ToDisplayString() + "; message contract from semantic type/overload. " + Runtime);
            context.EmitSymbol(RelationshipKind.POSSIBLE_TARGET, Symbol(message), null, call.Syntax,
                "Runtime dispatch target remains unresolved even when local contract candidates exist. " + Scope + " " + Runtime);
            context.Report("messagingScope", call.Syntax, Scope + " " + Runtime);
        }
    }

    private static (string Framework, RelationshipKind Kind, string Member)? HandlerRule(INamedTypeSymbol type)
    {
        if (Known(type, "UO.Mediator.Dispatching.IRequestHandler`1", "UO.Mediator.Shared") || Known(type, "UO.Mediator.Dispatching.IRequestHandler`2", "UO.Mediator.Shared"))
            return ("uoMediator", RelationshipKind.HANDLES, "HandleAsync");
        if (Known(type, "UO.Mediator.Dispatching.IRequestBehavior`2", "UO.Mediator.Shared"))
            return ("uoMediator", RelationshipKind.BEHAVIOR_FOR, "HandleAsync");
        if (Known(type, "MediatR.IRequestHandler`1", "MediatR") || Known(type, "MediatR.IRequestHandler`2", "MediatR"))
            return ("mediatr", RelationshipKind.HANDLES, "Handle");
        if (Known(type, "MediatR.INotificationHandler`1", "MediatR")) return ("mediatr", RelationshipKind.CONSUMES, "Handle");
        if (Known(type, "MediatR.IPipelineBehavior`2", "MediatR")) return ("mediatr", RelationshipKind.BEHAVIOR_FOR, "Handle");
        if (Known(type, "MassTransit.IConsumer`1", "MassTransit.Abstractions")) return ("massTransit", RelationshipKind.CONSUMES, "Consume");
        return null;
    }

    private static (string Framework, RelationshipKind Kind)? CallRule(IMethodSymbol method)
    {
        // Match the actual bound owner, not a receiver's simple name or an arbitrary Send/Publish method.
        if (Known(method.ContainingType, "UO.Mediator.Dispatching.IRequestDispatcher", "UO.Mediator.Shared") && method.Name == "DispatchAsync")
            return ("uoMediator", RelationshipKind.DISPATCHES);
        if (Known(method.ContainingType, "MediatR.ISender", "MediatR") && method.Name == "Send") return ("mediatr", RelationshipKind.DISPATCHES);
        if (Known(method.ContainingType, "MediatR.IPublisher", "MediatR") && method.Name == "Publish") return ("mediatr", RelationshipKind.PUBLISHES);
        if (Known(method.ContainingType, "MassTransit.IPublishEndpoint", "MassTransit.Abstractions") && method.Name == "Publish") return ("massTransit", RelationshipKind.PUBLISHES);
        if (Known(method.ContainingType, "MassTransit.ISendEndpoint", "MassTransit.Abstractions") && method.Name == "Send") return ("massTransit", RelationshipKind.DISPATCHES);
        return null;
    }

    private static ITypeSymbol? CallMessage(IInvocationOperation call, string framework)
    {
        var argument = call.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0)?.Value;
        if (framework == "massTransit")
        {
            // Generic T is the published contract, including interface-valued initializer overloads.
            if (call.TargetMethod.TypeArguments.Length == 1) return call.TargetMethod.TypeArguments[0];
            if (Argument(call, "messageType") is { } explicitType)
                return Unwrap(explicitType) is ITypeOfOperation t ? t.TypeOperand : null;
        }
        var value = Unwrap(argument);
        if (value is IObjectCreationOperation created) return created.Type;
        // Mediators dispatch by runtime type. Only sealed/value static types are sufficient without data flow.
        return value?.Type is INamedTypeSymbol { SpecialType: not SpecialType.System_Object } named && (named.IsSealed || named.IsValueType) ? named : null;
    }

    private static bool Known(INamedTypeSymbol type, string metadata, string assembly)
    {
        var definition = type.OriginalDefinition;
        // Version families are bounded to inspected contracts, not claims of compatibility with future majors.
        var version = definition.ContainingAssembly.Identity.Version;
        return definition.ContainingAssembly.Name == assembly &&
            definition.GetDocumentationCommentId() == "T:" + metadata &&
            !definition.Locations.Any(l => l.IsInSource) &&
            (assembly == "UO.Mediator.Shared" ? version.Major == 2 : assembly == "MediatR" ? version.Major == 12 : version.Major == 8);
    }

    private static bool DirectRegistration(IInvocationOperation call, INamedTypeSymbol implementation, INamedTypeSymbol contract) =>
        FrameworkType(call.TargetMethod.ContainingType, "Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions", "Microsoft.Extensions.DependencyInjection.Abstractions") &&
        call.TargetMethod.Name is "AddSingleton" or "AddScoped" or "AddTransient" && call.TargetMethod.TypeArguments.Length == 2 &&
        SymbolEqualityComparer.Default.Equals(call.TargetMethod.TypeArguments[0], contract) &&
        SymbolEqualityComparer.Default.Equals(call.TargetMethod.TypeArguments[1], implementation);

    private static bool Open(ITypeSymbol type) => type is ITypeParameterSymbol || type is INamedTypeSymbol n &&
        (n.TypeArguments.Any(Open) || n.ContainingType is { } parent && Open(parent)) || type is IArrayTypeSymbol a && Open(a.ElementType);

    private static GraphEntity Message(FrameworkExtractionContext context, string framework, ITypeSymbol type, SyntaxNode syntax)
    {
        string Key(ITypeSymbol t) => t switch
        {
            IArrayTypeSymbol a => GraphIdentity.Canonical("array", a.Rank.ToString(), Key(a.ElementType)),
            INamedTypeSymbol n => GraphIdentity.Canonical(["type", n.ContainingAssembly.Identity.ToString(),
                context.Resolve(n, syntax).ProjectId ?? "metadata", n.OriginalDefinition.GetDocumentationCommentId() ?? n.MetadataName,
                n.ContainingType is null ? "" : Key(n.ContainingType), .. n.TypeArguments.Select(Key)]),
            _ => GraphIdentity.Canonical(t.TypeKind.ToString(), t.ToDisplayString())
        };
        var key = Key(type);
        var canonical = GraphIdentity.Canonical("message", framework, key);
        var id = GraphIdentity.Hash(canonical);
        var evidence = context.Evidence(syntax);
        if (context.Entities.TryGetValue(id, out var existing))
        {
            context.Entities[id] = existing with { Sources = existing.Sources.Append(evidence).Distinct().ToArray() };
            return context.Entities[id];
        }
        var name = framework + " message " + type.ToDisplayString();
        var entity = new GraphEntity(id, "message", name, name, "", context.Resolve(type, syntax).ProjectId ?? context.ProjectId,
            Confidence.Exact, [evidence], canonical)
        {
            Message = new(framework, key, type.ContainingAssembly.Identity.ToString(), type.ToDisplayString(), Scope, [Runtime])
        };
        context.Entities[id] = entity;
        return entity;
    }
}
