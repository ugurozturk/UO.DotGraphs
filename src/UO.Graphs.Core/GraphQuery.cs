namespace UO.Graphs.Core;

public enum TraversalDirection { Outgoing, Incoming, Both }

public sealed record QueryOptions(TraversalDirection Direction, RelationshipKind[] EdgeTypes,
    int Depth = 1, int Limit = 100, bool IncludeInferred = false, int MaxEdges = 10000, int EvidenceLimit = 20)
{
    public static readonly RelationshipKind[] Dependencies = [RelationshipKind.CALLS, RelationshipKind.USES_TYPE,
        RelationshipKind.INHERITS, RelationshipKind.IMPLEMENTS, RelationshipKind.OVERRIDES, RelationshipKind.PROJECT_REFERENCES,
        RelationshipKind.HANDLED_BY, RelationshipKind.ACTIVATES, RelationshipKind.INJECTS,
        RelationshipKind.REGISTERS, RelationshipKind.IMPLEMENTED_BY, RelationshipKind.USES_FACTORY,
        RelationshipKind.HANDLES, RelationshipKind.CONSUMES, RelationshipKind.DISPATCHES, RelationshipKind.PUBLISHES,
        RelationshipKind.POSSIBLE_TARGET, RelationshipKind.BEHAVIOR_FOR, RelationshipKind.POSSIBLE_BEHAVIOR,
        RelationshipKind.APP_SERVICE_CONTRACT, RelationshipKind.MODULE_DEPENDS_ON, RelationshipKind.REPOSITORY_ENTITY,
        RelationshipKind.CONTEXT_ENTITY, RelationshipKind.CONFIGURES_ENTITY, RelationshipKind.MAPS_ENTITY,
        RelationshipKind.HTTP_CONTRACT, RelationshipKind.INVOKES_HTTP, RelationshipKind.POSSIBLE_ENDPOINT];

    public static QueryOptions Defaults(string command) => new(
        command is "callers" or "impact" ? TraversalDirection.Incoming : TraversalDirection.Outgoing,
        command is "callers" or "callees" ? [RelationshipKind.CALLS] : [.. Dependencies],
        command is "path" or "impact" ? 5 : 1);

    public void Validate(string command)
    {
        if (!Enum.IsDefined(Direction) || EdgeTypes.Length == 0 || EdgeTypes.Any(k => !Enum.IsDefined(k)) ||
            Depth is < 0 or > 100 || Limit is < 1 or > 10000 || MaxEdges is < 1 or > 1000000 || EvidenceLimit is < 1 or > 1000)
            throw new GraphException(2, "Invalid query bounds: depth 0..100, limit 1..10000, max-edges 1..1000000, evidence-limit 1..1000.");
        if (command == "impact" && (Direction != TraversalDirection.Incoming || EdgeTypes.Except(Dependencies).Any()))
            throw new GraphException(2, "Impact requires incoming dependency edges; REFERENCES and DECLARES are not impact propagation rules.");
    }
}

public sealed record QueryCompleteness(string Coverage, string[] Limitations);
public sealed record QueryDiagnostic(string Code, string Message);
public sealed record QueryStep(string FromId, string ToId, bool Reversed, GraphRelationship Relationship,
    RelationshipOccurrence[] Evidence, bool EvidenceTruncated);
public sealed record QueryItem(RelationshipSymbol Symbol, GraphEntity? Entity, int Distance, QueryStep[] Path);
public sealed record QueryLimits(int Depth, int Limit, int MaxEdges, int EvidenceLimit,
    int Returned, int ExaminedEdges, string[] Reasons);
public sealed record QueryEnvelope(int SchemaVersion, string SnapshotId, AnalysisSnapshot AnalysisContext,
    QueryCompleteness Completeness, string Stale, bool Truncated, QueryItem[] Items,
    QueryDiagnostic[] Diagnostics, string Command, string[] Arguments, QueryOptions Options, QueryLimits Limits,
    string Outcome);

public sealed record QueryResult(QueryEnvelope Envelope, int ExitCode = 0);
