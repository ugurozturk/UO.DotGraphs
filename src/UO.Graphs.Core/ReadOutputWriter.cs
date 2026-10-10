using System.Text.Json;
using System.Text.Json.Nodes;

namespace UO.Graphs.Core;

public sealed record ReadOutputOptions(string Details = "summary", int? MaxChars = null)
{
    public int Budget => MaxChars ?? 12000;
    public bool Full => Details == "full";
    public void Validate()
    {
        if (Details is not ("summary" or "full")) throw new GraphException(2, "--details must be summary or full.");
        if (Full && MaxChars is not null) throw new GraphException(2, "--details full cannot be combined with --max-chars.");
        if (Budget is < 1 or > 1000000) throw new GraphException(2, "--max-chars must be 1..1000000.");
    }
}

public sealed record ShortProjectContext(string Id, string Path, string TargetFramework, string Configuration,
    string SdkVersion, string LanguageVersion, string[] PreprocessorSymbols, string[]? SkippedTargetFrameworks);
public sealed record ReadAnalysisContext(string Id, DateTimeOffset CreatedAtUtc, string InputPath, string RootPath,
    string ToolVersion, string RoslynVersion, string IdentityVersion, string GeneratedPolicy, bool GeneratorsAllowed,
    string Coverage, string Freshness, FreshnessCheck? FreshnessCheck, DependencyFingerprintSummary? DependencyFingerprint,
    object[] Projects, int TotalProjects, int OmittedProjects);
public sealed record AnalysisDiagnosticSample(string Message, bool Truncated);
public sealed record AnalysisDiagnosticsOutput(int Total, AnalysisDiagnosticSample[] Samples, int Omitted);
public sealed record ReadBudget(int? MaxChars, string Unit = "unicodeScalarValuesIncludingFinalLF", int ActualChars = 0);
public sealed record ReadMetadataSummary(int OmittedDiagnostics, int OmittedProjects, bool ProjectDetailsOmitted);
public sealed record QueryOutput(int SchemaVersion, string SnapshotId, ReadAnalysisContext AnalysisContext,
    QueryCompleteness Completeness, string Stale, bool Truncated, QueryItem[] Items, QueryDiagnostic[] Diagnostics,
    AnalysisDiagnosticsOutput AnalysisDiagnostics, string Command, string[] Arguments, QueryOptions Options,
    QueryLimits Limits, string Outcome, RelationshipSymbol? ResolvedStart, RelationshipSymbol? ResolvedTarget,
    string Details, ReadBudget Budget, ReadMetadataSummary MetadataSummary);
public sealed record StatusOutput(int ContractVersion, string Command, int SchemaVersion, string Database,
    ReadAnalysisContext Analysis, QueryCompleteness Completeness, int EntityCount, int RelationshipCount,
    int OccurrenceCount, AnalysisDiagnosticsOutput AnalysisDiagnostics, string Details, ReadBudget Budget,
    ReadMetadataSummary MetadataSummary);

public static class ReadOutputWriter
{
    public static DeliveryOutput Query(QueryResult result, ReadOutputOptions? options = null)
    {
        options ??= new();
        options.Validate();
        var query = result.Envelope;
        var analysis = query.AnalysisContext;
        var items = query.Items.ToList();
        var samples = Diagnostics(analysis, options.Full).ToList();
        var reasons = new SortedSet<string>(query.Limits.Reasons, StringComparer.Ordinal);
        // Even an ambiguous candidate remains a whole record, never an implicit selection.
        var minimumItems = items.Count > 0 ? 1 : 0;
        while (true)
        {
            var context = Context(analysis, options.Full, RelevantProjects(query, items));
            var diagnostics = new AnalysisDiagnosticsOutput(analysis.Diagnostics.Length, samples.ToArray(), analysis.Diagnostics.Length - samples.Count);
            var document = Object(new QueryOutput(GraphJson.QueryContractVersion, query.SnapshotId, context,
                query.Completeness, query.Stale, query.Truncated || reasons.Count > 0, items.ToArray(), query.Diagnostics,
                diagnostics, query.Command, query.Arguments, query.Options,
                query.Limits with { Returned = items.Count, Reasons = reasons.ToArray() }, query.Outcome,
                query.ResolvedStart, query.ResolvedTarget, options.Details, new(options.Full ? null : options.Budget),
                new(diagnostics.Omitted, context.OmittedProjects, !options.Full)));
            var text = OutputJson.SerializeCounted(document);
            if (options.Full || OutputJson.Characters(text) <= options.Budget) return new(text, result.ExitCode);
            if (samples.Count > 0) { samples.RemoveAt(samples.Count - 1); continue; }
            if (items.Count > minimumItems)
            {
                var excess = OutputJson.Characters(text) - options.Budget;
                var removed = 0;
                do
                {
                    removed += OutputJson.Characters(JsonSerializer.Serialize(items[^1], OutputJson.Options)) + 1;
                    items.RemoveAt(items.Count - 1);
                } while (items.Count > minimumItems && removed < excess);
                reasons.Add("maxChars");
                continue;
            }
            throw TooSmall(options, document, "query");
        }
    }

    public static DeliveryOutput Status(AnalysisSnapshot analysis, int schemaVersion, string database,
        int entityCount, int relationshipCount, int occurrenceCount, ReadOutputOptions? options = null)
    {
        options ??= new();
        options.Validate();
        var samples = Diagnostics(analysis, options.Full).ToList();
        var projects = analysis.Projects.OrderBy(p => p.Id, StringComparer.Ordinal).Take(options.Full ? int.MaxValue : 10).ToList();
        while (true)
        {
            var context = Context(analysis, options.Full, projects.Select(p => p.Id).ToHashSet(StringComparer.Ordinal));
            var diagnostics = new AnalysisDiagnosticsOutput(analysis.Diagnostics.Length, samples.ToArray(), analysis.Diagnostics.Length - samples.Count);
            var document = Object(new StatusOutput(GraphJson.StatusContractVersion, "status", schemaVersion, database,
                context, new(analysis.Coverage, analysis.Limitations), entityCount, relationshipCount, occurrenceCount,
                diagnostics, options.Details, new(options.Full ? null : options.Budget),
                new(diagnostics.Omitted, context.OmittedProjects, !options.Full)));
            var text = OutputJson.SerializeCounted(document);
            if (options.Full || OutputJson.Characters(text) <= options.Budget) return new(text, 0);
            if (samples.Count > 0) { samples.RemoveAt(samples.Count - 1); continue; }
            if (projects.Count > 0) { projects.RemoveAt(projects.Count - 1); continue; }
            throw TooSmall(options, document, "status");
        }
    }

    private static GraphException TooSmall(ReadOutputOptions options, JsonObject document, string command)
    {
        // The reported budget is itself serialized: stabilize its digit width too.
        var required = OutputJson.Characters(OutputJson.SerializeCounted(document));
        while (true)
        {
            document["budget"]!["maxChars"] = required;
            var measured = OutputJson.Characters(OutputJson.SerializeCounted(document));
            if (measured == required) break;
            required = measured;
        }
        return new(2, $"--max-chars {options.Budget} cannot fit the minimum {command} envelope for this selection ({required} characters including final LF). Increase --max-chars. No JSON was emitted.");
    }

    private static IEnumerable<AnalysisDiagnosticSample> Diagnostics(AnalysisSnapshot analysis, bool full) =>
        analysis.Diagnostics.Order(StringComparer.Ordinal).Take(full ? int.MaxValue : 3).Select(message =>
        {
            if (full) return new AnalysisDiagnosticSample(message, false);
            var runes = message.EnumerateRunes().Take(301).ToArray();
            return new AnalysisDiagnosticSample(string.Concat(runes.Take(300).Select(r => r.ToString())), runes.Length > 300);
        });

    private static ReadAnalysisContext Context(AnalysisSnapshot analysis, bool full, HashSet<string> relevant)
    {
        var projects = analysis.Projects.Where(p => full || relevant.Contains(p.Id)).OrderBy(p => p.Id, StringComparer.Ordinal)
            .Select(p => full ? (object)p : new ShortProjectContext(p.Id, p.Path, p.TargetFramework, p.Configuration,
                p.SdkVersion, p.LanguageVersion, p.PreprocessorSymbols, p.SkippedTargetFrameworks)).ToArray();
        return new(analysis.Id, analysis.CreatedAtUtc, analysis.InputPath, analysis.RootPath, analysis.ToolVersion,
            analysis.RoslynVersion, analysis.IdentityVersion, analysis.GeneratedPolicy, analysis.GeneratorsAllowed,
            analysis.Coverage, analysis.Freshness, analysis.FreshnessCheck, analysis.DependencyFingerprint,
            projects, analysis.Projects.Length, analysis.Projects.Length - projects.Length);
    }

    private static HashSet<string> RelevantProjects(QueryEnvelope query, IEnumerable<QueryItem> items)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        void Symbol(RelationshipSymbol? symbol) { if (symbol?.ProjectId is { } id) ids.Add(id); }
        Symbol(query.ResolvedStart);
        Symbol(query.ResolvedTarget);
        foreach (var item in items)
        {
            Symbol(item.Symbol);
            if (item.Entity is { } entity) ids.Add(entity.ProjectId);
            foreach (var step in item.Path)
            {
                ids.Add(step.Relationship.ContextId);
                Symbol(step.Relationship.Source);
                Symbol(step.Relationship.Target);
                foreach (var evidence in step.Evidence)
                {
                    ids.Add(evidence.ContextId);
                    foreach (var related in evidence.RelatedSymbols ?? []) Symbol(related);
                }
            }
        }
        return ids;
    }

    private static JsonObject Object(object value) => JsonSerializer.SerializeToNode(value, OutputJson.Options)!.AsObject();
}
