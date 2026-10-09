using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Fixture.Generator;

[Generator]
public sealed class ProbeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(context.CompilationProvider, (output, compilation) =>
        {
            var marker = Environment.GetEnvironmentVariable("UOGRAPHS_GENERATOR_MARKER");
            if (!string.IsNullOrEmpty(marker)) File.WriteAllText(marker, "generator executed");
            if (compilation.AssemblyName == "ThrowingConsumer") throw new InvalidOperationException("Fixture generator failure");
            output.AddSource("Produced.g.cs", "namespace Fixture.Generated; public class Produced { public int Number => 42; }");
        });
    }
}

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProbeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [];
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        var marker = Environment.GetEnvironmentVariable("UOGRAPHS_ANALYZER_MARKER");
        if (!string.IsNullOrEmpty(marker)) File.WriteAllText(marker, "analyzer executed");
    }
}
