using System.Text.Json;

namespace UO.Graphs.IntegrationTests;

public sealed partial class CliTests
{
    [Fact]
    public async Task Phase10ApprovedIncrementalSkillWorkflowAcrossProcesses()
    {
        // Controlled fixture authorization belongs to this test. A deployed skill
        // must obtain equivalent input/trust/cost authorization from its caller.
        var arguments = new[] { "index", Path.Combine(Root, "samples/HelloGraph/HelloGraph.slnx"),
            "--incremental", "--trust-project", "--db", Database,
            "--root", Path.Combine(Root, "samples/HelloGraph"), "--configuration", "Debug",
            "--framework", "net10.0", "--generated", "exclude" };
        var initial = await Run(arguments);
        Assert.True(initial.Code == 0, initial.Error);
        var update = await Run(arguments);
        Assert.True(update.Code == 0, update.Error);
        using var indexed = JsonDocument.Parse(update.Output);
        Assert.Equal("incremental", indexed.RootElement.GetProperty("mode").GetString());
        Assert.Equal("entireSelectedProjectClosure", indexed.RootElement.GetProperty("granularity").GetString());
        var analysis = indexed.RootElement.GetProperty("analysis");
        Assert.Equal("partial", analysis.GetProperty("coverage").GetString());
        Assert.False(analysis.GetProperty("generatorsAllowed").GetBoolean());

        var status = await Run("status", "--db", Database, "--check-freshness");
        Assert.True(status.Code == 0, status.Error);
        using var state = JsonDocument.Parse(status.Output);
        Assert.Equal(analysis.GetProperty("id").GetString(), state.RootElement.GetProperty("analysis").GetProperty("id").GetString());
        Assert.Contains(state.RootElement.GetProperty("analysis").GetProperty("freshness").GetString(), new[] { "fresh", "stale", "unknown" });

        var find = await Run("query", "find", "Greet", "--limit", "10", "--format", "json", "--db", Database);
        Assert.True(find.Code == 0, find.Error);
        using var found = JsonDocument.Parse(find.Output);
        var entity = Assert.Single(found.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("entity")), e => e.GetProperty("name").GetString() == "Greet");
        var id = entity.GetProperty("id").GetString()!;
        var context = await Run("context", id, "--max-chars", "12000", "--max-nodes", "8",
            "--limit", "12", "--depth", "1", "--max-edges", "100", "--evidence-limit", "2", "--db", Database);
        Assert.True(context.Code == 0, context.Error);
        using var details = JsonDocument.Parse(context.Output);
        Assert.Equal(id, details.RootElement.GetProperty("selected").GetProperty("id").GetString());
        Assert.Equal("partial", details.RootElement.GetProperty("completeness").GetProperty("coverage").GetString());
        Assert.Equal("notChecked", details.RootElement.GetProperty("stale").GetString());
        Assert.NotEmpty(details.RootElement.GetProperty("warnings").EnumerateArray());

        var callers = await Run("query", "callers", id, "--depth", "1", "--limit", "10",
            "--max-edges", "100", "--evidence-limit", "2", "--db", Database);
        Assert.True(callers.Code == 0, callers.Error);
        using var called = JsonDocument.Parse(callers.Output);
        var welcome = Assert.Single(called.RootElement.GetProperty("items").EnumerateArray(),
            i => i.GetProperty("entity").GetProperty("name").GetString() == "Welcome");
        var step = Assert.Single(welcome.GetProperty("path").EnumerateArray());
        Assert.Equal("exact", step.GetProperty("relationship").GetProperty("confidence").GetString());
        Assert.NotEmpty(step.GetProperty("evidence").EnumerateArray());
    }
}
