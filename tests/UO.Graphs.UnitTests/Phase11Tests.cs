using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UO.Graphs.Core;
using UO.Graphs.Roslyn;

namespace UO.Graphs.UnitTests;

public sealed class Phase11Tests
{
    [Fact]
    public async Task PreparationOutputIsBoundedWithoutTruncatingValidJson()
    {
        Assert.Equal("{}", await ProjectPreparation.ReadBoundedAsync(new StringReader("{}"), 2, default));
        var error = await Assert.ThrowsAsync<GraphException>(() =>
            ProjectPreparation.ReadBoundedAsync(new StringReader(new string('x', 8193)), 8192, default));
        Assert.Equal(4, error.ExitCode);
        Assert.Contains("safety limit", error.Message);
    }

    [Fact]
    public async Task CancellationAlsoInterruptsOutputDrainAfterParentExit()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProjectPreparation.ReadBoundedAsync(new HoldingReader(), 8192, cancellation.Token));
    }

    [Fact]
    public void IdenticalSourceWithDifferentReferencesIsNotTheSameAnalysisContext()
    {
        var syntax = CSharpSyntaxTree.ParseText("class C {}", path: "C.cs");
        var firstDependency = CSharpCompilation.Create("Dependency", [CSharpSyntaxTree.ParseText("class A {}")]);
        var secondDependency = CSharpCompilation.Create("Dependency", [CSharpSyntaxTree.ParseText("class B {}")]);
        var first = CSharpCompilation.Create("Consumer", [syntax], [firstDependency.ToMetadataReference()]);
        var same = CSharpCompilation.Create("Consumer", [syntax], [firstDependency.ToMetadataReference()]);
        var different = CSharpCompilation.Create("Consumer", [syntax], [secondDependency.ToMetadataReference()]);
        Assert.True(ProjectIndexer.CompilationMatches(first, same));
        Assert.False(ProjectIndexer.CompilationMatches(first, different));
        Assert.False(ProjectIndexer.CompilationMatches(first, first.RemoveAllReferences()));
        Assert.False(ProjectIndexer.CompilationMatches(first,
            first.WithReferences(firstDependency.ToMetadataReference(aliases: System.Collections.Immutable.ImmutableArray.Create("other")))));
    }

    private sealed class HoldingReader : TextReader
    {
        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }
}
