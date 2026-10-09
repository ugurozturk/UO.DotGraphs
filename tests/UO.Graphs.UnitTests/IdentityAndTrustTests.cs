using UO.Graphs.Core;
using UO.Graphs.Roslyn;

namespace UO.Graphs.UnitTests;

public sealed class IdentityAndTrustTests
{
    [Fact]
    public void IdentitySeparatesProjectFrameworkConfigurationAndOverloads()
    {
        var project = GraphIdentity.Project("A.csproj", "net10.0", "Debug");
        Assert.Equal(project, GraphIdentity.Project("A.csproj", "net10.0", "Debug"));
        Assert.NotEqual(project, GraphIdentity.Project("B.csproj", "net10.0", "Debug"));
        Assert.NotEqual(project, GraphIdentity.Project("A.csproj", "net9.0", "Debug"));
        Assert.NotEqual(project, GraphIdentity.Project("A.csproj", "net10.0", "Release"));
        Assert.NotEqual(GraphIdentity.Symbol(project, "M:A.Go(System.String)"),
            GraphIdentity.Symbol(project, "M:A.Go(System.Int32)"));
    }

    [Fact]
    public void CanonicalV2IsLengthPrefixedPortableAndSignatureSensitive()
    {
        Assert.Equal("uographs:2:2:é3:a:b", GraphIdentity.Canonical("é", "a:b"));
        Assert.NotEqual(GraphIdentity.Canonical("a", "bc"), GraphIdentity.Canonical("ab", "c"));
        Assert.Equal(GraphIdentity.Project("dir/A.csproj", "net10.0", "Debug"),
            GraphIdentity.Project("dir\\A.csproj", "net10.0", "Debug"));
        Assert.Throws<ArgumentException>(() => GraphIdentity.Project(Path.GetFullPath("A.csproj"), "net10.0", "Debug"));
        var project = GraphIdentity.Project("A.csproj", "net10.0", "Debug");
        Assert.NotEqual(GraphIdentity.Symbol(project, "T:A"), GraphIdentity.Symbol(project, "T:Renamed"));
        Assert.NotEqual(GraphIdentity.Symbol(project, "T:A"), GraphIdentity.Symbol(project, "T:A`1"));
        Assert.NotEqual(GraphIdentity.Symbol(project, "M:A.Go(System.Int32)"), GraphIdentity.Symbol(project, "M:A.Go(System.Int32@)"));
    }

    [Fact]
    public async Task TrustIsCheckedBeforeEvenCheckingInputExistence()
    {
        using var output = new StringWriter();
        var error = await Assert.ThrowsAsync<GraphException>(() =>
            new ProjectIndexer().IndexAsync("does-not-exist.csproj", false, output));
        Assert.Equal(3, error.ExitCode);
        Assert.Contains("--trust-project", error.Message);
        Assert.Equal("", output.ToString());
    }
}
