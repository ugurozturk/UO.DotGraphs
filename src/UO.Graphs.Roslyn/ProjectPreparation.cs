using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UO.Graphs.Core;

namespace UO.Graphs.Roslyn;

internal sealed record PreparedProject(string Path, string Framework, string Sdk, string MSBuildPath,
    string MSBuildVersion, string[] Frameworks, string[] References, string[] SourceFiles, JsonElement Evaluation);
internal sealed record Preparation(Dictionary<string, PreparedProject> Projects, List<string> Diagnostics, string Root);

public static class ProjectPreparation
{
    public static async Task<string> GetSdkVersionAsync(string directory) =>
        (await RunDotnetAsync(directory, "--version")).Trim();

    internal static async Task<Preparation> CheckAsync(string input, IndexOptions options, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string?>? environment = null)
    {
        var pending = new Queue<(string Path, bool Required)>(GetProjects(input).Select(p => (p, true)));
        var result = new Dictionary<string, PreparedProject>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var diagnostics = new List<string>();
        while (pending.TryDequeue(out var item))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(item.Path);
            if (!visited.Add(fullPath)) continue;
            if (!File.Exists(fullPath) || !string.Equals(Path.GetExtension(fullPath), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add($"preparation: Missing or unsupported C# project: {fullPath}");
                continue;
            }
            var xml = XDocument.Load(fullPath);
            if (xml.Root?.Attribute("Sdk") is null && !xml.Descendants().Any(e => e.Name.LocalName == "Sdk" || e.Attribute("Sdk") is not null))
            {
                diagnostics.Add($"preparation: Unsupported non-SDK-style project: {fullPath}");
                continue;
            }
            var directory = Path.GetDirectoryName(fullPath)!;
            try
            {
                var sdk = (await RunDotnetAsync(directory, environment, cancellationToken, "--version")).Trim();
                var outer = await EvaluateAsync(fullPath, options, null, cancellationToken, environment);
                var frameworks = Split(outer.GetProperty("Properties").GetProperty("TargetFrameworks").GetString());
                var single = outer.GetProperty("Properties").GetProperty("TargetFramework").GetString()!;
                if (frameworks.Length == 0) frameworks = Split(single);
                if (frameworks.Length == 0) throw new GraphException(4, $"Unsupported project without TargetFramework: {fullPath}");
                if (frameworks.Length > 1 && options.Framework is null)
                    throw new GraphException(4, $"Multi-target project requires --framework <TFM>: {fullPath}; available: {string.Join(", ", frameworks)}. Run each context separately.");
                var framework = options.Framework is not null && frameworks.Contains(options.Framework) ? options.Framework : frameworks[0];
                if (options.Framework is not null && frameworks.Length > 1 && !frameworks.Contains(options.Framework))
                    throw new GraphException(4, $"Requested framework {options.Framework} not supported by {fullPath}: {string.Join(", ", frameworks)}");
                var evaluated = await EvaluateAsync(fullPath, options, framework, cancellationToken, environment);
                var properties = evaluated.GetProperty("Properties");
                var assets = properties.GetProperty("ProjectAssetsFile").GetString();
                if (string.IsNullOrEmpty(assets) || !File.Exists(Path.GetFullPath(assets, directory)))
                    throw new GraphException(4, $"NuGet assets missing for {fullPath}. Prepare explicitly with dotnet restore in your approved environment; uographs never restores.");
                var references = evaluated.GetProperty("Items").GetProperty("ProjectReference").EnumerateArray()
                    .Where(r => !r.TryGetProperty("OutputItemType", out var kind) || kind.GetString() != "Analyzer")
                    .Select(r => r.GetProperty("FullPath").GetString()!).ToArray();
                result.Add(fullPath, new(fullPath, framework, sdk, properties.GetProperty("MSBuildToolsPath").GetString()!,
                    properties.GetProperty("MSBuildVersion").GetString()!, frameworks, references,
                    evaluated.GetProperty("Items").GetProperty("Compile").EnumerateArray()
                        .Select(f => f.GetProperty("FullPath").GetString()!).ToArray(), evaluated));
                foreach (var reference in references) pending.Enqueue((reference, false));
            }
            catch (GraphException ex) when (!item.Required)
            {
                diagnostics.Add($"preparation: {fullPath}: {ex.Message}");
            }
        }
        if (result.Count == 0) throw new GraphException(4, "No supported, prepared C# projects found.\n" + string.Join("\n", diagnostics));
        if (result.Values.Select(p => p.MSBuildPath).Distinct().Count() != 1)
            throw new GraphException(4, "Multiple SDK/MSBuild installations in one analysis are unsupported; index separately.");
        if (options.Framework is not null && !result.Values.Any(p => p.Framework == options.Framework))
            throw new GraphException(4, $"No project supports requested framework {options.Framework}.");
        var root = options.Root is null ? Path.GetDirectoryName(input)! : Path.GetFullPath(options.Root);
        if (options.Root is null)
            while (result.Keys.Any(p => IsOutside(root, p)))
                root = Path.GetDirectoryName(root) ?? throw new GraphException(4, "Specify a common --root for all projects.");
        if (result.Keys.Append(input).Any(p => IsOutside(root, p)))
            throw new GraphException(2, "--root must contain the input and every prepared project.");
        return new(result, diagnostics, root);
    }

    private static bool IsOutside(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string[] Split(string? value) => (value ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static async Task<JsonElement> EvaluateAsync(string path, IndexOptions options, string? framework, CancellationToken cancellationToken, IReadOnlyDictionary<string, string?>? environment)
    {
        var args = new List<string> { "msbuild", path, "-nologo", "-verbosity:quiet", $"-property:Configuration={options.Configuration}",
            "-property:NuGetInteractive=false", "-property:RestoreDuringBuild=false",
            "-getProperty:ProjectAssetsFile,TargetFramework,TargetFrameworks,MSBuildToolsPath,MSBuildVersion,DefineConstants,LangVersion,Nullable,Optimize,AllowUnsafeBlocks,CheckForOverflowUnderflow,IntermediateOutputPath,MSBuildUserExtensionsPath,MSBuildExtensionsPath,MSBuildExtensionsPath32,MSBuildExtensionsPath64,MSBuildSDKsPath,RoslynTargetsPath,CustomBeforeMicrosoftCommonProps,CustomAfterMicrosoftCommonProps,CustomBeforeMicrosoftCommonTargets,CustomAfterMicrosoftCommonTargets,CustomBeforeMicrosoftCSharpTargets,CustomAfterMicrosoftCSharpTargets,CustomBeforeDirectoryBuildProps,CustomAfterDirectoryBuildProps,CustomBeforeDirectoryBuildTargets,CustomAfterDirectoryBuildTargets", "-getItem:ProjectReference,Compile,AdditionalFiles,Analyzer,EditorConfigFiles" };
        if (framework is not null) args.Add($"-property:TargetFramework={framework}");
        using var json = JsonDocument.Parse(await RunDotnetAsync(Path.GetDirectoryName(path)!, environment, cancellationToken, args.ToArray()));
        return json.RootElement.Clone();
    }

    private static IEnumerable<string> GetProjects(string input)
    {
        var directory = Path.GetDirectoryName(input)!;
        switch (Path.GetExtension(input).ToLowerInvariant())
        {
            case ".csproj": return [input];
            case ".slnx":
                return XDocument.Load(input).Descendants("Project")
                    .Select(p => Path.GetFullPath(p.Attribute("Path")!.Value.Replace('\\', Path.DirectorySeparatorChar), directory)).ToArray();
            case ".sln":
                return File.ReadLines(input)
                    .Select(line => Regex.Match(line, "^Project\\(\"(?<type>[^\"]+)\"\\)\\s*=\\s*\"[^\"]*\",\\s*\"(?<path>[^\"]+)\""))
                    .Where(m => m.Success && !m.Groups["type"].Value.Equals("{66A26720-8FB5-11D2-AA7E-00C04F688DDE}", StringComparison.OrdinalIgnoreCase)
                        && !m.Groups["type"].Value.Equals("{2150E333-8FDC-42A3-9474-1A3956D46DE8}", StringComparison.OrdinalIgnoreCase))
                    .Select(m => Path.GetFullPath(m.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar), directory)).ToArray();
            default: throw new GraphException(2, "Input must be a .csproj, .sln or .slnx file.");
        }
    }

    internal static Task<string> RunDotnetAsync(string directory, params string[] arguments) => RunDotnetAsync(directory, null, CancellationToken.None, arguments);

    internal static async Task<string> RunDotnetAsync(string directory, IReadOnlyDictionary<string, string?>? environment, CancellationToken cancellationToken, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        if (environment is not null)
        {
            start.Environment.Clear();
            foreach (var pair in environment) start.Environment[pair.Key] = pair.Value;
        }
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new GraphException(4, "Cannot start installed dotnet SDK.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        // Bound both streams, including drain after the parent exits. A descendant may
        // inherit a pipe; the deadline must cover that case as well as WaitForExit.
        var stdout = ReadBoundedAsync(process.StandardOutput, 32 * 1024 * 1024, timeout.Token);
        var stderr = ReadBoundedAsync(process.StandardError, 1024 * 1024, timeout.Token);
        var exited = process.WaitForExitAsync(timeout.Token);
        try
        {
            var pending = new List<Task> { stdout, stderr, exited };
            while (pending.Count > 0)
            {
                var completed = await Task.WhenAny(pending);
                await completed; // Fail promptly if a stream exceeds its cap.
                pending.Remove(completed);
            }
            var output = await stdout;
            var error = await stderr;
            if (process.ExitCode != 0)
                throw new GraphException(4, $"SDK/MSBuild prerequisite failed. Install the SDK selected by global.json and prepare assets explicitly.\n{output}\n{error}");
            return output;
        }
        catch (OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new GraphException(4, "SDK/MSBuild preparation or output drain timed out.");
        }
        finally
        {
            timeout.Cancel();
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* Concurrent exit. */ }
            try { await Task.WhenAll(stdout, stderr, exited); }
            catch (Exception ex) when (ex is OperationCanceledException or GraphException or IOException) { /* Original error wins. */ }
        }
    }

    internal static async Task<string> ReadBoundedAsync(TextReader reader, int maxChars, CancellationToken token)
    {
        var result = new System.Text.StringBuilder();
        var buffer = new char[8192];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
        {
            if (count > maxChars - result.Length)
                throw new GraphException(4, $"SDK/MSBuild output exceeds the {maxChars} character safety limit; no snapshot published.");
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }
}
