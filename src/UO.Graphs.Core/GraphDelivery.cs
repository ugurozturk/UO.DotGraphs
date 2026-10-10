namespace UO.Graphs.Core;

// Delivery has its own version, independent of query/status JSON and SQLite.
public sealed record DeliveryOptions(int MaxNodes = 40, int Limit = 80, int Depth = 1,
    int MaxEdges = 1000, int EvidenceLimit = 2, int MaxChars = 12000, bool IncludeSource = false)
{
    public void Validate()
    {
        if (MaxNodes is < 1 or > 10000 || Limit is < 1 or > 10000 || Depth is < 0 or > 10 ||
            MaxEdges is < 1 or > 100000 || EvidenceLimit is < 1 or > 100 || MaxChars is < 1 or > 1000000)
            throw new GraphException(2, "Delivery bounds: max-nodes/limit 1..10000, depth 0..10, max-edges 1..100000, evidence-limit 1..100, max-chars 1..1000000.");
    }
}

public sealed record DeliveryNode(RelationshipSymbol Symbol, GraphEntity? Entity, int Distance);
public sealed record DeliveryEdge(GraphRelationship Relationship, RelationshipOccurrence[] Evidence, bool EvidenceTruncated);
public sealed record DeliveryGraph(AnalysisSnapshot Analysis, DeliveryOptions Options, string Scope,
    string Outcome, string Match, DeliveryNode? Selected, DeliveryNode[] Candidates,
    DeliveryNode[] Nodes, DeliveryEdge[] Edges, string[] Reasons, int ExaminedEdges, int ExitCode);
public sealed record DeliveryOutput(string Text, int ExitCode);
