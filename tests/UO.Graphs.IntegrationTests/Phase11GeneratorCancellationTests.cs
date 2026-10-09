using System.Diagnostics;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UO.Graphs.Core;
using UO.Graphs.Roslyn;
using Xunit.Abstractions;

namespace UO.Graphs.IntegrationTests;

// Other collections mutate process-wide MSBuild environment variables. This test
// runs the indexer in-process and must not inherit those temporary settings.
[CollectionDefinition("Generator cancellation", DisableParallelization = true)]
public sealed class GeneratorCancellationCollection;

[Collection("Generator cancellation")]
public sealed class Phase11GeneratorCancellationTests(ITestOutputHelper output) : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "uographs-generator-cancel-" + Guid.NewGuid().ToString("N"));
    private bool preserveDirectory;

    [Fact]
    public async Task AllowedCooperativeGeneratorReceivesProductCancellationToken()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "UO.Graphs.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        Directory.CreateDirectory(directory);
        // The fixture lives outside the checkout, so SDK lookup would otherwise
        // select whichever SDK happens to be newest on the runner.
        File.Copy(Path.Combine(root.FullName, "global.json"), Path.Combine(directory, "global.json"));
        var preparedAssets = Path.Combine(root.FullName, "samples/HelloGraph/HelloGraph.Domain/obj/project.assets.json");
        Assert.True(File.Exists(preparedAssets), "Prepare existing HelloGraph assets explicitly before running this test; no restore is performed.");
        var generatorPath = Path.Combine(directory, "CooperativeGenerator.dll");
        var marker = Path.Combine(directory, "generator-started");
        var stopped = Path.Combine(directory, "generator-cancelled");
        var source = $$"""
            using System;
            using System.IO;
            using Microsoft.CodeAnalysis;
            [Generator]
            public sealed class CooperativeGenerator : ISourceGenerator
            {
                public void Initialize(GeneratorInitializationContext context) { }
                public void Execute(GeneratorExecutionContext context)
                {
                    File.WriteAllText({{JsonSerializer.Serialize(marker + ".tmp")}}, context.CancellationToken.CanBeCanceled.ToString());
                    File.Move({{JsonSerializer.Serialize(marker + ".tmp")}}, {{JsonSerializer.Serialize(marker)}});
                    context.CancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(15));
                    if (context.CancellationToken.IsCancellationRequested)
                        File.WriteAllText({{JsonSerializer.Serialize(stopped)}}, "observed");
                    context.CancellationToken.ThrowIfCancellationRequested();
                }
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(ISourceGenerator).Assembly.Location).Distinct(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("CooperativeGenerator", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emitted = compilation.Emit(generatorPath);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        var project = Path.Combine(directory, "GeneratorConsumer.csproj");
        File.WriteAllText(project, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup>
              <ItemGroup><Analyzer Include="CooperativeGenerator.dll" /></ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, "Code.cs"), "public class Consumer {}");
        Directory.CreateDirectory(Path.Combine(directory, "obj"));
        File.Copy(preparedAssets, Path.Combine(directory, "obj/project.assets.json"));
        using var cancellation = new CancellationTokenSource();
        using var progress = new IndexProgress();
        var options = new IndexOptions(AllowGenerators: true);
        // The synchronous generator waits for this test to cancel it. Keep the
        // product operation independent of xUnit's synchronization context.
        var operation = Task.Run(() => new IncrementalIndexer().RunAsync(project, true, progress, options,
            Path.Combine(directory, ".uographs/graph.db"), full: true, cancellationToken: cancellation.Token));
        try
        {
            await UntilAsync(() => File.Exists(marker), operation, progress);
            Assert.Equal("True", await File.ReadAllTextAsync(marker));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal("observed", await File.ReadAllTextAsync(stopped));
        }
        finally
        {
            cancellation.Cancel();
            try { await operation.WaitAsync(TimeSpan.FromSeconds(20)); }
            catch (OperationCanceledException) { /* Preserve the original assertion. */ }
            catch (TimeoutException ex)
            {
                preserveDirectory = true;
                throw new InvalidOperationException($"Index cancellation cleanup is still running; fixture preserved at {directory}.", ex);
            }
            finally { output.WriteLine(progress.ToString()); }
        }
    }

    private static async Task UntilAsync(Func<bool> condition, Task operation, IndexProgress progress)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (operation.IsCompleted)
            {
                await operation;
                Assert.Fail($"Index completed without starting the cooperative generator.\n{progress}");
            }
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(90),
                $"The product did not start the cooperative generator before the acceptance deadline. Operation: {operation.Status}.\n{progress}");
            await Task.Delay(20);
        }
    }

    private sealed class IndexProgress : TextWriter
    {
        private readonly Stopwatch timer = Stopwatch.StartNew();
        private readonly ConcurrentQueue<string> lines = new();
        public override Encoding Encoding => Encoding.UTF8;
        public override Task WriteLineAsync(string? value)
        {
            lines.Enqueue($"[{timer.Elapsed.TotalSeconds:F3}s] {value}");
            return Task.CompletedTask;
        }
        public override string ToString() => string.Join(Environment.NewLine, lines);
    }

    public void Dispose() { if (!preserveDirectory && Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}
