using UO.Graphs.Core;

namespace UO.Graphs.UnitTests;

public sealed class FingerprintTests
{
    [Fact]
    public void HashCostCountsBytesAndFilesAndReportsPartialReadFailure()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uographs-fingerprint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "a.cs");
            File.WriteAllBytes(source, [1, 2, 3, 4]);
            FingerprintDirectory[] directories = [new(directory, true)];
            string[] explicitFiles = [Path.Combine(directory, "missing.props")];
            var scan = FingerprintScanner.Scan(directories, explicitFiles, []);
            Assert.Equal(1, scan.FilesHashed);
            Assert.Equal(4, scan.Bytes);
            Assert.Equal(2, scan.Files.Length);
            var manifest = new IndexFingerprint(source, new(), directories, explicitFiles, [], scan.Files, "evaluation",
                FingerprintScanner.EnvironmentDigest(), true, []);
            var fresh = FingerprintScanner.Check(manifest);
            Assert.Equal("fresh", fresh.State);
            Assert.True(fresh.CountsComplete);
            File.CreateSymbolicLink(Path.Combine(directory, "z.cs"), source);
            var unknown = FingerprintScanner.Check(manifest);
            Assert.Equal("unknown", unknown.State);
            Assert.False(unknown.CountsComplete);
            Assert.Equal(1, unknown.FilesHashed);
            Assert.Equal(4, unknown.BytesHashed);
            Assert.Equal(9, Assert.Throws<GraphException>(() => FingerprintScanner.Apply(
                new("id", DateTimeOffset.UtcNow, source, "0.6", "5", "partial", "notChecked", [], [], []), manifest, false, true)).ExitCode);
        }
        finally { Directory.Delete(directory, true); }
    }
}
