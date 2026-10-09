namespace UO.Graphs.Core;

// Names and directions are a public, centrally defined contract.
public enum RelationshipKind { DECLARES, PROJECT_REFERENCES, INHERITS, IMPLEMENTS, OVERRIDES, USES_TYPE, REFERENCES, CALLS,
    REGISTERS, IMPLEMENTED_BY, USES_FACTORY, INJECTS, HANDLED_BY, ACTIVATES,
    HANDLES, DISPATCHES, PUBLISHES, CONSUMES, POSSIBLE_TARGET, BEHAVIOR_FOR, POSSIBLE_BEHAVIOR, APP_SERVICE_CONTRACT, MODULE_DEPENDS_ON, REPOSITORY_ENTITY, CONTEXT_ENTITY, CONFIGURES_ENTITY, MAPS_ENTITY, HTTP_CONTRACT, INVOKES_HTTP, POSSIBLE_ENDPOINT }

public sealed record RelationshipDefinition(string SourceRole, string TargetRole, string Meaning);

public static class RelationshipContract
{
    public static IReadOnlyDictionary<RelationshipKind, RelationshipDefinition> Definitions { get; } =
        new Dictionary<RelationshipKind, RelationshipDefinition>
        {
            [RelationshipKind.APP_SERVICE_CONTRACT] = new("application service", "application service interface", "Bound ABP service contract; not exposure or registration."),
            [RelationshipKind.MODULE_DEPENDS_ON] = new("ABP module", "declared module dependency", "Bound DependsOn typeof declaration; runtime module loading is unknown."),
            [RelationshipKind.REPOSITORY_ENTITY] = new("repository use or declaration", "entity type", "Bound ABP generic repository entity argument; no table, schema or query is inferred."),
            [RelationshipKind.CONTEXT_ENTITY] = new("DbContext or DbSet member", "entity type", "Bound DbSet or ModelBuilder.Entity declaration; effective model membership is not proven."),
            [RelationshipKind.CONFIGURES_ENTITY] = new("configuration class", "entity type", "Bound IEntityTypeConfiguration contract; execution and context application are unknown."),
            [RelationshipKind.MAPS_ENTITY] = new("model mapping declaration", "entity type", "Explicit mapping source declaration; not effective model, physical table access or SQL."),
            [RelationshipKind.HTTP_CONTRACT] = new("Refit interface method", "HTTP contract declaration", "Bound Refit HTTP attribute; not a server endpoint or an executed request."),
            [RelationshipKind.INVOKES_HTTP] = new("caller", "Refit HTTP contract declaration", "Compile-time call to an attributed interface method; runtime client configuration is unknown."),
            [RelationshipKind.POSSIBLE_ENDPOINT] = new("HTTP client contract", "endpoint candidate", "Remote endpoint selection unresolved without authority/configuration evidence; route similarity is insufficient."),
            [RelationshipKind.DECLARES] = new("container", "declaration", "Direct lexical or project ownership; partial declarations share a target."),
            [RelationshipKind.PROJECT_REFERENCES] = new("referencing project", "referenced project", "Evaluated project reference in the selected analysis context."),
            [RelationshipKind.INHERITS] = new("derived type", "direct base type or interface", "Direct class or interface inheritance; not transitive."),
            [RelationshipKind.IMPLEMENTS] = new("implementing type or member", "interface or interface member", "Roslyn interface implementation; inherited member mappings may repeat by context."),
            [RelationshipKind.OVERRIDES] = new("overriding member", "overridden member", "Immediate overridden method, property or event."),
            [RelationshipKind.USES_TYPE] = new("containing declaration", "bound type", "Type bound at a source syntax or operation, including constructed generic arguments."),
            [RelationshipKind.REFERENCES] = new("containing declaration", "bound symbol", "Symbol reference or method group; does not assert invocation."),
            [RelationshipKind.CALLS] = new("caller", "compile-time target", "Statically bound invocation, constructor or accessor; no runtime dispatch guarantee."),
            [RelationshipKind.REGISTERS] = new("registration declaration", "service type", "Statically declared service type; neither execution nor container membership is proven."),
            [RelationshipKind.IMPLEMENTED_BY] = new("registration declaration", "implementation type", "Declared implementation type; no runtime selection or closed generic construction is asserted."),
            [RelationshipKind.USES_FACTORY] = new("registration declaration", "factory method or lambda", "Statically bound factory expression; returned implementation remains unresolved."),
            [RelationshipKind.INJECTS] = new("constructor or handler", "parameter service type", "Declared constructor parameter or explicit service binding; inferred for implicit HTTP service candidates. Never selects a runtime implementation."),
            [RelationshipKind.HANDLED_BY] = new("endpoint declaration", "action or handler", "Statically associated handler; endpoint activation is not proven."),
            [RelationshipKind.ACTIVATES] = new("controller action", "constructor candidate", "Inferred controller constructor candidate; runtime activator and constructor selection are unknown."),
            [RelationshipKind.HANDLES] = new("handler method", "message contract", "Bound request handler contract; not registration or runtime execution."),
            [RelationshipKind.CONSUMES] = new("consumer method", "message contract", "Bound notification handler or consumer contract; not subscription or delivery."),
            [RelationshipKind.DISPATCHES] = new("caller", "message contract", "Bound dispatch/send API and statically known message contract; runtime recipient is separate."),
            [RelationshipKind.PUBLISHES] = new("caller", "message contract", "Bound publish API and message contract; zero, one or many runtime recipients may exist."),
            [RelationshipKind.POSSIBLE_TARGET] = new("message contract", "handler or consumer method", "Inferred contract-compatible declaration in indexed scope; registration, topology and runtime selection remain unknown."),
            [RelationshipKind.BEHAVIOR_FOR] = new("behavior method", "message contract", "Bound pipeline behavior contract; order and continuation execution are not proven."),
            [RelationshipKind.POSSIBLE_BEHAVIOR] = new("message contract", "behavior method", "Possible pipeline participant; no runtime order, continuation or mandatory execution guarantee.")
        };
}

// Endpoints also represent metadata and excluded/unindexed source symbols without fake declarations.
public sealed record RelationshipSymbol(string Id, string DeclarationId, string DisplayName,
    string AssemblyIdentity, string? ProjectId, string? EntityId = null);

public sealed record GraphRelationship(string Id, RelationshipKind Kind, RelationshipSymbol Source,
    RelationshipSymbol? Target, string ContextId, Confidence Confidence);

public sealed record RelationshipOccurrence(string Id, string RelationshipId, string AnalysisId,
    string ContextId, SourceEvidence Source, string Reason, string? BoundTarget,
    RelationshipSymbol[] RelatedSymbols);

public sealed class RelationshipBuilder(string analysisId)
{
    private readonly Dictionary<string, GraphRelationship> relationships = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RelationshipOccurrence> occurrences = new(StringComparer.Ordinal);
    public GraphRelationship[] Relationships => relationships.Values.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();
    public RelationshipOccurrence[] Occurrences => occurrences.Values.OrderBy(o => o.Id, StringComparer.Ordinal).ToArray();

    public void Add(RelationshipKind kind, RelationshipSymbol source, RelationshipSymbol? target,
        string contextId, Confidence confidence, SourceEvidence evidence, string reason,
        string? boundTarget = null, params RelationshipSymbol[] relatedSymbols)
    {
        if (!RelationshipContract.Definitions.ContainsKey(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A relationship needs an evidence rule.", nameof(reason));
        if ((confidence == Confidence.Unresolved) != (target is null))
            throw new ArgumentException("Unresolved relationships have no asserted target; resolved relationships require one.");
        var id = GraphIdentity.Hash(GraphIdentity.Canonical("relationship", kind.ToString(), source.Id, target?.Id ?? "", contextId, confidence.ToString()));
        relationships.TryAdd(id, new(id, kind, source, target, contextId, confidence));
        var occurrenceId = GraphIdentity.Hash(GraphIdentity.Canonical("occurrence", id, evidence.File,
            evidence.StartLine.ToString(), evidence.StartColumn.ToString(), evidence.EndLine.ToString(),
            evidence.EndColumn.ToString(), evidence.Sha256, reason, boundTarget ?? ""));
        occurrences.TryAdd(occurrenceId, new(occurrenceId, id, analysisId, contextId, evidence, reason, boundTarget, relatedSymbols));
    }
}
