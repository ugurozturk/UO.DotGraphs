using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace UO.Graphs.Core;

public sealed record FingerprintDirectory(string Path, bool SourceTree, bool NamesOnly = false);
public sealed record FingerprintFile(string Path, string Hash);
public sealed record IndexFingerprint(string Input, IndexOptions Options, FingerprintDirectory[] Directories,
    string[] ExplicitFiles, string[] Exclusions, FingerprintFile[] Files, string EvaluationHash,
    string EnvironmentHash, bool Complete, string[] Limitations);
public sealed record DependencyFingerprintSummary(bool Complete, string Granularity, string[] Limitations);
public sealed record FreshnessCheck(string State, string Method, long FilesHashed, long BytesHashed,
    long ElapsedMilliseconds, string[] Reasons, bool CountsComplete = true);

// Content hashes only. No timestamp cache, project evaluation, subprocess or download in reads.
public static class FingerprintScanner
{
    public static (FingerprintFile[] Files, long Bytes, long FilesHashed) Scan(FingerprintDirectory[] directories,
        string[] explicitFiles, string[] exclusions, CancellationToken cancellationToken = default, Action<long, long>? progress = null)
    {
        var paths = new SortedSet<string>(explicitFiles.Select(Path.GetFullPath), StringComparer.Ordinal);
        var memberships = new List<FingerprintFile>();
        foreach (var directory in directories)
            if (directory.NamesOnly)
                memberships.Add(new(directory.Path + Path.DirectorySeparatorChar, Directory.Exists(directory.Path)
                    ? GraphIdentity.Hash(string.Join("\n", Directory.EnumerateFileSystemEntries(directory.Path).Order(StringComparer.Ordinal))) : "missing"));
            else Walk(directory.Path, directory.SourceTree);
        long bytes = 0, hashedFiles = 0;
        var result = new List<FingerprintFile>();
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Excluded(path)) continue;
            FileAttributes attributes;
            try { attributes = File.GetAttributes(path); }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            { result.Add(new(path, "missing")); continue; }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Fingerprint cannot follow a symlink: {path}");
            if ((attributes & FileAttributes.Directory) != 0) { result.Add(new(path, "directory")); continue; }
            using var stream = File.OpenRead(path);
            result.Add(new(path, Convert.ToHexStringLower(SHA256.HashData(stream))));
            bytes += stream.Position;
            hashedFiles++;
            progress?.Invoke(hashedFiles, bytes);
        }
        return (result.Concat(memberships).OrderBy(f => f.Path, StringComparer.Ordinal).ToArray(), bytes, hashedFiles);

        bool Excluded(string path) => exclusions.Any(e => path == e || path == e + "-wal" || path == e + "-shm" || path == e + "-journal" || path == e + ".writer.lock");
        void Walk(string path, bool sourceTree)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(path)) { paths.Add(path); return; }
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Fingerprint cannot follow a symlink directory: {path}");
            foreach (var file in Directory.EnumerateFiles(path)) if (!Excluded(file)) paths.Add(file);
            foreach (var child in Directory.EnumerateDirectories(path))
            {
                var name = Path.GetFileName(child);
                if (sourceTree && name is "bin" or "obj" or ".git" or ".uographs") continue;
                Walk(child, sourceTree);
            }
            if (sourceTree && Directory.Exists(Path.Combine(path, "obj"))) WalkOutputs(Path.Combine(path, "obj"));
        }
        void WalkOutputs(string path)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Fingerprint cannot follow a symlink directory: {path}");
            foreach (var file in Directory.EnumerateFiles(path))
                if (Path.GetExtension(file) is ".json" or ".props" or ".targets" or ".cs" or ".editorconfig") paths.Add(file);
            foreach (var child in Directory.EnumerateDirectories(path)) WalkOutputs(child);
        }
    }

    // Never persist environment values (which may contain credentials).
    public static string EnvironmentDigest() => GraphIdentity.Hash(JsonSerializer.Serialize(
        Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .OrderBy(e => (string)e.Key, StringComparer.Ordinal)
            .Select(e => new[] { (string)e.Key, (string?)e.Value }).ToArray()) +
        JsonSerializer.Serialize(new { runtime = Environment.Version.ToString(), Environment.CurrentDirectory,
            AppContext.BaseDirectory, os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            culture = System.Globalization.CultureInfo.CurrentCulture.Name, uiCulture = System.Globalization.CultureInfo.CurrentUICulture.Name }));

    public static FreshnessCheck Check(IndexFingerprint? fingerprint, bool check = true)
    {
        if (!check) return new("notChecked", "none", 0, 0, 0, ["No filesystem or environment check requested; use --check-freshness (content hashing cost)."]);
        if (fingerprint is null) return new("unknown", "contentSha256", 0, 0, 0, ["Snapshot has no dependency fingerprint; explicitly re-index."]);
        var watch = Stopwatch.StartNew();
        long filesRead = 0, bytesRead = 0;
        try
        {
            var scan = Scan(fingerprint.Directories, fingerprint.ExplicitFiles, fingerprint.Exclusions, progress: (files, bytes) => { filesRead = files; bytesRead = bytes; });
            var changed = !scan.Files.SequenceEqual(fingerprint.Files) || EnvironmentDigest() != fingerprint.EnvironmentHash;
            return new(changed ? "stale" : fingerprint.Complete ? "fresh" : "unknown", "contentSha256", scan.FilesHashed, scan.Bytes,
                watch.ElapsedMilliseconds, changed ? ["Input content, membership or process environment changed."] : fingerprint.Limitations);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return new("unknown", "contentSha256", filesRead, bytesRead, watch.ElapsedMilliseconds, [ex.Message], CountsComplete: false); }
    }

    public static AnalysisSnapshot Apply(AnalysisSnapshot analysis, IndexFingerprint? fingerprint, bool check, bool requireFresh)
    {
        var result = Check(fingerprint, check || requireFresh);
        if (requireFresh && result.State != "fresh") throw new GraphException(9, $"--require-fresh rejected {result.State} snapshot: {string.Join(" ", result.Reasons)}");
        return analysis with { Freshness = result.State, FreshnessCheck = result };
    }
}
