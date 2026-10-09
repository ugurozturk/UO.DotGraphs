using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using UO.Graphs.Core;

namespace UO.Graphs.Roslyn;

public sealed class ProjectIndexer
{
    private static readonly object RegistrationLock = new();
    private static string? registeredPath;

    public async Task<GraphSnapshot> IndexAsync(string input, bool trustProject, TextWriter progress, IndexOptions? options = null)
    {
        if (!trustProject)
            throw new GraphException(3, "Indexing requires --trust-project before MSBuild evaluation. Trusted project build logic may execute; this is not a sandbox or network restriction.");
        options ??= new();
        input = Path.GetFullPath(input);
        if (!File.Exists(input)) throw new GraphException(2, $"Input does not exist: {input}");
        await progress.WriteLineAsync("Checking installed SDK and existing NuGet assets (no restore)...");
        var prepared = await ProjectPreparation.CheckAsync(input, options);
        return await IndexPreparedAsync(input, prepared, options, progress);
    }

    internal static async Task<GraphSnapshot> IndexPreparedAsync(string input, Preparation prepared, IndexOptions options, TextWriter progress, CancellationToken cancellationToken = default)
    {
        var path = prepared.Projects.Values.First().MSBuildPath;
        lock (RegistrationLock)
        {
            if (MSBuildLocator.IsRegistered)
            {
                if (registeredPath != path) throw new GraphException(4, "MSBuildLocator already registered for a different or unknown SDK; use a new process.");
            }
            else
            {
                if (!MSBuildLocator.CanRegister) throw new GraphException(4, "MSBuild was loaded before MSBuildLocator registration; use a new process.");
                MSBuildLocator.RegisterMSBuildPath(path);
                registeredPath = path;
            }
        }
        return await LoadAsync(input, prepared, options, progress, cancellationToken);
    }

    // Prevent Release JIT inlining from loading Microsoft.Build before Locator registration.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<GraphSnapshot> LoadAsync(string input, Preparation prepared, IndexOptions options, TextWriter progress, CancellationToken cancellationToken = default)
    {
        var roslynVersions = new[] { typeof(Compilation), typeof(CSharpCompilation), typeof(Workspace), typeof(MSBuildWorkspace) }
            .Select(t => t.Assembly.GetName().Version).Distinct().ToArray();
        if (roslynVersions.Length != 1) throw new GraphException(4, "Incompatible Roslyn assembly versions; install the locked package set.");
        var entities = new Dictionary<string, GraphEntity>(StringComparer.Ordinal);
        var contexts = new List<ProjectContext>();
        var analysisId = Guid.NewGuid().ToString("N");
        var relationships = new RelationshipBuilder(analysisId);
        var semanticWork = new List<SemanticWork>();
        var observedSources = new Dictionary<string, string>(StringComparer.Ordinal);
        var diagnostics = new List<string>(prepared.Diagnostics);
        var partial = diagnostics.Count > 0;
        var solutionId = GraphIdentity.Hash(GraphIdentity.Canonical("solution", Relative(input), options.Configuration, options.Framework ?? "default"));
        if (!input.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            entities.Add(solutionId, new(solutionId, "solution", Path.GetFileNameWithoutExtension(input), Relative(input), "", "",
                Confidence.Exact, [await FileEvidence(input)], GraphIdentity.Canonical("solution", Relative(input), options.Configuration, options.Framework ?? "default")));
        MSBuildWorkspace CreateWorkspace(string framework)
        {
            var workspace = MSBuildWorkspace.Create(new Dictionary<string, string>
            {
                ["Configuration"] = options.Configuration, ["TargetFramework"] = framework,
                ["NuGetInteractive"] = "false", ["RestoreDuringBuild"] = "false",
                ["RunAnalyzers"] = "false", ["RunAnalyzersDuringBuild"] = "false"
            });
            workspace.LoadMetadataForReferencedProjects = false;
            workspace.SkipUnrecognizedProjects = true;
            return workspace;
        }
        // Homogeneous, generator-disabled solutions share one evaluated workspace and
        // immutable analyzer-free solution. Mixed inner builds retain isolation.
        using var sharedWorkspace = !options.AllowGenerators && !input.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) &&
            prepared.Diagnostics.Count == 0 && prepared.Projects.Values.Select(p => p.Framework).Distinct().Count() == 1
            ? CreateWorkspace(prepared.Projects.Values.First().Framework) : null;
        Solution? sharedSolution = null;
        foreach (var context in prepared.Projects.Values.OrderBy(p => p.Path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var missing in context.SourceFiles.Where(f => !File.Exists(f)))
            {
                partial = true;
                diagnostics.Add($"document: Missing evaluated Compile item: {missing}");
            }
            // Each evaluated project gets its own inner-build TFM; no global TFM override is
            // applied to unrelated solution projects. Roslyn resolves project references locally.
            using var ownedWorkspace = sharedWorkspace is null ? CreateWorkspace(context.Framework) : null;
            var workspace = sharedWorkspace ?? ownedWorkspace!;
            await progress.WriteLineAsync($"Loading {context.Path} [{context.Framework}/{options.Configuration}]...");
            var solutionLoaded = false;
            try
            {
                Project opened;
                if (!input.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && prepared.Diagnostics.Count == 0 &&
                    prepared.Projects.Values.Select(p => p.Framework).Distinct().Count() == 1)
                {
                    var loaded = workspace.CurrentSolution.ProjectIds.Count == 0
                        ? await workspace.OpenSolutionAsync(input, cancellationToken: cancellationToken)
                        : workspace.CurrentSolution;
                    solutionLoaded = true;
                    opened = loaded.Projects.Single(p => p.FilePath == context.Path);
                }
                else opened = await workspace.OpenProjectAsync(context.Path, cancellationToken: cancellationToken);
                var solution = workspace.CurrentSolution;
                var analyzerReferences = solution.Projects.ToDictionary(p => p.Id, p => p.AnalyzerReferences);
                // Strip references from every project before compilation, including dependencies.
                if (sharedSolution is not null) solution = sharedSolution;
                else
                {
                    foreach (var p in solution.Projects.ToArray())
                        solution = solution.WithProjectAnalyzerReferences(p.Id, []);
                    if (sharedWorkspace is not null) sharedSolution = solution;
                }
                if (!options.AllowGenerators && analyzerReferences.Values.Any(a => a.Count > 0))
                {
                    partial = true;
                    diagnostics.Add($"generatorsSkipped: {context.Path}: analyzer/generator assemblies disabled; generated output may be missing. Use --allow-generators only with execution permission.");
                }
                var generatedIds = new HashSet<DocumentId>();
                if (options.AllowGenerators)
                {
                    foreach (var pid in solution.GetProjectDependencyGraph().GetTopologicallySortedProjects())
                    {
                        var generatorProject = solution.GetProject(pid)!;
                        if (generatorProject.Language != LanguageNames.CSharp) continue;
                        var generators = new List<ISourceGenerator>();
                        foreach (var reference in analyzerReferences[pid])
                        {
                            if (reference is AnalyzerFileReference fileReference)
                                fileReference.AnalyzerLoadFailed += (_, args) =>
                                {
                                    diagnostics.Add($"generatorLoad: {fileReference.FullPath}: {args.Message}");
                                };
                            generators.AddRange(reference.GetGenerators(LanguageNames.CSharp));
                        }
                        if (generators.Count == 0) continue;
                        var baseCompilation = await generatorProject.GetCompilationAsync(cancellationToken);
                        if (baseCompilation is null) { partial = true; continue; }
                        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators,
                            generatorProject.AnalyzerOptions.AdditionalFiles,
                            (CSharpParseOptions)generatorProject.ParseOptions!, generatorProject.AnalyzerOptions.AnalyzerConfigOptionsProvider);
                        driver = driver.RunGeneratorsAndUpdateCompilation(baseCompilation, out _, out var generatorDiagnostics, cancellationToken: cancellationToken);
                        foreach (var d in generatorDiagnostics)
                            diagnostics.Add($"generator [{generatorProject.FilePath}]: {d}");
                        partial |= generatorDiagnostics.Any(d => d.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error);
                        foreach (var result in driver.GetRunResult().Results)
                        {
                            foreach (var source in result.GeneratedSources)
                            {
                                var id = DocumentId.CreateNewId(pid);
                                var virtualPath = Path.Combine(prepared.Root, ".uographs-generated", Relative(generatorProject.FilePath!),
                                    source.SyntaxTree.FilePath);
                                solution = solution.AddDocument(id, source.HintName, source.SourceText, filePath: virtualPath);
                                generatedIds.Add(id);
                            }
                        }
                    }
                }
                var project = solution.GetProject(opened.Id)!;
                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation is null) throw new InvalidOperationException("No C# compilation available.");
                await progress.WriteLineAsync($"Reading symbols: {project.Name}");
                var compilationDiagnostics = compilation.GetDiagnostics(cancellationToken);
                partial |= compilationDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
                diagnostics.AddRange(compilationDiagnostics.Where(d => d.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
                    .Select(d => $"compilation [{Relative(context.Path)} {context.Framework}/{options.Configuration}]: {d}"));
                var projectId = GraphIdentity.Project(Relative(context.Path), context.Framework, options.Configuration);
                var parse = (CSharpParseOptions)project.ParseOptions!;
                contexts.Add(new(projectId, context.Path, compilation.AssemblyName ?? project.Name,
                    context.Framework, options.Configuration, context.Sdk, parse.LanguageVersion.ToString(), parse.PreprocessorSymbolNames.Order().ToArray(),
                    context.MSBuildPath, context.MSBuildVersion, context.Frameworks.Where(f => f != context.Framework).ToArray(),
                    context.References, opened.AnalyzerReferences.Select(a => a.FullPath ?? a.Display ?? "unknown").ToArray(),
                    project.ProjectReferences.Select(r => solution.GetProject(r.ProjectId)!).Select(p => new ReferenceContext(
                        p.FilePath ?? p.Name, p.OutputFilePath ?? "", ((CSharpParseOptions)p.ParseOptions!).PreprocessorSymbolNames.Order().ToArray())).ToArray()));
                entities.Add(projectId, new(projectId, "project", Path.GetFileNameWithoutExtension(context.Path), Relative(context.Path), "", projectId,
                    Confidence.Exact, [await FileEvidence(context.Path)], GraphIdentity.ProjectCanonical(Relative(context.Path), context.Framework, options.Configuration),
                    entities.ContainsKey(solutionId) ? solutionId : null));
                var assemblyKey = "A:" + compilation.Assembly.Identity;
                var assemblyId = GraphIdentity.Symbol(projectId, assemblyKey);
                entities.Add(assemblyId, new(assemblyId, "assembly", compilation.AssemblyName!, compilation.Assembly.Identity.ToString(), assemblyKey,
                    projectId, Confidence.Exact, [], GraphIdentity.Canonical("symbol", projectId, assemblyKey), projectId));
                var work = new SemanticWork(context.Path, projectId, compilation, [], []);
                semanticWork.Add(work);
                foreach (var reference in project.ProjectReferences)
                {
                    var referencedProject = solution.GetProject(reference.ProjectId)!;
                    var referencedCompilation = await referencedProject.GetCompilationAsync(cancellationToken);
                    if (referencedCompilation is null || referencedProject.FilePath is null) continue;
                    foreach (var metadata in compilation.References.OfType<CompilationReference>())
                        if (ReferenceEquals(metadata.Compilation, referencedCompilation) && compilation.GetAssemblyOrModuleSymbol(metadata) is IAssemblySymbol referenceAssembly)
                            work.References.Add(new(referenceAssembly, referencedProject.FilePath, referencedCompilation));
                }
                foreach (var document in project.Documents)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if (document.FilePath is { } sourcePath && !generatedIds.Contains(document.Id) && !File.Exists(sourcePath))
                        {
                            partial = true; diagnostics.Add($"document: Missing workspace file: {sourcePath}");
                            continue;
                        }
                        var tree = await document.GetSyntaxTreeAsync();
                        if (tree is null) { partial = true; diagnostics.Add($"document: Cannot load {document.FilePath}"); continue; }
                        var text = (await tree.GetTextAsync()).ToString();
                        if (!generatedIds.Contains(document.Id)) observedSources[tree.FilePath] = GraphIdentity.Hash(text);
                        var generated = generatedIds.Contains(document.Id) || IsGenerated(tree.FilePath, text);
                        if (generated && !options.IncludeGenerated)
                        {
                            diagnostics.Add($"generatedExcluded: {tree.FilePath}");
                            continue;
                        }
                        await SymbolCollector.CollectAsync(compilation, tree, projectId, prepared.Root, generated, entities, diagnostics);
                        work.Documents.Add((tree, generated));
                    }
                    catch (Exception ex) when (ex is IOException or InvalidOperationException)
                    {
                        partial = true; diagnostics.Add($"document: {document.FilePath}: {ex.Message}");
                    }
                }

            }
            catch (Exception ex) when (ex is not (OutOfMemoryException or OperationCanceledException))
            {
                if (sharedWorkspace is not null && !solutionLoaded)
                    throw new GraphException(5, $"Solution workspace could not be loaded; previous snapshot preserved. {ex.Message}");
                partial = true; diagnostics.Add($"projectLoad: {context.Path}: {ex.Message}");
            }
            foreach (var diagnostic in workspace.Diagnostics)
            {
                partial = true;
                diagnostics.Add($"workspace [{context.Path} {context.Framework}/{options.Configuration}] {diagnostic.Kind}: {diagnostic.Message}");
            }
        }
        if (contexts.Count == 0) throw new GraphException(5, "No project could be analyzed; previous snapshot preserved.\n" + string.Join("\n", diagnostics));
        foreach (var context in contexts)
        {
            foreach (var reference in context.ReferenceContexts ?? [])
            {
                var target = contexts.SingleOrDefault(p => p.Path == reference.Path);
                if (target is null || !target.PreprocessorSymbols.SequenceEqual(reference.PreprocessorSymbols))
                {
                    partial = true;
                    diagnostics.Add($"referenceContextMismatch: {context.Path} -> {reference.Path}: reference compilation differs from the indexed context or was not indexed; reference context recorded separately.");
                }
            }
        }
        partial |= contexts.Count != prepared.Projects.Count || diagnostics.Any(d => d.StartsWith("symbol:", StringComparison.Ordinal) || d.StartsWith("generatorLoad:", StringComparison.Ordinal));
        var matchingReferenceContexts = new HashSet<(string SourceId, string TargetPath)>();
        foreach (var work in semanticWork)
        {
            var assemblyContexts = new Dictionary<IAssemblySymbol, string>(SymbolEqualityComparer.Default) { [work.Compilation.Assembly] = work.ProjectId };
            foreach (var reference in work.References)
            {
                var selected = semanticWork.SingleOrDefault(w => w.Path == reference.Path);
                if (selected is not null && CompilationMatches(reference.Compilation, selected.Compilation))
                {
                    assemblyContexts[reference.Assembly] = selected.ProjectId;
                    matchingReferenceContexts.Add((work.ProjectId, reference.Path));
                }
                else
                {
                    partial = true;
                    diagnostics.Add($"relationshipContextMismatch: {work.Path} -> {reference.Path}: target remains a separate reference symbol; selected compilation does not match.");
                }
            }
            var collector = new RelationshipCollector(work.Compilation, work.ProjectId, prepared.Root, assemblyContexts, relationships);
            foreach (var (tree, generated) in work.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await collector.CollectAsync(tree, generated);
            }
            FrameworkExtractors.Extract(new(work.Compilation, work.ProjectId, prepared.Root, work.Documents, collector, relationships, entities, diagnostics));
        }
        partial |= diagnostics.Any(d => d.StartsWith("framework:", StringComparison.Ordinal));
        foreach (var entity in entities.Values)
        {
            if (entity.ParentId is not { } parentId || !entities.TryGetValue(parentId, out var parent)) continue;
            var evidence = entity.Sources.Length > 0 ? entity.Sources : entities[entity.ProjectId].Sources;
            foreach (var source in evidence)
                relationships.Add(RelationshipKind.DECLARES, Endpoint(parent), Endpoint(entity), entity.ProjectId,
                    Confidence.Exact, source, "Direct declaration ownership from Roslyn symbols or evaluated project structure.");
        }
        foreach (var context in contexts)
            foreach (var reference in context.ProjectReferences ?? [])
            {
                var target = contexts.SingleOrDefault(c => c.Path == reference);
                var matches = target is not null && matchingReferenceContexts.Contains((context.Id, reference));
                relationships.Add(RelationshipKind.PROJECT_REFERENCES, Endpoint(entities[context.Id]),
                    matches ? Endpoint(entities[target!.Id]) : null, context.Id,
                    matches ? Confidence.Exact : Confidence.Unresolved, entities[context.Id].Sources[0],
                    "Evaluated MSBuild ProjectReference; source project file is evidence, not an XML item span. " +
                    (matches ? "Referenced compilation matches selected target context." : "Referenced compilation is missing or differs from selected target context."),
                    reference, target is null ? [] : [Endpoint(entities[target.Id])]);
            }
        RelationshipSymbol Endpoint(GraphEntity entity) => new(entity.Id, entity.DocumentationId, entity.QualifiedName,
            contexts.FirstOrDefault(c => c.Id == entity.ProjectId)?.AssemblyName ?? "", entity.ProjectId, entity.Id);
        RelationshipSymbol Link(RelationshipSymbol symbol) => symbol with { EntityId = entities.ContainsKey(symbol.Id) ? symbol.Id : null };
        return new(new(analysisId, DateTimeOffset.UtcNow, input, "0.9.0",
            typeof(Compilation).Assembly.GetName().Version!.ToString(), partial ? "partial" : "completeWithinDeclaredScope", "notChecked",
            ["Phase 9: ABP/EF Core/Refit static declarations; conventional exposure, effective models, remote service identities and runtime configuration are partial/unresolved. UO.Mediator/MediatR/MassTransit bound messaging contracts; candidates are inferred, runtime registration/topology/order unresolved and external consumers outside scope. See docs/MESSAGING.md.",
             "Phase 7: Microsoft DI/ASP.NET Core static declarations; runtime activation, container selection/order, scanning, dynamic registration, MVC conventions and arbitrary route/handler data flow are not proven. See docs/FRAMEWORKS.md.",
             "Phase 3: semantic relationships in selected C# contexts. CALLS denotes compile-time binding, never runtime dispatch; see docs/RELATIONSHIPS.md.",
             "Solution entries are loaded as project inner builds using the requested configuration uniformly; solution configuration mappings are not applied.",
             "Diagnostic analyzers are not run. Generator execution is opt-in. Generated declarations follow the explicit include/exclude policy.",
             "Delegate/dynamic/reflection runtime targets are unresolved. No runtime dispatch resolution, data-flow target recovery or implicit protocol lowering. Source freshness is reported separately from analysis coverage."],
            diagnostics.Distinct().ToArray(), contexts.ToArray(), GraphIdentity.Version, prepared.Root,
            options.IncludeGenerated ? "includeWorkspaceOnly" : "exclude", options.AllowGenerators),
            entities.Values.OrderBy(e => e.Id, StringComparer.Ordinal).ToArray())
        {
            ObservedSources = observedSources,
            Relationships = relationships.Relationships.Select(r => r with { Source = Link(r.Source), Target = r.Target is null ? null : Link(r.Target) }).ToArray(),
            Occurrences = relationships.Occurrences.Select(o => o with { RelatedSymbols = o.RelatedSymbols.Select(Link).ToArray() }).ToArray()
        };

        string Relative(string path) => Path.GetRelativePath(prepared.Root, path).Replace('\\', '/');
    }

    private sealed record ReferencedCompilation(IAssemblySymbol Assembly, string Path, Compilation Compilation);
    private sealed record SemanticWork(string Path, string ProjectId, Compilation Compilation,
        List<(SyntaxTree Tree, bool Generated)> Documents, List<ReferencedCompilation> References);

    internal static bool CompilationMatches(Compilation reference, Compilation selected) =>
        CompilationMatches(reference, selected, new HashSet<(Compilation, Compilation)>());

    private static bool CompilationMatches(Compilation reference, Compilation selected, HashSet<(Compilation, Compilation)> visited)
    {
        if (ReferenceEquals(reference, selected)) return true;
        if (!visited.Add((reference, selected))) return true;
        var references = reference.References.ToArray();
        var selectedReferences = selected.References.ToArray();
        if (references.Length != selectedReferences.Length) return false;
        // Order and reference properties affect binding (aliases, embed interop types).
        // File paths/assembly identities alone cannot prove equal metadata content.
        foreach (var pair in references.Zip(selectedReferences))
        {
            if (!pair.First.Properties.Equals(pair.Second.Properties)) return false;
            if (pair.First is CompilationReference leftCompilation && pair.Second is CompilationReference rightCompilation)
            {
                if (!CompilationMatches(leftCompilation.Compilation, rightCompilation.Compilation, visited)) return false;
            }
            else if (pair.First is PortableExecutableReference leftPe && pair.Second is PortableExecutableReference rightPe)
            {
                if (!ModuleIds(leftPe).SequenceEqual(ModuleIds(rightPe))) return false;
            }
            else return false;
        }
        if (!reference.Assembly.Identity.Equals(selected.Assembly.Identity)) return false;
        var a = (CSharpCompilationOptions)reference.Options;
        var b = (CSharpCompilationOptions)selected.Options;
        if (a.OutputKind != b.OutputKind || a.Platform != b.Platform || a.AllowUnsafe != b.AllowUnsafe ||
            a.CheckOverflow != b.CheckOverflow || a.NullableContextOptions != b.NullableContextOptions ||
            a.OptimizationLevel != b.OptimizationLevel) return false;
        var left = reference.SyntaxTrees.OrderBy(t => t.FilePath, StringComparer.Ordinal).ToArray();
        var right = selected.SyntaxTrees.OrderBy(t => t.FilePath, StringComparer.Ordinal).ToArray();
        return left.Length == right.Length && left.Zip(right).All(pair =>
            pair.First.FilePath == pair.Second.FilePath && pair.First.Options.Equals(pair.Second.Options) &&
            pair.First.GetText().ContentEquals(pair.Second.GetText()));
    }


    private static IEnumerable<Guid> ModuleIds(PortableExecutableReference reference) => reference.GetMetadata() switch
    {
        AssemblyMetadata assembly => assembly.GetModules().Select(module => module.GetModuleVersionId()),
        ModuleMetadata module => [module.GetModuleVersionId()],
        _ => []
    };

    private static async Task<SourceEvidence> FileEvidence(string path) =>
        new(path, 1, 1, 1, 1, Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(path))));

    private static bool IsGenerated(string path, string text)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Split('/').Any(p => p.Equals("obj", StringComparison.OrdinalIgnoreCase) || p.Equals("bin", StringComparison.OrdinalIgnoreCase))
            || normalized.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
            || text.AsSpan(0, Math.Min(text.Length, 2048)).Contains("<auto-generated", StringComparison.OrdinalIgnoreCase);
    }
}
