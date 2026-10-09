using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using UO.Graphs.Core;

namespace UO.Graphs.Roslyn;

public sealed record IndexRun(GraphSnapshot? Graph, IndexFingerprint Fingerprint, string Outcome,
    int ProjectsAnalyzed, long FingerprintMilliseconds, long AnalysisMilliseconds, long FilesHashed, long BytesHashed);

public sealed class IncrementalIndexer
{
    public async Task<IndexRun> RunAsync(string input, bool trustProject, TextWriter progress, IndexOptions options,
        string database, IndexFingerprint? previous = null, bool full = false, CancellationToken cancellationToken = default)
    {
        if (!trustProject) throw new GraphException(3, "Indexing requires --trust-project before MSBuild evaluation, including incremental validation.");
        input = Path.GetFullPath(input);
        if (!File.Exists(input)) throw new GraphException(2, $"Input does not exist: {input}");
        cancellationToken.ThrowIfCancellationRequested();
        var environment = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string?)e.Value, StringComparer.Ordinal);
        var timer = Stopwatch.StartNew();
        await progress.WriteLineAsync("Checking installed SDK, evaluated inputs and content fingerprints (no restore)...");
        var preparation = await ProjectPreparation.CheckAsync(input, options, cancellationToken, environment);
        var (fingerprint, fingerprintBytes, fingerprintFiles) = await CaptureAsync(input, options, database, preparation, cancellationToken, environment);
        var fingerprintMs = timer.ElapsedMilliseconds;
        if (!full && previous is { Complete: true } && fingerprint.Complete && Same(previous, fingerprint))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(null, fingerprint, "noOp", 0, fingerprintMs, 0, fingerprintFiles, fingerprintBytes);
        }
        if (previous is not null) await progress.WriteLineAsync($"Fingerprint comparison: input={previous.Input == fingerprint.Input}, options={previous.Options == fingerprint.Options}, evaluation={previous.EvaluationHash == fingerprint.EvaluationHash}, environment={previous.EnvironmentHash == fingerprint.EnvironmentHash}, files={previous.Files.SequenceEqual(fingerprint.Files)}.");
        await progress.WriteLineAsync("Invalidation: entire selected project closure (including dependents and all inference records). " +
            (fingerprint.Complete ? "Content/context changed, full requested, or no reusable snapshot." : string.Join(" ", fingerprint.Limitations)));
        cancellationToken.ThrowIfCancellationRequested();
        timer.Restart();
        var graph = await ProjectIndexer.IndexPreparedAsync(input, preparation, options, progress, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // A workspace can write generated obj files. Only declared inputs are scanned. A changed
        // input prevents publication; no graph is labelled as belonging to a later source state.
        var verifiedPreparation = await ProjectPreparation.CheckAsync(input, options, cancellationToken, environment);
        var (verified, verifiedBytes, verifiedFiles) = await CaptureAsync(input, options, database, verifiedPreparation, cancellationToken, environment);
        if (verified.EvaluationHash != fingerprint.EvaluationHash)
            throw new GraphException(5, "Project evaluation changed during analysis; previous snapshot preserved. Retry with stable inputs. Changed properties: " +
                string.Join(", ", preparation.Projects.Values.SelectMany(p => verifiedPreparation.Projects.TryGetValue(p.Path, out var current)
                    ? p.Evaluation.GetProperty("Properties").EnumerateObject().Where(property =>
                        current.Evaluation.GetProperty("Properties").GetProperty(property.Name).GetRawText() != property.Value.GetRawText()).Select(property => property.Name)
                    : ["projectMembership"]).Distinct()));
        var after = (Files: verified.Files, Bytes: verifiedBytes);
        var changedFiles = after.Files.Except(fingerprint.Files).Concat(fingerprint.Files.Except(after.Files)).Select(f => f.Path).Distinct().ToArray();
        var evaluatedSources = preparation.Projects.Values.SelectMany(p => p.SourceFiles).ToHashSet(StringComparer.Ordinal);
        var unsafeChanges = changedFiles.Where(path => !SdkGeneratedOutput(path) || evaluatedSources.Contains(path)).ToArray();
        if (unsafeChanges.Length > 0 || graph.ObservedSources.Any(pair => !File.Exists(pair.Key) || GraphIdentity.Hash(File.ReadAllText(pair.Key)) != pair.Value))
            throw new GraphException(5, "Inputs changed during analysis; previous snapshot preserved. Retry index with stable inputs. " + string.Join(", ", unsafeChanges.Take(8)));
        // SDK-generated obj source may first appear during workspace load. Verify the exact
        // decoded text Roslyn consumed, then retain raw hashes and membership for future reads.
        fingerprint = fingerprint with { Files = after.Files };
        graph = graph with { Fingerprint = fingerprint, Analysis = graph.Analysis with
        { DependencyFingerprint = new(fingerprint.Complete, "entireSelectedProjectClosure", fingerprint.Limitations) } };
        return new(graph, fingerprint, "rebuiltAllProjects", graph.Analysis.Projects.Length, fingerprintMs,
            timer.ElapsedMilliseconds, fingerprintFiles + verifiedFiles, fingerprintBytes + after.Bytes);
    }

    private static bool SdkGeneratedOutput(string path) => path.Replace('\\', '/').Split('/').Contains("obj") &&
        (path.EndsWith(".GlobalUsings.g.cs", StringComparison.Ordinal) || path.EndsWith(".AssemblyInfo.cs", StringComparison.Ordinal) ||
         path.EndsWith(".AssemblyAttributes.cs", StringComparison.Ordinal) || path.EndsWith(".GeneratedMSBuildEditorConfig.editorconfig", StringComparison.Ordinal));

    private static bool Same(IndexFingerprint a, IndexFingerprint b) =>
        a.Input == b.Input && a.Options == b.Options && a.EvaluationHash == b.EvaluationHash &&
        a.EnvironmentHash == b.EnvironmentHash && a.Files.SequenceEqual(b.Files);

    private static async Task<(IndexFingerprint Fingerprint, long Bytes, long Files)> CaptureAsync(string input, IndexOptions options, string database,
        Preparation preparation, CancellationToken cancellationToken, IReadOnlyDictionary<string, string?> environment)
    {
        var files = new HashSet<string>(StringComparer.Ordinal) { input };
        var dirs = new HashSet<FingerprintDirectory> { new(preparation.Root, true), new(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), false) };
        var limitations = new SortedSet<string>(StringComparer.Ordinal);
        var evaluations = new List<string>();
        if (preparation.Diagnostics.Count > 0) limitations.Add("Incomplete project preparation; reuse disabled, broad invalidation required.");
        if (options.AllowGenerators) limitations.Add("Generator execution can read undeclared inputs; dependency closure unproven, reuse disabled.");
        foreach (var project in preparation.Projects.Values.OrderBy(p => p.Path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (project.Sdk != "10.0.201") limitations.Add("SDK outside the validated base-SDK dependency model; reuse disabled.");
            dirs.Add(new(project.MSBuildPath, false));
            var dotnetRoot = Directory.GetParent(project.MSBuildPath)!.Parent!.FullName;
            dirs.Add(new(Path.Combine(dotnetRoot, "packs"), false));
            dirs.Add(new(Path.Combine(dotnetRoot, "sdk-manifests"), false));
            dirs.Add(new(Path.Combine(dotnetRoot, "sdk"), false, true));
            files.Add(Path.Combine(dotnetRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
            var evaluatedProperties = project.Evaluation.GetProperty("Properties");
            foreach (var property in new[] { "MSBuildUserExtensionsPath", "MSBuildExtensionsPath", "MSBuildExtensionsPath32", "MSBuildExtensionsPath64", "MSBuildSDKsPath", "RoslynTargetsPath" })
                if (evaluatedProperties.TryGetProperty(property, out var value) && !string.IsNullOrWhiteSpace(value.GetString()))
                    dirs.Add(new(Path.GetFullPath(value.GetString()!, Path.GetDirectoryName(project.Path)!), false));
            foreach (var property in evaluatedProperties.EnumerateObject().Where(p => p.Name.StartsWith("Custom", StringComparison.Ordinal)))
                foreach (var value in (property.Value.GetString() ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (value.IndexOfAny(['*', '?']) >= 0) limitations.Add("Custom implicit import pattern cannot be proven complete; reuse disabled.");
                    else files.Add(Path.GetFullPath(value, Path.GetDirectoryName(project.Path)!));
                }
            evaluations.Add(StableEvaluation(project.Evaluation));
            evaluations.Add(project.Sdk);
            var preprocessed = await ProjectPreparation.RunDotnetAsync(Path.GetDirectoryName(project.Path)!, environment, cancellationToken, "msbuild", project.Path,
                "-nologo", "-preprocess", $"-property:Configuration={options.Configuration}", $"-property:TargetFramework={project.Framework}",
                "-property:NuGetInteractive=false", "-property:RestoreDuringBuild=false");
            evaluations.Add(preprocessed);
            // MSBuild's preprocessor includes an absolute filename between separator lines for
            // each imported file. If the format cannot be established, never allow reuse.
            var imports = XDocument.Parse(preprocessed).DescendantNodes().OfType<XComment>()
                .Where(c => c.Value.Contains("================", StringComparison.Ordinal))
                .SelectMany(c => c.Value.Split('\n')).Select(l => l.Trim())
                .Where(l => Path.IsPathFullyQualified(l) && File.Exists(l)).Distinct().ToArray();
            if (imports.Length < 2) limitations.Add("MSBuild import manifest unavailable; reuse disabled.");
            foreach (var import in imports)
            {
                files.Add(import);
                if (!Within(project.MSBuildPath, import) && !Within(Path.Combine(dotnetRoot, "sdk-manifests"), import)) InspectBuildLogic(import);
            }
            foreach (var itemType in new[] { "Compile", "AdditionalFiles", "Analyzer", "EditorConfigFiles" })
                foreach (var item in project.Evaluation.GetProperty("Items").GetProperty(itemType).EnumerateArray())
                    if (item.TryGetProperty("FullPath", out var path)) files.Add(path.GetString()!);
            foreach (var ancestor in Ancestors(Path.GetDirectoryName(project.Path)!))
                foreach (var name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "NuGet.Config", "nuget.config", ".editorconfig", "Directory.Build.rsp" })
                    files.Add(Path.Combine(ancestor, name));
            var assets = Path.GetFullPath(project.Evaluation.GetProperty("Properties").GetProperty("ProjectAssetsFile").GetString()!, Path.GetDirectoryName(project.Path)!);
            files.Add(assets);
            files.Add(Path.Combine(Path.GetDirectoryName(project.Path)!, "packages.lock.json"));
            using var json = JsonDocument.Parse(File.ReadAllText(assets));
            if (json.RootElement.TryGetProperty("libraries", out var libraries))
                foreach (var library in libraries.EnumerateObject())
                    if (library.Value.TryGetProperty("path", out var packagePath))
                        foreach (var folder in json.RootElement.GetProperty("packageFolders").EnumerateObject())
                        {
                            var package = Path.Combine(folder.Name, packagePath.GetString()!);
                            dirs.Add(new(package, false));
                        }
        }
        // Any custom build logic may use environment, globs, tasks or property functions whose
        // actual dependency set MSBuild does not expose. We record known inputs but decline reuse.
        foreach (var path in files.ToArray())
            if (Path.GetExtension(path) is ".csproj" or ".props" or ".targets" && File.Exists(path) &&
                !preparation.Projects.Values.Any(p => Within(p.MSBuildPath, path) || Within(Path.Combine(Directory.GetParent(p.MSBuildPath)!.Parent!.FullName, "sdk-manifests"), path))) InspectBuildLogic(path);
        var exclusions = new[] { Path.GetFullPath(database) };
        var orderedDirs = dirs.Select(d => d with { Path = Path.GetFullPath(d.Path) }).Distinct()
            .Where(d => !dirs.Any(parent => parent != d && !parent.SourceTree && !parent.NamesOnly && Within(parent.Path, d.Path)))
            .OrderBy(d => d.Path, StringComparer.Ordinal).ToArray();
        var orderedFiles = files.Order(StringComparer.Ordinal).ToArray();
        var scan = FingerprintScanner.Scan(orderedDirs, orderedFiles, exclusions, cancellationToken);
        return (new(input, options, orderedDirs, orderedFiles, exclusions, scan.Files,
            GraphIdentity.Hash(JsonSerializer.Serialize(evaluations)), FingerprintScanner.EnvironmentDigest(), limitations.Count == 0, limitations.ToArray()), scan.Bytes, scan.FilesHashed);

        void InspectBuildLogic(string path)
        {
            var document = XDocument.Load(path);
            var sdk = document.Root?.Attribute("Sdk")?.Value;
            var itemTypes = new HashSet<string>(StringComparer.Ordinal)
            { "Compile", "ProjectReference", "PackageReference", "PackageVersion", "Analyzer", "AdditionalFiles", "EditorConfigFiles",
              "GlobalAnalyzerConfigFiles", "Using", "None", "Content", "SourceRoot", "InternalsVisibleTo" };
            if (document.Descendants().Any(e => e.Name.LocalName == "ItemDefinitionGroup" ||
                e.Parent?.Name.LocalName == "ItemGroup" && !itemTypes.Contains(e.Name.LocalName)))
                limitations.Add("Custom item/task input behavior unproven; reuse disabled: " + path);
            var properties = new HashSet<string>(StringComparer.Ordinal)
            {
                "TargetFramework", "TargetFrameworks", "Nullable", "ImplicitUsings", "LangVersion", "TreatWarningsAsErrors",
                "Deterministic", "RestorePackagesWithLockFile", "Version", "PackageLicenseExpression", "DefineConstants",
                "OutputType", "AssemblyName", "RootNamespace", "AllowUnsafeBlocks", "CheckForOverflowUnderflow", "Optimize",
                "ManagePackageVersionsCentrally", "CentralPackageTransitivePinningEnabled", "IsPackable", "IsTestProject",
                "GenerateDocumentationFile", "NoWarn", "WarningsAsErrors", "EnableNETAnalyzers", "AnalysisLevel",
                "RestoreSuccess", "RestoreTool", "ProjectAssetsFile", "NuGetPackageRoot", "NuGetPackageFolders", "NuGetProjectStyle", "NuGetToolVersion"
            };
            if (document.Descendants().Where(e => e.Parent?.Name.LocalName == "PropertyGroup").Any(e => !properties.Contains(e.Name.LocalName)))
                limitations.Add("Custom MSBuild properties can redirect implicit inputs; dependency closure unproven; reuse disabled: " + path);
            if (document.Descendants().Attributes().Any(a => a.Name.LocalName is "Include" or "Update" or "Remove" &&
                a.Value.Replace('\\', '/').Split('/').Any(part => part is "bin" or "obj" or ".git" or ".uographs")))
                limitations.Add("Explicit item patterns in excluded output directories; dependency closure unproven; reuse disabled: " + path);
            if (sdk is not null && sdk != "Microsoft.NET.Sdk") limitations.Add("Non-base SDK dependency behavior unproven; reuse disabled.");
            if (document.Descendants().Any(e => e.Name.LocalName is "Target" or "UsingTask" or "Import" or "Sdk" or "Reference") ||
                document.ToString().Contains("$([", StringComparison.Ordinal) || document.ToString().Contains("Exists(", StringComparison.OrdinalIgnoreCase))
                limitations.Add("Custom imports/targets/property functions or existence conditions: dependency closure unproven; reuse disabled: " + path);
            // Outside-root item globs and linked trees are not safely covered by the source inventory.
            if (document.Descendants().Attributes().Any(a => (a.Value.Contains("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(a.Value) || a.Value.Contains("$(", StringComparison.Ordinal)) && a.Name.LocalName is "Include" or "Update" or "Remove" && a.Parent?.Name.LocalName is not ("ProjectReference" or "SourceRoot")))
                limitations.Add("External item patterns may introduce unseen inputs; reuse disabled.");
        }
    }

    private static string StableEvaluation(JsonElement evaluation)
    {
        var node = JsonNode.Parse(evaluation.GetRawText())!;
        Clean(node);
        return node.ToJsonString();
        static void Clean(JsonNode? value)
        {
            if (value is JsonObject obj)
                foreach (var key in obj.Select(p => p.Key).ToArray())
                    if (key is "ModifiedTime" or "CreatedTime" or "AccessedTime") obj.Remove(key);
                    else Clean(obj[key]);
            else if (value is JsonArray array) foreach (var child in array) Clean(child);
        }
    }

    private static bool Within(string root, string path) => path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    private static IEnumerable<string> Ancestors(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current)) yield return current;
    }
}
