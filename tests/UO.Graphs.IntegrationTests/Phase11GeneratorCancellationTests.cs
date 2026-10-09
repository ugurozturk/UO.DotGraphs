using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UO.Graphs.Core;
using UO.Graphs.Roslyn;

namespace UO.Graphs.IntegrationTests;

public sealed class Phase11GeneratorCancellationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "uographs-generator-cancel-" + Guid.NewGuid().ToString("N"));
    private bool preserveDirectory;

    public Phase11GeneratorCancellationTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task AllowedCooperativeGeneratorReceivesProductCancellationToken()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "UO.Graphs.slnx"))) root = root.Parent;
        Assert.NotNull(root);
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
        var options = new IndexOptions(AllowGenerators: true);
        var operation = new IncrementalIndexer().RunAsync(project, true, TextWriter.Null, options,
            Path.Combine(directory, ".uographs/graph.db"), full: true, cancellationToken: cancellation.Token);
        try
        {
            await UntilAsync(() => File.Exists(marker), operation);
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
        }
    }

    private static async Task UntilAsync(Func<bool> condition, Task operation)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (operation.IsCompleted)
            {
                await operation;
                Assert.Fail("Index completed without starting the cooperative generator.");
            }
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(90), "The product did not start the cooperative generator before the acceptance deadline.");
            await Task.Delay(20);
        }
    }

    public void Dispose() { if (!preserveDirectory && Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}
