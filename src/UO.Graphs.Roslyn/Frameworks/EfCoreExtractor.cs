using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using UO.Graphs.Core;
using static UO.Graphs.Roslyn.FrameworkExtractionContext;
using static UO.Graphs.Roslyn.FrameworkSymbols;

namespace UO.Graphs.Roslyn;

internal sealed class EfCoreExtractor : IFrameworkExtractor
{
    public string Id => "ef-core-v1";
    private const string Core = "Microsoft.EntityFrameworkCore";
    private const string Relational = Core + ".Relational";
    public void Extract(FrameworkExtractionContext c)
    {
        if (!Is(c.Compilation.GetTypeByMetadataName(Core + ".DbContext"), Core + ".DbContext", Core)) return;
        foreach (var (type, node) in Types(c))
        {
            var contextBase = Bases(type.BaseType).FirstOrDefault(t => Is(t, Core + ".DbContext", Core));
            if (contextBase is not null)
            {
                Annotate(c, type, node, "efCore", "dbContext", contextBase, "Context is never instantiated; provider, conventions and effective model are unknown.");
                c.Report("efEffectiveModel", node, "DbContext declaration only. No construction, migrations, database connection, SQL translation or query plan evaluation.");
                foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
                    if (property.Type is INamedTypeSymbol set && Is(set, Core + ".DbSet`1", Core))
                    {
                        var site = property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? node;
                        Entity(RelationshipKind.CONTEXT_ENTITY, c.Resolve(type, node), set.TypeArguments[0], site, "DbContext declares DbSet<TEntity>; model Ignore/NotMapped/configuration may override membership.");
                        Entity(RelationshipKind.CONTEXT_ENTITY, c.Resolve(property, site), set.TypeArguments[0], site, "Bound DbSet<TEntity> member type; no table naming convention inferred.");
                    }
            }
            foreach (var contract in type.AllInterfaces.Where(i => Is(i, Core + ".IEntityTypeConfiguration`1", Core)))
                Entity(RelationshipKind.CONFIGURES_ENTITY, c.Resolve(type, node), contract.TypeArguments[0], node,
                    "Bound IEntityTypeConfiguration<TEntity>; declaration does not prove application to any DbContext.");
            foreach (var attr in type.GetAttributes().Where(a => Annotation(a, "TableAttribute")))
                Mapping(attr.ApplicationSyntaxReference?.GetSyntax() ?? node, "TableAttribute", type, null,
                    attr.ConstructorArguments.FirstOrDefault().Value as string,
                    attr.NamedArguments.FirstOrDefault(a => a.Key == "Schema").Value.Value as string, null);
            foreach (var member in type.GetMembers().OfType<IPropertySymbol>())
                foreach (var attr in member.GetAttributes().Where(a => Annotation(a, "ColumnAttribute")))
                    Mapping(attr.ApplicationSyntaxReference?.GetSyntax() ?? node, "ColumnAttribute", type, member.Name,
                        null, null, attr.ConstructorArguments.FirstOrDefault().Value as string);
        }
        foreach (var call in c.Invocations())
        {
            var method = call.TargetMethod;
            if (Is(method.ContainingType, Core + ".ModelBuilder", Core) && method.Name == "Entity")
            {
                var entity = method.TypeArguments.FirstOrDefault() ?? (Unwrap(Argument(call, "type")) as ITypeOfOperation)?.TypeOperand;
                Entity(RelationshipKind.CONTEXT_ENTITY, Owner(c, call.Syntax), entity, call.Syntax, "Bound ModelBuilder.Entity declaration; effective model execution and final membership unknown.");
                if (entity is null) c.Report("efEntity", call.Syntax, "Nonconstant/shared-name entity configuration unresolved.");
                continue;
            }
            if (Is(method.ContainingType, Core + ".RelationalEntityTypeBuilderExtensions", Relational) && method.Name is "ToTable" or "ToView")
            {
                var receiver = Argument(call, "entityTypeBuilder") ?? call.Instance;
                var entity = BuilderEntity(receiver?.Type);
                // Only simple name/schema overloads: builder callbacks and split/owned mappings are not folded into a table.
                var supported = entity is not null && call.Arguments.All(a => a.Parameter?.Name is "entityTypeBuilder" or "name" or "schema");
                Mapping(call.Syntax, method.Name, supported ? entity : null, null,
                    supported ? Constant(Argument(call, "name")) : null, supported ? Constant(Argument(call, "schema")) : null, null,
                    !supported || Constant(Argument(call, "name")) is null ? "Unsupported overload, entity builder or nonconstant mapping name." :
                    Argument(call, "schema") is { ConstantValue.HasValue: false } ? "Runtime schema expression unresolved; literal object name is only a source declaration." : null);
                continue;
            }
            if (Is(method.ContainingType, Core + ".RelationalPropertyBuilderExtensions", Relational) && method.Name == "HasColumnName")
            {
                var receiver = Unwrap(Argument(call, "propertyBuilder") ?? call.Instance);
                var property = receiver as IInvocationOperation;
                var builder = property is null ? null : BuilderEntity(property.Instance?.Type);
                var lambda = property is null ? null : Unwrap(Argument(property, "propertyExpression"));
                var function = lambda as IAnonymousFunctionOperation;
                var returns = function?.Body.Descendants().OfType<IReturnOperation>().ToArray() ?? [];
                var reference = returns.Length == 1 ? Unwrap(returns[0].ReturnedValue) as IPropertyReferenceOperation : null;
                // Only a direct property access on the lambda parameter is an entity member expression.
                var member = reference is not null && Unwrap(reference.Instance) is IParameterReferenceOperation parameter &&
                    SymbolEqualityComparer.Default.Equals(parameter.Parameter, function!.Symbol.Parameters.FirstOrDefault()) ? reference.Property : null;
                var supported = builder is INamedTypeSymbol entityType && member is not null &&
                    Bases(entityType).Any(t => SymbolEqualityComparer.Default.Equals(t, member.ContainingType)) &&
                    Is(property?.TargetMethod.ContainingType, Core + ".Metadata.Builders.EntityTypeBuilder`1", Core);
                Mapping(call.Syntax, "HasColumnName", supported ? builder : null, supported ? member!.Name : null,
                    null, null, Constant(Argument(call, "name")), !supported ? "Property builder alias/string/expression association unresolved." : Argument(call, "name") is { ConstantValue.HasValue: false } ? "Runtime column name expression unresolved." : null);
                continue;
            }
            if (IsModelBuilder(method.ContainingType) || IsModelBuilder(call.Instance?.Type) ||
                call.Arguments.Any(a => IsModelBuilder(a.Value.Type)))
            {
                // Property() is the input to the supported HasColumnName chain; still not an effective model assertion.
                if (method.Name == "Property" && BuilderEntity(call.Instance?.Type) is not null) continue;
                Mapping(call.Syntax, method.Name, null, null, null, null, null,
                    "Model configuration path not evaluated: " + method.ToDisplayString());
            }
        }

        void Entity(RelationshipKind kind, RelationshipSymbol source, ITypeSymbol? entity, SyntaxNode site, string reason)
        {
            var open = entity is not null && Open(entity);
            c.Emit(kind, source, open ? null : entity, site, reason + (open ? " Open generic entity unresolved." : ""));
            if (open) c.Report("efGenericEntity", site, "Open generic entity substitution is not evaluated.");
        }
        void Mapping(SyntaxNode site, string form, ITypeSymbol? type, string? member, string? table, string? schema, string? column, string? problem = null)
        {
            var unresolved = type is null || Open(type) || c.Invalid(site);
            var notes = new List<string> { "Source mapping declaration only; Fluent/annotation precedence, configuration order, Ignore, provider conventions and effective model are not evaluated.",
                "Missing schema is unknown, not dbo/public. No database access, LINQ-to-SQL or query plan inference." };
            if (problem is not null) notes.Add(problem);
            if (Conditional(site)) notes.Add("Conditional mapping; branch execution unknown.");
            if (unresolved) { notes.Add("Entity association unresolved."); table = null; schema = null; column = null; }
            var entity = c.AddNode("modelMapping", form + " " + (type?.ToDisplayString() ?? "<unknown-entity>"), site, form);
            c.Entities[entity.Id] = entity with { Confidence = unresolved || problem is not null ? Confidence.Unresolved : entity.Confidence,
                ModelMapping = new(form, unresolved ? null : type!.ToDisplayString(), member, form == "ToView" ? null : table, schema, column, Conditional(site), notes.ToArray(), form == "ToView" ? table : null) };
            Entity(RelationshipKind.MAPS_ENTITY, Symbol(entity), unresolved ? null : type, site, string.Join(" ", notes));
            c.Report("efMapping", site, string.Join(" ", notes));
        }
    }
    private static bool IsModelBuilder(ITypeSymbol? type) => type is INamedTypeSymbol named &&
        (Is(named, Core + ".ModelBuilder", Core) || named.ContainingAssembly.Name == Core &&
            named.ContainingNamespace.ToDisplayString().StartsWith(Core + ".Metadata.Builders", StringComparison.Ordinal));
    private static ITypeSymbol? BuilderEntity(ITypeSymbol? type) => type is INamedTypeSymbol builder &&
        Is(builder, Core + ".Metadata.Builders.EntityTypeBuilder`1", Core) ? builder.TypeArguments[0] : null;
    private static bool Annotation(AttributeData attr, string name) => Is(attr.AttributeClass,
        "System.ComponentModel.DataAnnotations.Schema." + name, "System.ComponentModel.Annotations");
}
