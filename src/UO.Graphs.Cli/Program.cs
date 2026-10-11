using System.Text.Json;
using Microsoft.Data.Sqlite;
using UO.Graphs.Core;
using UO.Graphs.Roslyn;
using UO.Graphs.Storage.Sqlite;

return await Cli.RunAsync(args);

internal static class Cli
{
    private static readonly string Help = $"""
        UO.Graphs {ProductVersion.Current} — local C# symbol index
        Usage:
          uographs --help
          uographs doctor
          uographs index <project.csproj|solution.sln|solution.slnx> --trust-project [--db <path>]
          uographs status [--db <path>]
          uographs query find <text> [--limit <n>] [--format json] [--db <path>]
          uographs query symbol <symbol-id|exact-name> [--db <path>]
          uographs query callers|callees|dependencies|impact <symbol-id|exact-name> [options]
          uographs query path <from-id> <to-id> [options]
          uographs context <symbol-id-or-text> [--max-chars <n>] [--format json] [--include-source]
          uographs export --format json|ndjson|mermaid|dot [--symbol <id-or-exact-name>]
                          [--profile graph|backend] [--output <new-path>]

        Context/export: --db <path> --max-nodes <1..10000> --limit <1..10000>
                        --depth <0..10> --max-edges <1..100000> --evidence-limit <1..100>
        Defaults context/export: nodes 40/1000, relationships 80/5000, depth 1,
        examined edges 1000/10000, evidence per edge 2/20. Export without --symbol
        selects the full graph within these bounds; --depth requires --symbol.
        Backend export requires --symbol, follows outgoing flow relationships, includes
        inferred candidates; defaults: depth 4, nodes 40, relationships 80, edges 1000,
        evidence 2. --calls all|exclude-properties|only-properties (default exclude-properties)
        filters Roslyn property accessors including custom bodies before traversal budgets.
        Graph profile is the default. --output requires an existing parent directory and
        refuses existing files/symlinks; successful export goes to file, summary to stderr.
        Context max-chars: 12000 (1..1000000), entire stdout in Unicode scalar values,
        including final LF. Too-small envelope: exit 2, no stdout. No token count.
        Include-source: context only; at most 3 C# snippets, 12 lines/1200 chars each,
        1 MiB/file; excludes secret/config paths and requires snapshot hash match.

        Query/status output: --details summary|full (default summary), --max-chars <1..1000000>.
        Summary defaults to 12000 Unicode scalars including final LF; compact JSON.
        Summary samples metadata before pruning whole results; omissions are explicit.
        Full includes all analysis metadata/diagnostics once; incompatible with --max-chars.
        Query/status contract 6; index/doctor 4; context/export 1; SQLite schema 3.
        Query --view standard|compact (default standard). Compact contract 1 uses
        shared symbol/relationship/evidence/file tables with document-local references.
        Compact depth-1 callees/callers/dependencies page whole relationships (including
        unresolved observations), ordered by relationship ID, not importance/execution.
        --calls all|exclude-properties|only-properties is an explicit semantic filter.
        Continue with --after <cursor> --snapshot <id> and the same query policy.
        Counts include eligible, filtered, budget omissions and remaining relationships.
        Other compact queries retain standard traversal/ambiguity semantics.
        Query options: --direction outgoing|incoming|both --edge-types CALLS,USES_TYPE,...
                       --depth <0..100> --limit <1..10000> --include-inferred
                       --max-edges <1..1000000> --evidence-limit <1..1000>
                       --format json --db <path>
        Defaults: depth 1 (path/impact 5), limit 100, max-edges 10000, evidence-limit 20.
        Callers: incoming CALLS; callees: outgoing CALLS. Dependencies/path: outgoing
        CALLS,USES_TYPE,INHERITS,IMPLEMENTS,OVERRIDES,PROJECT_REFERENCES.
        Impact: incoming dependency edges only; potential static impact, not runtime proof.
        Path: one bounded shortest path. Inferred excluded; unresolved never traversed.
        Names must be unique or use an exact ID. Use -- before positional text starting '-'.

        Default database: <current-directory>/.uographs/graph.db
        Data: JSON stdout. Progress/errors: stderr. No automatic restore/download.
        --trust-project permits MSBuild evaluation; it is not a sandbox/network block.
        Phase 6: content-based solution-closure incremental indexing; atomic snapshots.
        Index mode: --incremental (default) or --full. Requires --trust-project in both modes.
        Reads: --check-freshness hashes tracked inputs; default notChecked (zero hash I/O).
               --require-fresh implies check and rejects stale/unknown with exit 9.
        Freshness reads never evaluate MSBuild or restore. Writer contention: exit 10.
        Index options: --configuration <name> --framework <TFM> --root <directory>
                       --allow-generators --generated <include|exclude>
        Multi-target projects require --framework. Generators disabled by default.
        """;

    public static async Task<int> RunAsync(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            var command = Parse(args);
            var store = new GraphStore(command.Database);
            switch (command.Name)
            {
                case "help": Console.WriteLine(Help); return 0;
                case "doctor":
                    var sdk = await ProjectPreparation.GetSdkVersionAsync(Environment.CurrentDirectory);
                    Write(new { contractVersion = GraphJson.DoctorContractVersion, command = "doctor", runtime = Environment.Version.ToString(), sdk,
                        requiredDevelopmentSdk = "10.0.201", automaticRestore = false, targetProjectChecked = false,
                        preparation = "Install the SDK selected by the target global.json and explicitly run dotnet restore <target> in an approved environment. Index checks SDK/assets after --trust-project; doctor does not evaluate projects." });
                    return 0;
                case "index":
                    // The lease spans validation, analysis and commit: a slow writer cannot
                    // overwrite a newer generation published by another indexing process.
                    if (!command.Trust) throw new GraphException(3, "Indexing requires --trust-project before MSBuild evaluation.");
                    using (var writer = store.AcquireWriter())
                    {
                        IndexFingerprint? previous = null;
                        if (!command.Full && File.Exists(command.Database))
                            try { previous = store.Fingerprint(); }
                            catch (GraphException ex) when (ex.ExitCode == 4) { /* No committed snapshot after an interrupted first publication. */ }
                        var run = await new IncrementalIndexer().RunAsync(command.Value!, command.Trust, Console.Error,
                            command.Options!, store.DatabasePath, previous, command.Full, cancellation.Token);
                        if (run.Graph is { } graph) store.Replace(graph, cancellation.Token, writer);
                        var indexed = store.Status();
                        var validationState = run.Fingerprint.Complete ? "fresh" : "unknown";
                        var validatedAnalysis = indexed.Analysis with { Freshness = validationState,
                            FreshnessCheck = new(validationState, "indexEvaluationAndContentValidation", run.FilesHashed, run.BytesHashed,
                                run.FingerprintMilliseconds + run.AnalysisMilliseconds, run.Fingerprint.Limitations) };
                        Write(new { contractVersion = GraphJson.IndexContractVersion, command = "index", database = Path.GetFullPath(command.Database),
                            mode = command.Full ? "full" : "incremental", run.Outcome, granularity = "entireSelectedProjectClosure",
                            run.ProjectsAnalyzed, run.FingerprintMilliseconds, run.AnalysisMilliseconds, run.FilesHashed, run.BytesHashed,
                            dependencyFingerprint = new { run.Fingerprint.Complete, run.Fingerprint.Limitations },
                            analysis = validatedAnalysis, entityCount = indexed.EntityCount,
                            relationshipCount = indexed.RelationshipCount, occurrenceCount = indexed.OccurrenceCount });
                    }
                    return 0;
                case "status":
                    var status = store.Status(command.CheckFreshness, command.RequireFresh);
                    var statusOutput = ReadOutputWriter.Status(status.Analysis, status.SchemaVersion, Path.GetFullPath(command.Database),
                        status.EntityCount, status.RelationshipCount, status.OccurrenceCount, command.ReadOutput);
                    Console.Out.Write(statusOutput.Text);
                    return 0;
                case "context":
                    var context = DeliveryWriter.Context(store.ReadDelivery(command.Value!, command.Delivery!, lexical: true, checkFreshness: command.CheckFreshness, requireFresh: command.RequireFresh));
                    Console.Out.Write(context.Text);
                    return context.ExitCode;
                case "export":
                    var exported = DeliveryWriter.Export(store.ReadDelivery(command.Value, command.Delivery!, checkFreshness: command.CheckFreshness, requireFresh: command.RequireFresh), command.Format);
                    if (command.Output is null || exported.ExitCode != 0) Console.Out.Write(exported.Text);
                    else
                    {
                        WriteExport(command.Output, exported.Text);
                        Console.Error.WriteLine($"uographs: Exported {command.Format} to {command.Output}");
                    }
                    return exported.ExitCode;
                case "find" or "symbol" or "callers" or "callees" or "dependencies" or "path" or "impact":
                    var result = store.Query(command.Name, command.Value!, command.Target, command.QueryOptions, command.CheckFreshness, command.RequireFresh);
                    Console.Out.Write(ReadOutputWriter.Query(result, command.ReadOutput).Text);
                    return result.ExitCode;
                default: throw new InvalidOperationException("Unknown parsed command.");
            }
        }
        catch (OperationCanceledException) { return Error(130, "Indexing cancelled; last committed snapshot preserved."); }
        catch (GraphException ex) { return Error(ex.ExitCode, ex.Message); }
        catch (SqliteException ex) { return Error(6, ex.Message); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Error(6, ex.Message); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception) { return Error(4, $"Installed dotnet SDK unavailable: {ex.Message}"); }
        catch (Exception ex) { return Error(5, ex.Message); }
        finally { Console.CancelKeyPress -= cancel; }
    }

    private static Command Parse(string[] args)
    {
        if (args is ["--help"] or ["-h"]) return new("help", null, "", false);
        if (args.Length == 0) throw new GraphException(2, "Missing command. Use --help.");
        var tokens = new List<string>();
        var database = Path.Combine(Environment.CurrentDirectory, ".uographs", "graph.db");
        var trust = false;
        var full = false;
        var checkFreshness = false;
        var requireFresh = false;
        var options = new IndexOptions();
        TraversalDirection? direction = null;
        RelationshipKind[]? edgeTypes = null;
        int? depth = null, limit = null, maxEdges = null, evidenceLimit = null;
        var includeInferred = false;
        int? maxNodes = null, maxChars = null;
        var includeSource = false;
        string? symbol = null;
        var format = "json";
        var details = "summary";
        var view = "standard";
        var calls = "all";
        var profile = "graph";
        string? output = null;
        string? after = null, snapshot = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--") { tokens.AddRange(args[(i + 1)..]); break; }
            if (!arg.StartsWith('-')) { tokens.Add(arg); continue; }
            if (!seen.Add(arg)) throw new GraphException(2, $"Duplicate option: {arg}");
            switch (arg)
            {
                case "--view": view = Value(); break;
                case "--calls": calls = Value(); break;
                case "--profile": profile = Value(); break;
                case "--output": output = Value(); break;
                case "--after": after = Value(); break;
                case "--snapshot": snapshot = Value(); break;
                case "--details": details = Value(); break;
                case "--full": full = true; break;
                case "--incremental": break;
                case "--check-freshness": checkFreshness = true; break;
                case "--require-fresh": requireFresh = true; break;
                case "--max-nodes": maxNodes = Number(); break;
                case "--max-chars": maxChars = Number(); break;
                case "--include-source": includeSource = true; break;
                case "--symbol": symbol = Value(); break;
                case "--direction":
                    direction = Value() switch
                    {
                        "outgoing" => TraversalDirection.Outgoing, "incoming" => TraversalDirection.Incoming,
                        "both" => TraversalDirection.Both, _ => throw new GraphException(2, "--direction must be outgoing, incoming or both.")
                    }; break;
                case "--edge-types":
                    edgeTypes = Value().Split(',').Select(v => Enum.GetNames<RelationshipKind>().Contains(v) && Enum.TryParse<RelationshipKind>(v, out var kind)
                        ? kind : throw new GraphException(2, "--edge-types must contain comma-separated relationship names, e.g. CALLS,USES_TYPE.")).ToArray(); break;
                case "--depth": depth = Number(); break;
                case "--limit": limit = Number(); break;
                case "--max-edges": maxEdges = Number(); break;
                case "--evidence-limit": evidenceLimit = Number(); break;
                case "--include-inferred": includeInferred = true; break;
                case "--trust-project": trust = true; break;
                case "--configuration": options = options with { Configuration = SafeValue() }; break;
                case "--framework": options = options with { Framework = SafeValue() }; break;
                case "--root": options = options with { Root = Value() }; break;
                case "--allow-generators": options = options with { AllowGenerators = true }; break;
                case "--generated":
                    var generated = Value();
                    if (generated is not ("include" or "exclude")) throw new GraphException(2, "--generated must be include or exclude.");
                    options = options with { IncludeGenerated = generated == "include" }; break;
                case "--db": database = Value(); break;
                case "--format":
                    format = Value();
                    break;
                default: throw new GraphException(2, $"Unknown option: {arg}");
            }
            int Number() => int.TryParse(Value(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number)
                ? number : throw new GraphException(2, $"{arg} requires an integer.");
            string SafeValue()
            {
                var value = Value();
                if (!System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-zA-Z0-9_.-]+$"))
                    throw new GraphException(2, $"Invalid value for {arg}.");
                return value;
            }
            string Value()
            {
                if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith('-'))
                    throw new GraphException(2, $"Missing value for {arg}.");
                return args[i];
            }
        }
        var name = tokens.ToArray() switch
        {
            ["doctor"] => "doctor",
            ["status"] => "status",
            ["index", _] => "index",
            ["context", var focus] when !string.IsNullOrWhiteSpace(focus) => "context",
            ["export"] => "export",
            ["query", var query, var text] when query is "find" or "symbol" or "callers" or "callees" or "dependencies" or "impact" && !string.IsNullOrWhiteSpace(text) => query,
            ["query", "path", var from, var to] when !string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to) => "path",
            _ => throw new GraphException(2, "Invalid command or arguments. Use --help.")
        };
        if (seen.Contains("--full") && seen.Contains("--incremental") || name != "index" && seen.Any(s => s is "--full" or "--incremental"))
            throw new GraphException(2, "Index mode must be exactly one of --full or --incremental (default). Modes are only valid for index.");
        if ((checkFreshness || requireFresh) && name is "index" or "doctor")
            throw new GraphException(2, "Freshness options require status, query, context or export.");
        var isQuery = tokens[0] == "query";
        var isDelivery = name is "context" or "export";
        if (seen.Any(s => s is "--profile" or "--output") && name != "export")
            throw new GraphException(2, "--profile and --output require export.");
        if (profile is not ("graph" or "backend"))
            throw new GraphException(2, "--profile must be graph or backend.");
        var backend = name == "export" && profile == "backend";
        if (backend && symbol is null)
            throw new GraphException(2, "Backend export requires --symbol.");
        if (format != "json" && !(name == "export" && format is "ndjson" or "mermaid" or "dot"))
            throw new GraphException(2, "Format must be json (export also supports ndjson, mermaid, dot).");
        if (seen.Contains("--symbol") && name != "export" || includeSource && name != "context" ||
            maxNodes is not null && !isDelivery)
            throw new GraphException(2, "Delivery option is not valid for this command.");
        var isReadOutput = isQuery || name == "status";
        if (seen.Contains("--details") && !isReadOutput || maxChars is not null && !isReadOutput && name != "context")
            throw new GraphException(2, "--details requires query/status; --max-chars requires query/status/context.");
        if (seen.Contains("--view") && !isQuery || seen.Any(s => s is "--after" or "--snapshot") && (!isQuery || view != "compact") ||
            seen.Contains("--calls") && !backend && (!isQuery || view != "compact"))
            throw new GraphException(2, "--view requires query; --after/--snapshot require compact query; --calls requires compact query or backend export.");
        ReadOutputOptions? readOutput = isReadOutput ? new(details, maxChars, view) : null;
        readOutput?.Validate();
        if (name == "export" && depth is not null && symbol is null)
            throw new GraphException(2, "Export --depth requires --symbol; full export has no traversal root.");
        if (isDelivery && (direction is not null || edgeTypes is not null || includeInferred))
            throw new GraphException(2, "Context/export direction, confidence and edge types are determined by the profile.");
        if (trust && name != "index" || seen.Contains("--format") && !isQuery && !isDelivery || seen.Contains("--db") && name == "doctor")
            throw new GraphException(2, "Option is not valid for this command. Use --help.");
        if (name != "index" && seen.Any(s => s is "--configuration" or "--framework" or "--root" or "--allow-generators" or "--generated"))
            throw new GraphException(2, "Analysis options are only valid for index.");
        var traversalOptions = seen.Any(s => s is "--direction" or "--edge-types" or "--depth" or "--include-inferred" or "--max-edges" or "--evidence-limit");
        if ((!isQuery && !isDelivery && (traversalOptions || limit is not null)) || (name is "find" or "symbol" && traversalOptions))
            throw new GraphException(2, "Traversal options require a traversal query; --limit requires a query.");
        var queryOptions = QueryOptions.Defaults(name);
        queryOptions = queryOptions with { Direction = direction ?? queryOptions.Direction, EdgeTypes = edgeTypes ?? queryOptions.EdgeTypes,
            Depth = depth ?? queryOptions.Depth, Limit = limit ?? queryOptions.Limit, IncludeInferred = includeInferred,
            MaxEdges = maxEdges ?? queryOptions.MaxEdges, EvidenceLimit = evidenceLimit ?? queryOptions.EvidenceLimit };
        if (isQuery && (view == "compact" && name is "callees" or "callers" or "dependencies" && queryOptions.Depth == 1 ||
            seen.Any(s => s is "--calls" or "--after" or "--snapshot")))
            queryOptions = queryOptions with { Selection = new(calls, after, snapshot) };
        if (isQuery) queryOptions.Validate(name);
        DeliveryOptions? delivery = null;
        if (isDelivery)
        {
            var small = name == "context" || backend;
            delivery = new(maxNodes ?? (small ? 40 : 1000), limit ?? (small ? 80 : 5000),
                depth ?? (backend ? 4 : 1), maxEdges ?? (small ? 1000 : 10000), evidenceLimit ?? (small ? 2 : 20),
                maxChars ?? 12000, includeSource, profile, backend ? (seen.Contains("--calls") ? calls : "exclude-properties") : "all");
            delivery.Validate();
        }
        return new(name, name == "context" ? tokens[1] : name == "export" ? symbol : isQuery ? tokens[2] : name == "index" ? tokens[1] : null, database, trust, options,
            name == "path" ? tokens[3] : null, queryOptions, delivery, format, full, checkFreshness, requireFresh, readOutput, output);
    }

    private static void WriteExport(string path, string text)
    {
        var destination = Path.GetFullPath(path);
        if (File.Exists(destination) || Directory.Exists(destination) || new FileInfo(destination).LinkTarget is not null)
            throw new GraphException(6, "Export destination already exists (files and symlinks are never overwritten).");
        // Stage beside the destination, then publish without replacement. A failed write never
        // leaves a partial destination; Move also rejects a destination created after our check.
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".uographs-export-" + Guid.NewGuid().ToString("N"));
        var created = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
                writer.Write(text);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: false);
        }
        finally { if (created) File.Delete(temporary); }
    }

    private static void Write(object value) => Console.Out.WriteLine(JsonSerializer.Serialize(value, GraphJson.Options));
    private static int Error(int code, string message)
    {
        Console.Error.WriteLine($"uographs: {message}");
        return code;
    }
    private sealed record Command(string Name, string? Value, string Database, bool Trust, IndexOptions? Options = null, string? Target = null, QueryOptions? QueryOptions = null, DeliveryOptions? Delivery = null, string Format = "json", bool Full = false, bool CheckFreshness = false, bool RequireFresh = false, ReadOutputOptions? ReadOutput = null, string? Output = null);
}
