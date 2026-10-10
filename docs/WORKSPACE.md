# Workspace and execution contract

SDK-style C# `.csproj`, `.sln`, and `.slnx` inputs are supported. Solution folders
are not projects. VB/F#/C++ and legacy non-SDK C# inputs produce diagnostics.
No preparable project means exit 4; no compilation means exit 5. The previous
snapshot is preserved on these failures.

## Trust and initialization

`--trust-project` is checked before target evaluation. MSBuild imports, targets,
and tasks may execute code; permission is not a sandbox/network block. The CLI
never restores, downloads, or updates dependencies at runtime.

SDK selection runs `dotnet --version` in each target project directory and honors
its `global.json`. Preparation evaluates actual MSBuildToolsPath, MSBuildVersion,
ProjectAssetsFile, TargetFramework(s), ProjectReference, and Compile items. Missing
root SDK/assets stop indexing; an unloadable dependency can make analysis partial.
A single snapshot cannot mix SDK installations; use separate processes.

MSBuildLocator registers before workspace APIs load, guarded by `CanRegister`, a
process registration lock, and a `NoInlining` boundary. External or different-SDK
registrations are rejected. Microsoft.Build runtime assets are not copied into
the tool. Common/CSharp/Workspaces/MSBuild Roslyn assembly versions are compared.
The pinned set uses Roslyn **5.0.0** and Locator **1.11.2**. SDK **10.0.201**,
MSBuild **18.3.0**, and runtime **10.0.5** form the recorded local acceptance base;
[STATUS](STATUS.md) adds current platform results.

Initialization references: [Locator guidance](https://learn.microsoft.com/en-us/visualstudio/msbuild/find-and-use-msbuild-versions)
and the [Roslyn Release inlining issue](https://github.com/dotnet/roslyn/issues/78478).

## Configuration and target framework

`--configuration` defaults to `Debug`; Release or custom names can be selected.
MSBuild evaluates conditional ProjectReference/Compile/DefineConstants, and the
actual Roslyn preprocessor symbols are recorded. Multi-target projects require
`--framework`; index each TFM separately. Other TFMs appear in
`skippedTargetFrameworks`; other single-target solution projects retain their TFM.

Homogeneous solution contexts use `OpenSolutionAsync`; mixed-TFM or partly
unsupported solutions load listed projects with `OpenProjectAsync`. Configuration
is applied globally, not through a solution's per-project configuration/platform
mapping matrix. RID/platform selection is not exposed by this CLI.

Project references are not forced to metadata-only loading. Their compilation
preprocessor symbols and output paths are preserved in `referenceContexts`.
Unloaded or mismatched reference contexts produce partial analysis and separate
identities. Source IDs are unified only when compilation context matches, including
reference properties, PE module identities, and recursive compilation references.

The optimized same-TFM, diagnostic-free solution path shares one analyzer-free
immutable Solution when generators are disabled. Other paths remain separate.
See [ARCHITECTURE](ARCHITECTURE.md) and [PERFORMANCE](PERFORMANCE.md).

## Diagnostics and partial publication

Workspace diagnostics, compilation warnings/errors, missing Compile files,
project failures, and generator load/execution diagnostics are stored. If at least
one compilation is usable, available declarations can be published atomically
with `coverage: partial`, replacing the previous snapshot. Successful publication
returns exit 0 even when partial: always inspect `analysis.coverage`.
Preparation/fatal errors preserve the database. Partial empty output never proves
that a relationship is absent.

## Generator and analyzer policy

Without `--allow-generators`, analyzer references are removed from **all** workspace
projects before compilation. Even generator assembly discovery is skipped.
References, including SDK-provided ones, produce `generatorsSkipped` and partial
coverage because output cannot be ruled out without executing them.
`--trust-project` does not imply generator permission.

With permission, Roslyn GeneratorDriver uses actual parse options, AdditionalFiles,
and AnalyzerConfigOptions in dependency order. Outputs enter the in-memory
workspace; dependent compilations can consume generated types. Load/execution
failures and diagnostics affect coverage. No `WithAnalyzers` call executes
DiagnosticAnalyzer, and MSBuild RunAnalyzers/RunAnalyzersDuringBuild are disabled.
Loading an assembly can still have side effects; custom MSBuild code can execute
its own programs.

Cancellation is cooperative. A real generator token/cancellation test exists, but
a generator that ignores cancellation is not forcibly isolated. Preparation
subprocess time/output limits do not apply to every BuildHost or generator path.
See [SECURITY](SECURITY.md).

`--generated exclude` is the default. `include` indexes workspace Compile documents
and permitted Roslyn outputs; it does not scan obj/bin for declarations. A workspace
document is classified as generated by an obj/bin path component, `.g.cs`,
`.generated.cs`, `.designer.cs`, or `<auto-generated` in the first 2,048 characters.
Other manually included generated files are treated as ordinary source.
Each excluded file produces `generatedExcluded`. It stays in semantic compilation
while its declarations are excluded from the index. Completeness is relative to
this declared policy.

Generated evidence uses virtual paths:
`.uographs-generated/<relative-project>/<generator>/<hint>`. No corresponding disk
file is created; hashes come from actual SourceText, with `SourceEvidence.generated`.

## Fixtures and SDK selection

`samples/Phase2` contains both solution formats; a net10.0/netstandard2.1 library;
an app reference; overload/generic/partial/nested/operator/extension, file-local,
and lexical local-function cases; deliberately broken references/files; and
Generator/GeneratedConsumer permission, policy, and exception cases.

Copied integration fixtures also copy the repository `global.json`. The regression
`CopiedFixtureSelectsRepositorySdkOutsideCheckout` places a conflicting `99.0.100`
request in a temporary parent and verifies actual `dotnet --version` selection.
This prevents CI machines with multiple SDKs from drifting outside the fingerprint
model. See [STATUS](STATUS.md) for the observed failure and passing fix.

The generator fixture targets netstandard2.1 for the product's .NET 10 host, not
portable VS/.NET Framework analyzer distribution. RS1041 and RS1036 warnings are
not suppressed. A rejected netstandard2.0 experiment introduced a legacy,
non-permissive Microsoft.NETCore.Platforms dependency; it was removed from the
final set. NuGet vulnerability-service access and license audit are separate checks.
