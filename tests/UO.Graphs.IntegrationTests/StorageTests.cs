using Microsoft.Data.Sqlite;
using UO.Graphs.Core;
using UO.Graphs.Storage.Sqlite;

namespace UO.Graphs.IntegrationTests;

public sealed class StorageTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "uographs-storage-" + Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(directory, "graph.db");

    [Fact]
    public void FailedReplacementRollsBackBothEntitiesAndAnalysis()
    {
        var original = Snapshot("old");
        new GraphStore(Database).Replace(original);
        var replacement = Snapshot("new");
        // The second insert fails AFTER DELETE and analysis/first entity writes.
        replacement = replacement with { Entities = [replacement.Entities[0], replacement.Entities[0]] };
        var exception = Assert.Throws<SqliteException>(() => new GraphStore(Database).Replace(replacement));
        Assert.Equal(19, exception.SqliteErrorCode);
        var status = new GraphStore(Database).Status();
        Assert.Equal("old", status.Analysis.Id);
        Assert.Equal(1, status.EntityCount);
        Assert.Equal("old", Assert.Single(new GraphStore(Database).Find("old").Entities).Id);
        Assert.Empty(new GraphStore(Database).Find("new").Entities);
    }

    [Fact]
    public void FindUsesLiteralParameterizedSearchAndSurvivesReopen()
    {
        new GraphStore(Database).Replace(Snapshot("100%_O'Brien"));
        Assert.Single(new GraphStore(Database).Find("%_o'brien").Entities);
        Assert.Empty(new GraphStore(Database).Find("' OR 1=1 --").Entities);
    }

    [Fact]
    public void QueryDoesNotCreateMissingDatabase()
    {
        Assert.Equal(4, Assert.Throws<GraphException>(() => new GraphStore(Database).Status()).ExitCode);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void UnknownSchemaIsRejectedWithoutChangingExistingData()
    {
        new GraphStore(Database).Replace(Snapshot("old"));
        using (var connection = new SqliteConnection($"Data Source={Database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 99";
            command.ExecuteNonQuery();
        }
        Assert.Equal(6, Assert.Throws<GraphException>(() => new GraphStore(Database).Replace(Snapshot("new"))).ExitCode);
        using var check = new SqliteConnection($"Data Source={Database};Pooling=False");
        check.Open();
        using var query = check.CreateCommand();
        query.CommandText = "SELECT id FROM entities";
        Assert.Equal("old", query.ExecuteScalar());
    }

    [Fact]
    public void OccurrenceConstraintFailureRollsBackRelationshipsAndSnapshot()
    {
        var original = Snapshot("old");
        var symbol = new RelationshipSymbol("old", "T:old", "old", "test", "project");
        var builder = new RelationshipBuilder("old");
        builder.Add(RelationshipKind.REFERENCES, symbol, symbol, "project", Confidence.Exact,
            new("test.cs", 1, 1, 1, 2, "hash"), "storage round trip");
        original = original with { Relationships = builder.Relationships, Occurrences = builder.Occurrences };
        new GraphStore(Database).Replace(original);
        var invalid = original with { Analysis = original.Analysis with { Id = "replacement" },
            Occurrences = [original.Occurrences[0] with { RelationshipId = "missing" }] };
        Assert.Equal(19, Assert.Throws<SqliteException>(() => new GraphStore(Database).Replace(invalid)).SqliteErrorCode);
        var retained = new GraphStore(Database).Find("");
        Assert.Equal("old", retained.Analysis.Id);
        Assert.Equal(original.Relationships, retained.Relationships);
        Assert.Equal(original.Occurrences[0].RelationshipId, Assert.Single(retained.Occurrences).RelationshipId);
    }

    [Fact]
    public void SchemaOneIsReadableAndOnlyUpgradedBySuccessfulReplacement()
    {
        new GraphStore(Database).Replace(Snapshot("old"));
        using (var connection = new SqliteConnection($"Data Source={Database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE occurrences; DROP TABLE relationships; PRAGMA user_version = 1;";
            command.ExecuteNonQuery();
        }
        Assert.Equal("old", new GraphStore(Database).Status().Analysis.Id);
        Assert.Empty(new GraphStore(Database).Find("").Relationships);
        Assert.Equal(1, new GraphStore(Database).Status().SchemaVersion);
        var replacement = Snapshot("new");
        Assert.Throws<SqliteException>(() => new GraphStore(Database).Replace(replacement with { Entities = [replacement.Entities[0], replacement.Entities[0]] }));
        using (var connection = new SqliteConnection($"Data Source={Database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version";
            Assert.Equal(1L, command.ExecuteScalar());
        }
        new GraphStore(Database).Replace(replacement);
        Assert.Equal("new", new GraphStore(Database).Find("").Analysis.Id);
        Assert.Equal(3, new GraphStore(Database).Status().SchemaVersion);
    }

    private static GraphSnapshot Snapshot(string id) => new(
        new(id, DateTimeOffset.UtcNow, "/test.csproj", "0.1.0", "5.0.0", "completeWithinDeclaredScope",
            "notChecked", ["Storage test data"], [], []),
        [new(id, "type", id, id, "T:" + id, "project", Confidence.Exact, [])]);

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
