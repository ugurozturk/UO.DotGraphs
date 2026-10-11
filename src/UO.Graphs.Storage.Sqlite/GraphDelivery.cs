using System.Text.Json;
using Microsoft.Data.Sqlite;
using UO.Graphs.Core;

namespace UO.Graphs.Storage.Sqlite;

public sealed partial class GraphStore
{
    public DeliveryGraph ReadDelivery(string? symbol, DeliveryOptions options, bool lexical = false, bool checkFreshness = false, bool requireFresh = false)
    {
        options.Validate();
        if (options.Profile == "backend" && (symbol is null || lexical))
            throw new GraphException(2, "Backend export requires an exact --symbol selection.");
        if (symbol is not null && (string.IsNullOrWhiteSpace(symbol) || symbol.Length > 1024))
            throw new GraphException(2, "Symbol text must contain 1..1024 UTF-16 code units.");
        using var connection = Open(readOnly: true);
        using var transaction = connection.BeginTransaction(deferred: true);
        if (Version(connection) != SchemaVersion)
            throw new GraphException(4, "Context/export require SQLite schema 3. Explicitly re-index; reads never migrate.");
        try
        {
            var analysis = FingerprintScanner.Apply(ReadAnalysis(connection, transaction), checkFreshness || requireFresh ? ReadFingerprint(connection, transaction) : null, checkFreshness, requireFresh);
            if (analysis is null || analysis.Projects is null || analysis.Limitations is null || analysis.Diagnostics is null)
                throw new GraphException(6, "Invalid analysis record in database.");
            return new DeliverySession(connection, transaction, analysis, options).Run(symbol, lexical);
        }
        catch (JsonException ex) { throw new GraphException(6, $"Invalid graph JSON in database: {ex.Message}"); }
    }

    private sealed class DeliverySession(SqliteConnection connection, SqliteTransaction transaction,
        AnalysisSnapshot analysis, DeliveryOptions options)
    {
        private readonly SortedSet<string> reasons = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DeliveryNode> nodes = new(StringComparer.Ordinal);
        private readonly List<DeliveryEdge> edges = [];
        private readonly HashSet<string> examinedIds = new(StringComparer.Ordinal);
        private int examined;

        public DeliveryGraph Run(string? value, bool lexical)
        {
            DeliveryNode? selected = null;
            if (value is not null)
            {
                var resolution = new QuerySession(connection, transaction, analysis, "symbol", value, null,
                    QueryOptions.Defaults("symbol") with { Limit = Math.Min(options.Limit, options.MaxNodes) }).Run();
                if (resolution.ExitCode != 0)
                {
                    foreach (var reason in resolution.Envelope.Limits.Reasons) reasons.Add(reason);
                    var candidates = resolution.Envelope.Items.Select(i => new DeliveryNode(i.Symbol, i.Entity, 0)).ToArray();
                    var match = "exact";
                    if (resolution.ExitCode == 8 && lexical)
                    {
                        match = "lexical";
                        candidates = Search(value);
                    }
                    return Finish(candidates.Length > 0 ? "selectionRequired" : "symbolNotFound", match,
                        candidates.Length > 0 ? 7 : 8, candidates);
                }
                var item = resolution.Envelope.Items.Single();
                selected = new(item.Symbol, item.Entity, 0);
                nodes.Add(selected.Symbol.Id, selected);
                var queue = new Queue<DeliveryNode>();
                queue.Enqueue(selected);
                while (queue.TryDequeue(out var current))
                {
                    var adjacent = ReadEdges(current.Symbol.Id);
                    foreach (var edge in adjacent)
                    {
                        if (examined == options.MaxEdges) { reasons.Add("maxEdges"); return Finish(); }
                        examined++;
                        if (!examinedIds.Add(edge.Id)) continue;
                        if (current.Distance == options.Depth) { reasons.Add("depth"); continue; }
                        if (edges.Count == options.Limit) { reasons.Add("limit"); return Finish(); }
                        var next = edge.Source.Id == current.Symbol.Id ? edge.Target?.Id : edge.Source.Id;
                        if (next is not null && !nodes.ContainsKey(next))
                        {
                            if (nodes.Count == options.MaxNodes) { reasons.Add("maxNodes"); continue; }
                            var node = Node(next, current.Distance + 1);
                            nodes.Add(next, node);
                            queue.Enqueue(node);
                        }
                        edges.Add(Evidence(edge));
                    }
                }
            }
            else
            {
                using (var sql = Command("SELECT id FROM nodes ORDER BY id LIMIT $limit"))
                {
                    sql.Parameters.AddWithValue("$limit", options.MaxNodes + 1);
                    var ids = new List<string>();
                    using (var reader = sql.ExecuteReader()) while (reader.Read()) ids.Add(reader.GetString(0));
                    if (ids.Count > options.MaxNodes) reasons.Add("maxNodes");
                    foreach (var id in ids.Take(options.MaxNodes)) nodes.Add(id, Node(id, 0));
                }
                foreach (var edge in ReadEdges(null))
                {
                    if (examined == options.MaxEdges) { reasons.Add("maxEdges"); break; }
                    examined++;
                    if (!nodes.ContainsKey(edge.Source.Id) || edge.Target is { } t && !nodes.ContainsKey(t.Id))
                    { reasons.Add("maxNodes"); continue; }
                    if (edges.Count == options.Limit) { reasons.Add("limit"); break; }
                    edges.Add(Evidence(edge));
                }
            }
            return Finish();

            DeliveryGraph Finish(string outcome = "ok", string match = "exact", int exit = 0, DeliveryNode[]? candidates = null) =>
                new(analysis, options, value is null ? "fullGraph" : "neighborhood", outcome, match, selected, candidates ?? [],
                    nodes.Values.OrderBy(n => n.Distance).ThenBy(n => n.Symbol.Id, StringComparer.Ordinal).ToArray(),
                    edges.OrderBy(e => Priority(e.Relationship.Kind)).ThenBy(e => e.Relationship.Id, StringComparer.Ordinal).ToArray(),
                    reasons.ToArray(), examined, exit);
        }

        // Lexical suggestions only, never a guessed semantic selection. Bound both scanned rows and returned candidates.
        private DeliveryNode[] Search(string value)
        {
            using var sql = Command("SELECT id, name, qualified_name FROM entities ORDER BY id LIMIT 10001");
            var ids = new List<string>();
            var scanned = 0;
            using (var reader = sql.ExecuteReader())
                while (reader.Read())
                {
                    if (++scanned > 10000) { reasons.Add("searchScanLimit"); break; }
                    if (!reader.GetString(1).Contains(value, StringComparison.OrdinalIgnoreCase) &&
                        !reader.GetString(2).Contains(value, StringComparison.OrdinalIgnoreCase)) continue;
                    if (ids.Count == Math.Min(options.Limit, options.MaxNodes)) { reasons.Add("candidateLimit"); break; }
                    ids.Add(reader.GetString(0));
                }
            return ids.Select(id => Node(id, 0)).ToArray();
        }

        private static int Priority(RelationshipKind kind) => kind switch
        { RelationshipKind.CALLS => 0, RelationshipKind.OVERRIDES or RelationshipKind.IMPLEMENTS or RelationshipKind.INHERITS => 1,
            RelationshipKind.USES_TYPE or RelationshipKind.PROJECT_REFERENCES => 2, RelationshipKind.REFERENCES => 3, _ => 4 };

        private List<GraphRelationship> ReadEdges(string? id)
        {
            var where = id is null ? "" : "WHERE (source_id = $id OR target_id = $id) ";
            if (options.Profile == "backend")
                where = "WHERE source_id = $id AND kind IN (" +
                    string.Join(",", DeliveryOptions.BackendEdgeTypes.Select((_, i) => "$kind" + i)) +
                    ") AND " + PropertyCallFilter(options.Calls) + " ";
            // Indexed adjacency; full export explicitly scans graph records. No source files are opened here.
            // Backend filters precede LIMIT and budgets, so excluded rows do not consume traversal work.
            using var sql = Command("SELECT id, source_id, target_id, json FROM relationships " +
                where +
                "ORDER BY CASE kind WHEN 'CALLS' THEN 0 WHEN 'OVERRIDES' THEN 1 WHEN 'IMPLEMENTS' THEN 1 WHEN 'INHERITS' THEN 1 WHEN 'USES_TYPE' THEN 2 WHEN 'PROJECT_REFERENCES' THEN 2 WHEN 'REFERENCES' THEN 3 ELSE 4 END, id LIMIT $limit");
            if (id is not null) sql.Parameters.AddWithValue("$id", id);
            if (options.Profile == "backend")
                for (var i = 0; i < DeliveryOptions.BackendEdgeTypes.Count; i++)
                    sql.Parameters.AddWithValue("$kind" + i, DeliveryOptions.BackendEdgeTypes[i].ToString());
            sql.Parameters.AddWithValue("$limit", options.MaxEdges - examined + 1);
            using var reader = sql.ExecuteReader();
            var result = new List<GraphRelationship>();
            while (reader.Read())
            {
                var edge = Read<GraphRelationship>(reader.GetString(3));
                if (edge.Id != reader.GetString(0) || edge.Source?.Id != reader.GetString(1) ||
                    edge.Target?.Id != (reader.IsDBNull(2) ? null : reader.GetString(2)) ||
                    (edge.Confidence == Confidence.Unresolved) != (edge.Target is null))
                    throw new GraphException(6, "Inconsistent relationship record in database.");
                result.Add(edge);
            }
            return result;
        }

        private DeliveryNode Node(string id, int distance)
        {
            using var sql = Command("SELECT n.json, e.json FROM nodes n LEFT JOIN entities e ON e.id = n.entity_id WHERE n.id = $id");
            sql.Parameters.AddWithValue("$id", id);
            using var reader = sql.ExecuteReader();
            if (!reader.Read()) throw new GraphException(6, "Dangling graph endpoint in database.");
            var symbol = Read<RelationshipSymbol>(reader.GetString(0));
            var entity = reader.IsDBNull(1) ? null : Read<GraphEntity>(reader.GetString(1));
            if (symbol.Id != id || symbol.EntityId is not null && entity?.Id != symbol.EntityId)
                throw new GraphException(6, "Inconsistent graph node in database.");
            return new(symbol, entity, distance);
        }

        private DeliveryEdge Evidence(GraphRelationship edge)
        {
            using var sql = Command("SELECT json FROM occurrences WHERE relationship_id = $id ORDER BY id LIMIT $limit");
            sql.Parameters.AddWithValue("$id", edge.Id);
            sql.Parameters.AddWithValue("$limit", options.EvidenceLimit + 1);
            using var reader = sql.ExecuteReader();
            var sites = new List<RelationshipOccurrence>();
            while (reader.Read())
            {
                var site = Read<RelationshipOccurrence>(reader.GetString(0));
                if (site.RelationshipId != edge.Id || site.AnalysisId != analysis.Id || site.Source is null || string.IsNullOrWhiteSpace(site.Reason))
                    throw new GraphException(6, "Inconsistent relationship evidence in database.");
                sites.Add(site);
            }
            if (sites.Count == 0) throw new GraphException(6, "Relationship has no source evidence in database.");
            var cut = sites.Count > options.EvidenceLimit;
            if (cut) reasons.Add("evidenceLimit");
            return new(edge, sites.Take(options.EvidenceLimit).ToArray(), cut);
        }

        private SqliteCommand Command(string text)
        {
            var sql = connection.CreateCommand(); sql.Transaction = transaction; sql.CommandText = text; return sql;
        }
        private static T Read<T>(string json) where T : class => JsonSerializer.Deserialize<T>(json, GraphJson.Options)
            ?? throw new GraphException(6, "Null graph record in database.");
    }
}
