using System.Text.Json;
using Microsoft.Data.Sqlite;
using UO.Graphs.Core;

namespace UO.Graphs.Storage.Sqlite;

public sealed partial class GraphStore(string databasePath)
{
    public const int SchemaVersion = 3;
    // Deterministic SQLITE_FULL fault injection for transaction regression tests.
    internal int? TestMaximumPageCount { get; init; }
    public string DatabasePath => CanonicalDatabasePath(databasePath);

    public void Replace(GraphSnapshot snapshot, CancellationToken cancellationToken = default, WriterLease? writer = null)
    {
        using var ownedWriter = writer is null ? AcquireWriter() : null;
        if (writer is not null) writer.Validate(DatabasePath);
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = DatabasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var connection = Open(readOnly: false);
        using (var journal = connection.CreateCommand())
        {
            journal.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = FULL";
            journal.ExecuteScalar();
        }
        if (TestMaximumPageCount is { } pageLimit)
        {
            using var limit = connection.CreateCommand();
            limit.CommandText = "PRAGMA max_page_count = " + pageLimit.ToString(System.Globalization.CultureInfo.InvariantCulture);
            limit.ExecuteScalar();
        }
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS analysis (singleton INTEGER PRIMARY KEY CHECK(singleton = 1), json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS fingerprint (singleton INTEGER PRIMARY KEY CHECK(singleton = 1), json TEXT NOT NULL);
            DELETE FROM fingerprint;
            CREATE TABLE IF NOT EXISTS entities (id TEXT PRIMARY KEY NOT NULL, name TEXT NOT NULL, qualified_name TEXT NOT NULL, json TEXT NOT NULL);
            DROP TABLE IF EXISTS occurrences;
            DROP TABLE IF EXISTS relationships;
            DROP TABLE IF EXISTS nodes;
            CREATE TABLE nodes (id TEXT PRIMARY KEY NOT NULL, entity_id TEXT REFERENCES entities(id), json TEXT NOT NULL CHECK(json_valid(json)));
            CREATE TABLE relationships (id TEXT PRIMARY KEY NOT NULL,
                source_id TEXT NOT NULL REFERENCES nodes(id), target_id TEXT REFERENCES nodes(id),
                kind TEXT NOT NULL, confidence TEXT NOT NULL CHECK(confidence IN ('exact','inferred','unresolved')),
                json TEXT NOT NULL CHECK(json_valid(json)),
                CHECK ((confidence = 'unresolved') = (target_id IS NULL)));
            CREATE TABLE occurrences (id TEXT PRIMARY KEY NOT NULL, relationship_id TEXT NOT NULL REFERENCES relationships(id), json TEXT NOT NULL CHECK(json_valid(json)));
            CREATE INDEX IF NOT EXISTS ix_entities_name ON entities(name, id);
            CREATE INDEX IF NOT EXISTS ix_entities_qualified ON entities(qualified_name, id);
            CREATE INDEX ix_relationships_source ON relationships(source_id, kind, confidence, id);
            CREATE INDEX ix_relationships_target ON relationships(target_id, kind, confidence, id);
            CREATE INDEX ix_occurrences_relationship ON occurrences(relationship_id, id);
            PRAGMA user_version = 3;
            DELETE FROM entities;
            DELETE FROM analysis;
            """;
        command.ExecuteNonQuery();
        command.CommandText = "INSERT INTO analysis(singleton, json) VALUES(1, $json)";
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(snapshot.Analysis, GraphJson.Options));
        command.ExecuteNonQuery();
        if (snapshot.Fingerprint is not null)
        {
            command.CommandText = "INSERT INTO fingerprint(singleton, json) VALUES(1, $json)";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(snapshot.Fingerprint, GraphJson.Options));
            command.ExecuteNonQuery();
        }
        foreach (var entity in snapshot.Entities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            command.CommandText = "INSERT INTO entities(id, name, qualified_name, json) VALUES($id, $name, $qualified, $json)";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$id", entity.Id);
            command.Parameters.AddWithValue("$name", entity.Name);
            command.Parameters.AddWithValue("$qualified", entity.QualifiedName);
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(entity, GraphJson.Options));
            command.ExecuteNonQuery();
        }
        // A node can be a source entity or a metadata/excluded endpoint. Never fake an entity.
        var nodes = snapshot.Entities.ToDictionary(e => e.Id,
            e => new RelationshipSymbol(e.Id, e.DocumentationId, e.QualifiedName, "", e.ProjectId, e.Id), StringComparer.Ordinal);
        foreach (var edge in snapshot.Relationships)
            foreach (var endpoint in new[] { edge.Source, edge.Target }.OfType<RelationshipSymbol>())
                nodes[endpoint.Id] = endpoint;
        foreach (var node in nodes.Values.OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            command.CommandText = "INSERT INTO nodes(id, entity_id, json) VALUES($id, $entity, $json)";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$id", node.Id);
            command.Parameters.AddWithValue("$entity", (object?)node.EntityId ?? DBNull.Value);
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(node, GraphJson.Options));
            command.ExecuteNonQuery();
        }
        foreach (var relationship in snapshot.Relationships)
        {
            cancellationToken.ThrowIfCancellationRequested();
            command.CommandText = "INSERT INTO relationships(id, source_id, target_id, kind, confidence, json) VALUES($id, $source, $target, $kind, $confidence, $json)";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$id", relationship.Id);
            command.Parameters.AddWithValue("$source", relationship.Source.Id);
            command.Parameters.AddWithValue("$target", (object?)relationship.Target?.Id ?? DBNull.Value);
            command.Parameters.AddWithValue("$kind", relationship.Kind.ToString());
            command.Parameters.AddWithValue("$confidence", relationship.Confidence.ToString().ToLowerInvariant());
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(relationship, GraphJson.Options));
            command.ExecuteNonQuery();
        }
        foreach (var occurrence in snapshot.Occurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            command.CommandText = "INSERT INTO occurrences(id, relationship_id, json) VALUES($id, $relationship, $json)";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$id", occurrence.Id);
            command.Parameters.AddWithValue("$relationship", occurrence.RelationshipId);
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(occurrence, GraphJson.Options));
            command.ExecuteNonQuery();
        }
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
    }

    public (AnalysisSnapshot Analysis, int EntityCount, int RelationshipCount, int OccurrenceCount, int SchemaVersion) Status(bool checkFreshness = false, bool requireFresh = false)
    {
        using var connection = Open(readOnly: true);
        using var transaction = connection.BeginTransaction(deferred: true);
        var analysis = FingerprintScanner.Apply(ReadAnalysis(connection, transaction), checkFreshness || requireFresh ? ReadFingerprint(connection, transaction) : null, checkFreshness, requireFresh);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM entities";
        var entityCount = Convert.ToInt32(command.ExecuteScalar());
        if (Version(connection) == 1) return (analysis, entityCount, 0, 0, 1);
        command.CommandText = "SELECT COUNT(*) FROM relationships";
        var relationshipCount = Convert.ToInt32(command.ExecuteScalar());
        command.CommandText = "SELECT COUNT(*) FROM occurrences";
        return (analysis, entityCount, relationshipCount, Convert.ToInt32(command.ExecuteScalar()), Version(connection));
    }

    public GraphSnapshot Find(string text)
    {
        using var connection = Open(readOnly: true);
        using var transaction = connection.BeginTransaction(deferred: true);
        var analysis = ReadAnalysis(connection, transaction);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // instr treats %, _ and quotes literally. SQLite lower is ASCII case folding.
        command.CommandText = "SELECT json FROM entities WHERE instr(lower(name), lower($text)) > 0 OR instr(lower(qualified_name), lower($text)) > 0 ORDER BY id";
        command.Parameters.AddWithValue("$text", text);
        using var reader = command.ExecuteReader();
        var entities = new List<GraphEntity>();
        while (reader.Read()) entities.Add(JsonSerializer.Deserialize<GraphEntity>(reader.GetString(0), GraphJson.Options)!);
        reader.Close();
        if (Version(connection) == 1) return new(analysis, entities.ToArray());
        // Phase 3 evidence attachment only; graph traversal/query operators belong to Phase 4.
        var ids = entities.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        command.CommandText = "SELECT json FROM relationships ORDER BY id";
        command.Parameters.Clear();
        var relationships = new List<GraphRelationship>();
        using (var edges = command.ExecuteReader())
            while (edges.Read())
            {
                var relationship = JsonSerializer.Deserialize<GraphRelationship>(edges.GetString(0), GraphJson.Options)!;
                if (text.Length == 0 || ids.Contains(relationship.Source.Id) || relationship.Target is { } target && ids.Contains(target.Id))
                    relationships.Add(relationship);
            }
        var relationIds = relationships.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        command.CommandText = "SELECT json FROM occurrences ORDER BY id";
        var occurrences = new List<RelationshipOccurrence>();
        using (var sites = command.ExecuteReader())
            while (sites.Read())
            {
                var occurrence = JsonSerializer.Deserialize<RelationshipOccurrence>(sites.GetString(0), GraphJson.Options)!;
                if (relationIds.Contains(occurrence.RelationshipId)) occurrences.Add(occurrence);
            }
        return new(analysis, entities.ToArray()) { Relationships = relationships.ToArray(), Occurrences = occurrences.ToArray(), Fingerprint = ReadFingerprint(connection, transaction) };
    }


    private static string CanonicalDatabasePath(string path)
    {
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full)!;
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is not null) current = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                ?? throw new IOException($"Cannot resolve database path: {current}");
        }
        return current;
    }

    public WriterLease AcquireWriter()
    {
        var path = DatabasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        foreach (var suffix in new[] { ".writer.lock", "-wal", "-shm", "-journal" })
            if (new FileInfo(path + suffix).LinkTarget is not null)
                throw new GraphException(6, $"Refusing symlink database sidecar: {path + suffix}");
        try { return new(path, new FileStream(path + ".writer.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)); }
        catch (IOException ex) { throw new GraphException(10, $"Another writer holds the index lease (or lock is unavailable): {ex.Message}"); }
    }

    public sealed class WriterLease : IDisposable
    {
        private readonly string path;
        private FileStream? stream;
        internal WriterLease(string path, FileStream stream) { this.path = path; this.stream = stream; }
        internal void Validate(string expected)
        {
            if (stream is null || path != expected) throw new InvalidOperationException("Invalid writer lease.");
        }
        public void Dispose() { stream?.Dispose(); stream = null; }
    }

    public IndexFingerprint? Fingerprint()
    {
        using var connection = Open(readOnly: true);
        using var transaction = connection.BeginTransaction(deferred: true);
        return ReadFingerprint(connection, transaction);
    }

    private static IndexFingerprint? ReadFingerprint(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'fingerprint'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0) return null;
        command.CommandText = "SELECT json FROM fingerprint WHERE singleton = 1";
        try { return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<IndexFingerprint>(json, GraphJson.Options) : null; }
        catch (JsonException ex) { throw new GraphException(6, $"Invalid fingerprint JSON: {ex.Message}"); }
    }

    private SqliteConnection Open(bool readOnly)
    {
        if (readOnly && !File.Exists(databasePath))
            throw new GraphException(4, $"No index at {DatabasePath}. Run index first.");
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        try
        {
            connection.Open();
            using (var foreignKeys = connection.CreateCommand())
            {
                foreignKeys.CommandText = "PRAGMA foreign_keys = ON";
                foreignKeys.ExecuteNonQuery();
            }
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version";
            var version = Convert.ToInt32(command.ExecuteScalar());
            if (version != SchemaVersion && version != 1 && version != 2)
            {
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
                var empty = Convert.ToInt32(command.ExecuteScalar()) == 0;
                if (version == 0 && empty && readOnly)
                    throw new GraphException(4, "Database has no committed snapshot. Run index to create one.");
                if (version != 0 || !empty)
                    throw new GraphException(6, $"Unsupported SQLite schema version {version}; expected {SchemaVersion}. Database was not modified.");
            }
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    private static int Version(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static AnalysisSnapshot ReadAnalysis(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT json FROM analysis WHERE singleton = 1";
        var json = command.ExecuteScalar() as string ?? throw new GraphException(4, "Database has no completed index.");
        return JsonSerializer.Deserialize<AnalysisSnapshot>(json, GraphJson.Options)!;
    }
}
