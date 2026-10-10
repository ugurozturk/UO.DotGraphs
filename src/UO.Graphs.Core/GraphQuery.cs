namespace UO.Graphs.Core;

public enum TraversalDirection { Outgoing, Incoming, Both }

public sealed record QueryOptions(TraversalDirection Direction, RelationshipKind[] EdgeTypes,
    int Depth = 1, int Limit = 100, bool IncludeInferred = false, int MaxEdges = 10000, int EvidenceLimit = 20)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public DirectQuerySelection? Selection { get; init; }
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
        if (Selection is { } selection)
        {
            if (command is not ("callees" or "callers" or "dependencies") || Depth != 1)
                throw new GraphException(2, "Compact paging/filtering requires callees, callers or dependencies with --depth 1.");
            if (selection.Calls is not ("all" or "exclude-properties" or "only-properties"))
                throw new GraphException(2, "--calls must be all, exclude-properties or only-properties.");
            if (selection.After is not null && selection.Snapshot is null)
                throw new GraphException(2, "--after requires --snapshot from the previous compact page.");
        }
        if (!Enum.IsDefined(Direction) || EdgeTypes.Length == 0 || EdgeTypes.Any(k => !Enum.IsDefined(k)) ||
            Depth is < 0 or > 100 || Limit is < 1 or > 10000 || MaxEdges is < 1 or > 1000000 || EvidenceLimit is < 1 or > 1000)
            throw new GraphException(2, "Invalid query bounds: depth 0..100, limit 1..10000, max-edges 1..1000000, evidence-limit 1..1000.");
        if (command == "impact" && (Direction != TraversalDirection.Incoming || EdgeTypes.Except(Dependencies).Any()))
            throw new GraphException(2, "Impact requires incoming dependency edges; REFERENCES and DECLARES are not impact propagation rules.");
    }
}

public sealed record QueryCompleteness(string Coverage, string[] Limitations);
public sealed record QueryDiagnostic(string Code, string Message);
public sealed record DirectQuerySelection(string Calls = "all", string? After = null, string? Snapshot = null);
public sealed record DirectQueryPage(int Eligible, int Filtered, int BeforePage, int Collected, string Calls, string CursorScope);
public sealed record QueryStep(string FromId, string? ToId, bool Reversed, GraphRelationship Relationship,
    RelationshipOccurrence[] Evidence, bool EvidenceTruncated)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int? EvidenceTotal { get; init; }
}
public sealed record QueryItem(RelationshipSymbol Symbol, GraphEntity? Entity, int Distance, QueryStep[] Path);
public sealed record QueryLimits(int Depth, int Limit, int MaxEdges, int EvidenceLimit,
    int Returned, int ExaminedEdges, string[] Reasons);
public sealed record QueryEnvelope(int SchemaVersion, string SnapshotId, AnalysisSnapshot AnalysisContext,
    QueryCompleteness Completeness, string Stale, bool Truncated, QueryItem[] Items,
    QueryDiagnostic[] Diagnostics, string Command, string[] Arguments, QueryOptions Options, QueryLimits Limits,
    string Outcome)
{
    public RelationshipSymbol? ResolvedStart { get; init; }
    public RelationshipSymbol? ResolvedTarget { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public DirectQueryPage? Page { get; init; }
}

public sealed record QueryResult(QueryEnvelope Envelope, int ExitCode = 0);
