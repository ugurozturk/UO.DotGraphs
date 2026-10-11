using System.Text.Json;
using Microsoft.Data.Sqlite;
using UO.Graphs.Core;

namespace UO.Graphs.Storage.Sqlite;

public sealed partial class GraphStore
{
    // Formal accessor identities come from Roslyn MethodKind, never display/name heuristics.
    private static string PropertyCallFilter(string calls)
    {
        const string property = "(kind = 'CALLS' AND (json_extract(json, '$.target.declarationId') GLOB 'accessor:*:PropertyGet' OR json_extract(json, '$.target.declarationId') GLOB 'accessor:*:PropertySet'))";
        return calls switch
        {
            "exclude-properties" => $"NOT COALESCE({property}, 0)",
            "only-properties" => $"COALESCE({property}, 0)",
            _ => "1"
        };
    }

    public QueryResult Query(string command, string value, string? target = null, QueryOptions? options = null, bool checkFreshness = false, bool requireFresh = false)
    {
        if (command is not ("find" or "symbol" or "callers" or "callees" or "dependencies" or "path" or "impact") ||
            string.IsNullOrWhiteSpace(value) || command == "path" && string.IsNullOrWhiteSpace(target))
            throw new GraphException(2, "Invalid query command or arguments.");
        options ??= QueryOptions.Defaults(command);
        options = options with { EdgeTypes = options.EdgeTypes.Distinct().Order().ToArray() };
        options.Validate(command);
        using var connection = Open(readOnly: true);
        using var transaction = connection.BeginTransaction(deferred: true);
        if (Version(connection) != SchemaVersion)
            throw new GraphException(4, "Graph queries require SQLite schema 3. Explicitly re-index to upgrade; query never writes or migrates.");
        try
        {
            var analysis = FingerprintScanner.Apply(ReadAnalysis(connection, transaction), checkFreshness || requireFresh ? ReadFingerprint(connection, transaction) : null, checkFreshness, requireFresh);
            if (analysis is null || analysis.Id is null || analysis.Projects is null || analysis.Limitations is null || analysis.Diagnostics is null)
                throw new GraphException(6, "Invalid analysis record in database.");
            return new QuerySession(connection, transaction, analysis, command, value, target, options).Run();
        }
        catch (JsonException ex) { throw new GraphException(6, $"Invalid graph JSON in database: {ex.Message}"); }
    }

    private sealed class QuerySession(SqliteConnection connection, SqliteTransaction transaction,
        AnalysisSnapshot analysis, string command, string value, string? target, QueryOptions options)
    {
        private readonly SortedSet<string> reasons = new(StringComparer.Ordinal);
        private readonly List<QueryDiagnostic> diagnostics = [];
        private readonly Dictionary<string, (RelationshipOccurrence[] Items, bool Truncated)> evidence = new(StringComparer.Ordinal);
        private int examined;
        private RelationshipSymbol? resolvedStart, resolvedTarget;
        private DirectQueryPage? page;
        private sealed record Walk(string Id, int Distance, Walk? Previous = null, GraphRelationship? Edge = null);

        public QueryResult Run()
        {
            if (options.Selection?.Snapshot is { } expected && expected != analysis.Id)
                throw new GraphException(2, "Compact page snapshot mismatch; restart the query against the selected snapshot.");
            if (command == "find")
            {
                var matches = Search(value, exact: false, options.Limit + 1);
                if (matches.Count > options.Limit) reasons.Add("limit");
                return Finish(matches.Take(options.Limit).Select(e => Item(new(e.Id, 0))).ToArray(), "ok");
            }
            var start = Resolve(value);
            if (start.Failure is { } failure) return failure;
            resolvedStart = Item(new(start.Id!, 0)).Symbol;
            if (options.Selection is not null) return Direct(start.Id!);
            if (command == "symbol") return Finish([Item(new(start.Id!, 0))], "ok");
            string? destination = null;
            if (command == "path")
            {
                var end = Resolve(target!);
                if (end.Failure is { } endFailure) return endFailure;
                destination = end.Id;
                resolvedTarget = Item(new(end.Id!, 0)).Symbol;
            }
            if (command == "impact") diagnostics.Add(new("potentialImpact",
                "Potential static dependency impact only; no runtime execution or dispatch guarantee. REFERENCES/DECLARES do not propagate impact."));
            var root = new Walk(start.Id!, 0);
            if (destination == root.Id) return Finish([Item(root)], "found");
            var visited = new HashSet<string>(StringComparer.Ordinal) { root.Id };
            var queue = new Queue<Walk>();
            queue.Enqueue(root);
            var results = new List<Walk>();
            var selfAdded = false;
            while (queue.TryDequeue(out var current))
            {
                foreach (var edge in Neighbors(current.Id))
                {
                    if (examined == options.MaxEdges)
                    {
                        reasons.Add("maxEdges");
                        return Complete();
                    }
                    examined++;
                    var next = edge.Source.Id == current.Id && options.Direction != TraversalDirection.Incoming
                        ? edge.Target!.Id : edge.Source.Id;
                    var self = next == root.Id && current.Distance == 0 && !selfAdded;
                    if (visited.Contains(next) && !(self && destination is null && command != "impact")) continue;
                    if (current.Distance == options.Depth)
                    {
                        reasons.Add("depth");
                        continue;
                    }
                    var step = new Walk(next, current.Distance + 1, current, edge);
                    if (next == destination) return Finish([Item(step)], "found");
                    if (destination is null)
                    {
                        if (results.Count == options.Limit)
                        {
                            reasons.Add("limit");
                            return Complete();
                        }
                        results.Add(step);
                    }
                    if (self) selfAdded = true;
                    if (visited.Add(next)) queue.Enqueue(step);
                }
            }
            return Complete();

            QueryResult Complete()
            {
                if (destination is not null)
                {
                    diagnostics.Add(new("pathNotFound", "No path found within this snapshot, edge policy and search bounds; this does not prove runtime unreachability."));
                    return Finish([], reasons.Count > 0 ? "boundedSearchIncomplete" : "notFound");
                }
                return Finish(results.Select(Item).ToArray(), "ok");
            }
        }

        private QueryResult Direct(string root)
        {
            var selection = options.Selection!;
            var cursorScope = GraphIdentity.Hash(GraphIdentity.Canonical(analysis.Id, root, command,
                options.Direction.ToString(), string.Join(",", options.EdgeTypes), selection.Calls, options.IncludeInferred.ToString()));
            var after = "";
            if (selection.After is { } cursor)
            {
                var parts = cursor.Split('.');
                if (parts.Length != 2 || parts[1] != cursorScope)
                    throw new GraphException(2, "Compact cursor does not match snapshot, root or query policy; restart the query.");
                after = parts[0];
            }
            var direction = options.Direction switch
            {
                TraversalDirection.Outgoing => "source_id = $root",
                TraversalDirection.Incoming => "target_id = $root",
                _ => "(source_id = $root OR target_id = $root)"
            };
            var types = string.Join(",", options.EdgeTypes.Select((_, i) => "$kind" + i));
            var scope = $"{direction} AND kind IN ({types}) AND confidence IN ('exact', 'unresolved', $confidence)";
            var filter = PropertyCallFilter(selection.Calls);
            using var counts = Sql($"SELECT count(*), COALESCE(sum(CASE WHEN {filter} THEN 1 ELSE 0 END),0), COALESCE(sum(CASE WHEN {filter} AND id <= $after THEN 1 ELSE 0 END),0) FROM relationships WHERE {scope}");
            int total, eligible, before;
            using (var reader = counts.ExecuteReader())
            {
                reader.Read(); total = reader.GetInt32(0); eligible = reader.GetInt32(1); before = reader.GetInt32(2);
            }
            if (after.Length > 0)
            {
                using var exists = Sql($"SELECT count(*) FROM relationships WHERE {scope} AND {filter} AND id = $after");
                if (Convert.ToInt32(exists.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
                    throw new GraphException(2, "Compact cursor relationship is absent from this selection.");
            }
            var edges = new List<GraphRelationship>();
            using (var sql = Sql($"SELECT id, source_id, target_id, kind, confidence, json FROM relationships WHERE {scope} AND {filter} AND id > $after ORDER BY id LIMIT $limit"))
            using (var reader = sql.ExecuteReader())
                while (reader.Read())
                {
                    var edge = Read<GraphRelationship>(reader.GetString(5));
                    if (edge.Source is null || edge.Id != reader.GetString(0) || edge.Source.Id != reader.GetString(1) ||
                        edge.Target?.Id != (reader.IsDBNull(2) ? null : reader.GetString(2)) ||
                        edge.Kind.ToString() != reader.GetString(3) || edge.Confidence.ToString().ToLowerInvariant() != reader.GetString(4) ||
                        (edge.Target is null) != (edge.Confidence == Confidence.Unresolved))
                        throw new GraphException(6, "Inconsistent relationship record in database.");
                    edges.Add(edge);
                }
            examined = edges.Count;
            var items = new List<QueryItem>();
            foreach (var edge in edges)
            {
                var reversed = edge.Source.Id != root;
                var next = reversed ? edge.Source.Id : edge.Target?.Id;
                var sites = Evidence(edge.Id);
                using var count = Command("SELECT count(*) FROM occurrences WHERE relationship_id = $id");
                count.Parameters.AddWithValue("$id", edge.Id);
                var step = new QueryStep(root, next, reversed, edge, sites.Items, sites.Truncated)
                { EvidenceTotal = Convert.ToInt32(count.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) };
                var node = Item(new(next ?? root, 0));
                items.Add(node with { Distance = 1, Path = [step] });
            }
            if (eligible > before + items.Count) reasons.Add(options.MaxEdges < options.Limit ? "maxEdges" : "limit");
            page = new(eligible, total - eligible, before, items.Count, selection.Calls, cursorScope);
            return Finish(items.ToArray(), "ok");

            SqliteCommand Sql(string text)
            {
                var sql = Command(text);
                sql.Parameters.AddWithValue("$root", root);
                sql.Parameters.AddWithValue("$after", after);
                sql.Parameters.AddWithValue("$confidence", options.IncludeInferred ? "inferred" : "exact");
                sql.Parameters.AddWithValue("$limit", Math.Min(options.Limit, options.MaxEdges));
                for (var i = 0; i < options.EdgeTypes.Length; i++) sql.Parameters.AddWithValue("$kind" + i, options.EdgeTypes[i].ToString());
                return sql;
            }
        }

        private (string? Id, QueryResult? Failure) Resolve(string input)
        {
            using var byId = Command("SELECT id FROM nodes WHERE id = $id");
            byId.Parameters.AddWithValue("$id", input);
            if (byId.ExecuteScalar() is string id) return (id, null);
            // Probe at least two even when limit=1: a hidden second candidate must not select the first.
            var matches = Search(input, exact: true, Math.Max(2, options.Limit + 1));
            if (matches.Count == 1) return (matches[0].Id, null);
            if (matches.Count == 0)
            {
                diagnostics.Add(new("symbolNotFound", $"Symbol '{input}' was not found in this snapshot. Use query find to search names."));
                return (null, Finish([], "symbolNotFound", 8));
            }
            if (matches.Count > options.Limit) reasons.Add("limit");
            diagnostics.Add(new("ambiguousSymbol", $"Name '{input}' matches multiple symbols. Select an exact symbol ID from items; increase --limit if candidates are truncated."));
            return (null, Finish(matches.Take(options.Limit).Select(e => Item(new(e.Id, 0))).ToArray(), "ambiguousSymbol", 7));
        }

        private List<GraphEntity> Search(string text, bool exact, int limit)
        {
            using var sql = Command(exact
                ? "SELECT json FROM entities WHERE name = $text OR qualified_name = $text ORDER BY id LIMIT $limit"
                : "SELECT json FROM entities WHERE instr(lower(name), lower($text)) > 0 OR instr(lower(qualified_name), lower($text)) > 0 ORDER BY id LIMIT $limit");
            sql.Parameters.AddWithValue("$text", text);
            sql.Parameters.AddWithValue("$limit", limit);
            using var reader = sql.ExecuteReader();
            var result = new List<GraphEntity>();
            while (reader.Read()) result.Add(Read<GraphEntity>(reader.GetString(0)));
            return result;
        }

        private IEnumerable<GraphRelationship> Neighbors(string id)
        {
            // Only trusted enum-derived SQL structure; all values, including IDs and kinds, are parameters.
            var direction = options.Direction switch
            {
                TraversalDirection.Outgoing => "source_id = $id",
                TraversalDirection.Incoming => "target_id = $id",
                _ => "(source_id = $id OR target_id = $id)"
            };
            var types = string.Join(",", options.EdgeTypes.Select((_, i) => "$kind" + i));
            using var sql = Command($"SELECT id, source_id, target_id, json FROM relationships WHERE {direction} AND kind IN ({types}) AND confidence IN ('exact', $confidence) AND target_id IS NOT NULL ORDER BY id LIMIT $limit");
            sql.Parameters.AddWithValue("$id", id);
            sql.Parameters.AddWithValue("$confidence", options.IncludeInferred ? "inferred" : "exact");
            sql.Parameters.AddWithValue("$limit", options.MaxEdges - examined + 1);
            for (var i = 0; i < options.EdgeTypes.Length; i++) sql.Parameters.AddWithValue("$kind" + i, options.EdgeTypes[i].ToString());
            using var reader = sql.ExecuteReader();
            while (reader.Read())
            {
                var edge = Read<GraphRelationship>(reader.GetString(3));
                if (edge.Source is null || edge.Target is null || edge.Id != reader.GetString(0) ||
                    edge.Source.Id != reader.GetString(1) || edge.Target.Id != reader.GetString(2) ||
                    !options.EdgeTypes.Contains(edge.Kind) || edge.Confidence == Confidence.Unresolved ||
                    edge.Confidence == Confidence.Inferred && !options.IncludeInferred)
                    throw new GraphException(6, "Inconsistent relationship record in database.");
                yield return edge;
            }
        }

        private QueryItem Item(Walk walk)
        {
            using var sql = Command("SELECT n.json, e.json FROM nodes n LEFT JOIN entities e ON e.id = n.entity_id WHERE n.id = $id");
            sql.Parameters.AddWithValue("$id", walk.Id);
            RelationshipSymbol symbol;
            GraphEntity? entity;
            using (var reader = sql.ExecuteReader())
            {
                if (!reader.Read()) throw new GraphException(6, "Dangling graph endpoint in database.");
                symbol = Read<RelationshipSymbol>(reader.GetString(0));
                entity = reader.IsDBNull(1) ? null : Read<GraphEntity>(reader.GetString(1));
                if (symbol.Id != walk.Id || symbol.EntityId is not null && entity?.Id != symbol.EntityId)
                    throw new GraphException(6, "Inconsistent graph node in database.");
            }
            var steps = new List<QueryStep>();
            for (var node = walk; node.Previous is { } previous && node.Edge is { } edge; node = previous)
            {
                var sites = Evidence(edge.Id);
                steps.Add(new(previous.Id, node.Id, edge.Source.Id != previous.Id, edge, sites.Items, sites.Truncated));
            }
            steps.Reverse();
            return new(symbol, entity, walk.Distance, steps.ToArray());
        }

        private (RelationshipOccurrence[] Items, bool Truncated) Evidence(string id)
        {
            if (evidence.TryGetValue(id, out var cached)) return cached;
            using var sql = Command("SELECT json FROM occurrences WHERE relationship_id = $id ORDER BY id LIMIT $limit");
            sql.Parameters.AddWithValue("$id", id);
            sql.Parameters.AddWithValue("$limit", options.EvidenceLimit + 1);
            using var reader = sql.ExecuteReader();
            var sites = new List<RelationshipOccurrence>();
            while (reader.Read())
            {
                var site = Read<RelationshipOccurrence>(reader.GetString(0));
                if (site.RelationshipId != id || site.AnalysisId != analysis.Id || site.Source is null || string.IsNullOrWhiteSpace(site.Reason))
                    throw new GraphException(6, "Inconsistent relationship evidence in database.");
                sites.Add(site);
            }
            if (sites.Count == 0) throw new GraphException(6, "Relationship has no source evidence in database.");
            var truncated = sites.Count > options.EvidenceLimit;
            if (truncated) reasons.Add("evidenceLimit");
            return evidence[id] = (sites.Take(options.EvidenceLimit).ToArray(), truncated);
        }

        private QueryResult Finish(QueryItem[] items, string outcome, int exitCode = 0)
        {
            return new(new(GraphJson.QueryContractVersion, analysis.Id, analysis,
                new(analysis.Coverage, analysis.Limitations), analysis.Freshness, reasons.Count > 0, items,
                diagnostics.ToArray(), "query." + command, target is null ? [value] : [value, target], options,
                new(options.Depth, options.Limit, options.MaxEdges, options.EvidenceLimit, items.Length, examined, reasons.ToArray()), outcome) { ResolvedStart = resolvedStart, ResolvedTarget = resolvedTarget, Page = page }, exitCode);
        }

        private SqliteCommand Command(string text)
        {
            var sql = connection.CreateCommand();
            sql.Transaction = transaction;
            sql.CommandText = text;
            return sql;
        }

        private static T Read<T>(string json) where T : class => JsonSerializer.Deserialize<T>(json, GraphJson.Options)
            ?? throw new GraphException(6, "Null graph record in database.");
    }
}
