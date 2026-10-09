using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using UO.Graphs.Core;

namespace UO.Graphs.Roslyn;

internal sealed class RelationshipCollector(Compilation compilation, string projectId, string rootPath,
    IReadOnlyDictionary<IAssemblySymbol, string> assemblyContexts, RelationshipBuilder builder)
{
    private readonly Diagnostic[] compilationDiagnostics = compilation.GetDiagnostics().ToArray();

    public async Task CollectAsync(SyntaxTree tree, bool generated)
    {
        var model = compilation.GetSemanticModel(tree);
        var root = await tree.GetRootAsync();
        var hash = GraphIdentity.Hash((await tree.GetTextAsync()).ToString());
        var errors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error && d.Location.SourceTree == tree)
            .Select(d => d.Location.SourceSpan).ToArray();
        var operations = new HashSet<IOperation>(ReferenceEqualityComparer.Instance);
        var callSites = new HashSet<SyntaxNode>();
        foreach (var node in root.DescendantNodesAndSelf())
        {
            if (node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or BaseMethodDeclarationSyntax
                or BasePropertyDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax)
            {
                var symbol = model.GetDeclaredSymbol(node);
                if (symbol is not null) Structural(symbol, node);
            }
            if (node is TypeSyntax && model.GetSymbolInfo(node).Symbol is ITypeSymbol type)
                UseType(type, Owner(node), node);
            if (model.GetOperation(node) is { } operation && operation.Parent is null) Walk(operation);
        }
        // Invalid/dynamic syntax may have no callable operation. Never promote candidate symbols to targets.
        foreach (var node in root.DescendantNodes().Where(n => n is InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax or ConstructorInitializerSyntax))
        {
            if (callSites.Contains(node)) continue;
            var info = model.GetSymbolInfo(node);
            Emit(RelationshipKind.CALLS, Owner(node), null, node, "Unresolved call: " + info.CandidateReason,
                related: info.CandidateSymbols);
        }

        bool HasErrors(SyntaxNode node) => errors.Any(e => node.Span.IntersectsWith(e));
        bool Invalid(SyntaxNode node)
        {
            // Conservative per executable declaration: overload recovery inside a broken body is not proof.
            var scope = node.AncestorsAndSelf().FirstOrDefault(n => n is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax
                or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax or BasePropertyDeclarationSyntax or FieldDeclarationSyntax);
            return HasErrors(scope ?? node);
        }
        ISymbol? Owner(SyntaxNode node)
        {
            foreach (var ancestor in node.AncestorsAndSelf())
            {
                if (ancestor is AnonymousFunctionExpressionSyntax && model.GetOperation(ancestor) is IAnonymousFunctionOperation lambda) return lambda.Symbol;
                if (ancestor is LocalFunctionStatementSyntax or BaseMethodDeclarationSyntax or AccessorDeclarationSyntax)
                    return model.GetDeclaredSymbol(ancestor);
                if (ancestor is PropertyDeclarationSyntax or IndexerDeclarationSyntax)
                {
                    var property = model.GetDeclaredSymbol(ancestor) as IPropertySymbol;
                    return node.AncestorsAndSelf().Any(n => n is EqualsValueClauseSyntax) ? property : property?.GetMethod ?? (ISymbol?)property;
                }
                if (ancestor is VariableDeclaratorSyntax && model.GetDeclaredSymbol(ancestor) is IFieldSymbol field) return field;
                if (ancestor is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax) return model.GetDeclaredSymbol(ancestor);
            }
            return model.GetEnclosingSymbol(node.SpanStart);
        }
        void Emit(RelationshipKind kind, ISymbol? source, ISymbol? target, SyntaxNode node, string reason,
            bool checkErrors = true, IEnumerable<ISymbol>? related = null)
        {
            if (source is null) return;
            var invalid = checkErrors && Invalid(node);
            var relatedSymbols = (related ?? []).Concat(invalid && target is not null ? [target] : []).Select(s => Resolve(s, node)).ToArray();
            builder.Add(kind, Resolve(source, node), target is null || invalid ? null : Resolve(target, node), projectId,
                target is null || invalid ? Confidence.Unresolved : Confidence.Exact, Evidence(node),
                invalid ? "Compilation error in containing declaration; binding is not asserted. " + reason : reason,
                target?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), relatedSymbols);
        }
        SourceEvidence Evidence(SyntaxNode node)
        {
            var span = node.GetLocation().GetLineSpan();
            return new(tree.FilePath, span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1,
                span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1, hash, generated);
        }
        void UseType(ITypeSymbol type, ISymbol? owner, SyntaxNode node)
        {
            if (type is IArrayTypeSymbol array) { UseType(array.ElementType, owner, node); return; }
            if (type is IPointerTypeSymbol pointer) { UseType(pointer.PointedAtType, owner, node); return; }
            Emit(RelationshipKind.USES_TYPE, owner, type.TypeKind is TypeKind.Error or TypeKind.Dynamic ? null : type, node,
                "SemanticModel bound type; generic constructions are retained in boundTarget.");
            if (type is INamedTypeSymbol named)
                foreach (var argument in named.TypeArguments.Where(a => a is not ITypeParameterSymbol)) UseType(argument, owner, node);
        }
        void Structural(ISymbol symbol, SyntaxNode node)
        {
            ISymbol? overridden = symbol switch
            {
                IMethodSymbol m => m.OverriddenMethod,
                IPropertySymbol p => p.OverriddenProperty,
                IEventSymbol e => e.OverriddenEvent,
                _ => null
            };
            if (overridden is not null) Emit(RelationshipKind.OVERRIDES, symbol, overridden, node, "Roslyn OverriddenMember binding.");
            if (symbol is not INamedTypeSymbol type || node is not BaseTypeDeclarationSyntax declaration) return;
            if (declaration.BaseList is { } bases)
                foreach (var entry in bases.Types)
                {
                    var target = model.GetTypeInfo(entry.Type).Type;
                    var kind = type.TypeKind == TypeKind.Interface || target?.TypeKind != TypeKind.Interface
                        ? RelationshipKind.INHERITS : RelationshipKind.IMPLEMENTS;
                    Emit(kind, type, target?.TypeKind == TypeKind.Error ? null : target, entry, "SemanticModel direct base-list binding.");
                }
            if (type.TypeKind == TypeKind.Interface) return;
            foreach (var contract in type.AllInterfaces)
                foreach (var member in contract.GetMembers())
                    if (type.FindImplementationForInterfaceMember(member) is { } implementation)
                        Emit(RelationshipKind.IMPLEMENTS, implementation, member, node,
                            "Roslyn FindImplementationForInterfaceMember in " + type.ToDisplayString() + ".", checkErrors: true);
        }
        void Call(IMethodSymbol? target, IOperation operation, string reason)
        {
            callSites.Add(operation.Syntax);
            for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
                if (parent is INameOfOperation) return;
            if (target?.MethodKind == MethodKind.DelegateInvoke)
                Emit(RelationshipKind.CALLS, Owner(operation.Syntax), null, operation.Syntax,
                    "Delegate invocation: runtime delegate value is not resolved; Invoke is only a related signature.", related: [target]);
            else
                Emit(RelationshipKind.CALLS, Owner(operation.Syntax), target, operation.Syntax, reason);
            if (target?.ContainingType.ToDisplayString() is "System.Reflection.MethodBase" or "System.Reflection.MethodInfo" or "System.Delegate"
                && target.Name is "Invoke" or "DynamicInvoke")
                Emit(RelationshipKind.CALLS, Owner(operation.Syntax), null, operation.Syntax,
                    "Reflection/delegate API invocation is bound; reflected runtime target is unknown.", related: [target]);
        }
        void Walk(IOperation operation)
        {
            if (!operations.Add(operation)) return;
            switch (operation)
            {
                case IDelegateCreationOperation:
                    callSites.Add(operation.Syntax); break;
                case IDynamicMemberReferenceOperation:
                    Emit(RelationshipKind.REFERENCES, Owner(operation.Syntax), null, operation.Syntax,
                        "Dynamic member reference: no statically bound member."); break;
                case INameOfOperation:
                    callSites.Add(operation.Syntax); break;
                case IInvocationOperation invocation:
                    Call(invocation.TargetMethod, invocation, invocation.IsVirtual
                        ? "IInvocationOperation.TargetMethod: compile-time virtual/interface slot; runtime dispatch not resolved."
                        : "IInvocationOperation.TargetMethod: compile-time method binding.");
                    break;
                case IObjectCreationOperation creation:
                    Call(creation.Constructor, creation, "IObjectCreationOperation.Constructor binding.");
                    if (creation.Type is { } created) UseType(created, Owner(creation.Syntax), creation.Syntax);
                    break;
                case IDynamicInvocationOperation or IDynamicObjectCreationOperation:
                    Call(null, operation, "Dynamic operation: runtime target cannot be statically bound.");
                    break;
                case IPropertyReferenceOperation property:
                    Emit(RelationshipKind.REFERENCES, Owner(property.Syntax), property.Property, property.Syntax, "IPropertyReferenceOperation.Property binding.");
                    var parent = property.Parent;
                    IOperation targetPart = property;
                    while (targetPart.Parent is ITupleOperation or IConversionOperation) targetPart = targetPart.Parent;
                    if (targetPart.Parent is IDeconstructionAssignmentOperation deconstruction && ReferenceEquals(deconstruction.Target, targetPart))
                    {
                        Emit(RelationshipKind.CALLS, Owner(property.Syntax), null, property.Syntax,
                            "Property target in deconstruction: accessor lowering is outside the supported matrix.", related: [property.Property]);
                        break;
                    }
                    var writeOnly = parent is ISimpleAssignmentOperation assignment && ReferenceEquals(assignment.Target, property);
                    var readWrite = parent is ICompoundAssignmentOperation compound && ReferenceEquals(compound.Target, property)
                        || parent is IIncrementOrDecrementOperation;
                    if ((!writeOnly || property.Property.ReturnsByRef || property.Property.ReturnsByRefReadonly) && property.Property.GetMethod is { } getter) Call(getter, property, "Property read invokes the compile-time getter.");
                    if ((writeOnly || readWrite) && property.Property.SetMethod is { } setter) Call(setter, property, "Property write invokes the compile-time setter.");
                    break;
                case IMethodReferenceOperation method:
                    Emit(RelationshipKind.REFERENCES, Owner(method.Syntax), method.Method, method.Syntax, "IMethodReferenceOperation: method group, not an invocation.");
                    break;
                case IFieldReferenceOperation field:
                    Emit(RelationshipKind.REFERENCES, Owner(field.Syntax), field.Field, field.Syntax, "IFieldReferenceOperation.Field binding.");
                    break;
                case IEventReferenceOperation evt:
                    Emit(RelationshipKind.REFERENCES, Owner(evt.Syntax), evt.Event, evt.Syntax, "IEventReferenceOperation.Event binding.");
                    break;
                case IEventAssignmentOperation evtAssignment when evtAssignment.EventReference is IEventReferenceOperation eventReference:
                    Call(evtAssignment.Adds ? eventReference.Event.AddMethod : eventReference.Event.RemoveMethod, evtAssignment,
                        "IEventAssignmentOperation binds the event add/remove accessor.");
                    break;
                case IBinaryOperation { OperatorMethod: { } binaryMethod }:
                    Call(binaryMethod, operation, "IBinaryOperation.OperatorMethod binding."); break;
                case IUnaryOperation { OperatorMethod: { } unaryMethod }:
                    Call(unaryMethod, operation, "IUnaryOperation.OperatorMethod binding."); break;
                case IConversionOperation { OperatorMethod: { } conversionMethod }:
                    Call(conversionMethod, operation, "IConversionOperation.OperatorMethod binding."); break;
            }
            foreach (var child in operation.ChildOperations) Walk(child);
        }
    }

    internal RelationshipSymbol Resolve(ISymbol symbol, SyntaxNode fallback)
    {
        if (symbol is IMethodSymbol method) symbol = (method.ReducedFrom ?? method).OriginalDefinition;
        else symbol = symbol.OriginalDefinition;
        if (symbol is IMethodSymbol { PartialDefinitionPart: { } definition }) symbol = definition;
        var syntax = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? fallback;
        var key = SymbolCollector.DeclarationKey(symbol, syntax, rootPath) ?? GraphIdentity.Canonical("symbol-without-doc-id", symbol.Kind.ToString(), symbol.ToDisplayString());
        var assembly = symbol.ContainingAssembly;
        var context = assembly is not null && assemblyContexts.TryGetValue(assembly, out var mapped) ? mapped : null;
        var id = context is not null ? GraphIdentity.Symbol(context, key)
            : GraphIdentity.Hash(GraphIdentity.Canonical("external", projectId, assembly?.Identity.ToString() ?? "", key));
        return new(id, key, symbol.ToDisplayString(), assembly?.Identity.ToString() ?? "", context);
    }
}
