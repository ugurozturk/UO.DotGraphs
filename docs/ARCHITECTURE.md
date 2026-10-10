# Architecture

## Projects and dependencies

| Project | Responsibility | Project references |
| --- | --- | --- |
| `UO.Graphs.Core` | Graph, identity, evidence, query/delivery contracts, source policy, fingerprints | None |
| `UO.Graphs.Roslyn` | Trust-controlled preparation, workspace loading, symbol and relationship extraction | Core |
| `UO.Graphs.Storage.Sqlite` | Versioned local storage, transactions, queries, bounded graph delivery | Core |
| `UO.Graphs.Cli` | Input validation, composition, commands, output and exit codes | Core, Roslyn, Storage.Sqlite |

Core has no Roslyn or SQLite package dependency. Roslyn does not manage storage;
the CLI composes the indexing and publication flow. Test and fixture projects are
separate from these four product projects. There is no generic repository, in-app
mediator, event bus, remote service, or dynamic plugin loader.

```text
Local C# project/solution
  -> trust and prerequisite checks
  -> MSBuild preparation + dependency fingerprint
  -> Roslyn workspace + semantic/framework extraction (unless safe no-op)
  -> source/input validation
  -> atomic SQLite graph + fingerprint publication
  -> read-only CLI query/context/export
```

## Current contracts

Product **0.9.0**, query/index contract **4**, SQLite schema **3**, context/export
format **1**, declaration identity **2**. These versions describe different
contracts and must not be substituted for each other. Phase-specific older
versions are historical records under `docs/history/`.

An analysis snapshot records its ID/time/tool version, logical root and input,
SDK/Roslyn versions, project TFM/configuration/preprocessor/reference contexts,
generated-code policy, diagnostics, coverage, and limitations. Entities preserve
canonical IDs and all indexed declaration locations. Relationships retain kind,
direction, context, confidence, and typed endpoints. Occurrences preserve
source ranges/hashes, rules, bound signatures, and unresolved candidates.

Coverage, confidence, freshness, and query truncation are independent. `exact`
proves the stated static meaning, never runtime execution. Unresolved targets
remain null. External and excluded-source symbols remain endpoints without fake
source entities. See [IDENTITY](IDENTITY.md) and [RELATIONSHIPS](RELATIONSHIPS.md).

## Workspace and semantic analysis

`--trust-project` is checked before target evaluation. The target SDK and restored
assets must already exist. MSBuildLocator registers before workspace APIs load;
a process registration lock, `CanRegister`, `NoInlining` boundary, and Roslyn
assembly-version checks protect initialization. Microsoft.Build runtime assets are
not copied into the product.

Configuration is global to the analysis; multi-target projects require a selected
TFM. Reference compilation contexts are compared before source IDs are unified.
The comparison includes assembly identity, parse and compilation options, source
content, reference properties/aliases/EmbedInteropTypes, PE module MVIDs, and
recursive compilation references. Mismatches remain explicit and partial.

Generators require separate `--allow-generators` permission. Without it, analyzer
references are removed before compilation and potential missing output is reported.
Permitted generators run through Roslyn's driver with real parse options,
additional files, and analyzer configuration. Diagnostic analyzers are not run.
Generated declaration inclusion is a separate policy. See [WORKSPACE](WORKSPACE.md).

Calls and references use SemanticModel, symbol binding, and IOperation; there is
no name/regex-based call matching. `CALLS` retains compile-time binding. Runtime
delegate/dynamic/reflection targets remain unresolved. A usable partial analysis
may be saved with explicit coverage; preparation/fatal failure preserves the old
snapshot.

For homogeneous, diagnostic-free, same-TFM solutions with generators disabled,
projects share one workspace and an immutable analyzer-free Solution. Mixed TFM,
project-input, and generator paths remain separate. This optimization does not
introduce method- or project-granular incremental reuse.

## Framework extraction

Small internal `IFrameworkExtractor` implementations are statically registered
inside Roslyn. `FrameworkExtractionContext` provides compilation/documents,
identity resolution, source evidence, and graph construction, without host or
storage access. Extractors bind real framework symbols and metadata assemblies.
The analyzed application, DI container, broker, DbContext, and HTTP client are not
started.

| Extractor area | Preserved information | Contract |
| --- | --- | --- |
| Microsoft DI / ASP.NET Core | Registration declarations, service parameters, endpoint/handler and constructor candidates | [FRAMEWORKS](FRAMEWORKS.md) |
| UO.Mediator / MediatR / MassTransit | Closed message identity, dispatch/publish, handler/consumer/behavior contracts and candidates | [MESSAGING](MESSAGING.md) |
| ABP | Service/module/repository contracts and inferred exposure/DI candidates | [ABP-EF-REFIT](ABP-EF-REFIT.md) |
| EF Core | Source model/mapping declarations, not an effective runtime model | [ABP-EF-REFIT](ABP-EF-REFIT.md) |
| Refit | Client interface contracts, unresolved base address and remote endpoint | [ABP-EF-REFIT](ABP-EF-REFIT.md) |

Optional framework metadata uses existing SQLite JSON payloads. New extraction
rules do not change CALLS into runtime dispatch edges. Framework package fixtures
are separate from product dependencies; the ABP LGPL closure is isolated in its
non-packable analysis fixture.

## Storage, queries, and delivery

SQLite stores one committed snapshot. Schema 3 contains endpoint nodes, entities,
relationships, occurrences, and optional fingerprint data, with foreign keys and
adjacency/evidence indexes. A process-wide OS file lease is held from before
analysis through commit. WAL and one transaction publish graph and manifest
together. Errors/cancellation roll back; readers see one consistent generation.

Queries use one read-only transaction and parameterized, bounded SQL reads.
Deterministic BFS tracks visited nodes, direction, work/depth/result limits, and
occurrence truncation. Impact traverses incoming dependency edges only;
REFERENCES and DECLARES are not propagation rules. See [QUERIES](QUERIES.md).

Context/export use bounded selection in Storage and contracts/serialization in
Core. Context enforces a whole-stdout Unicode-scalar budget by removing records,
while retaining uncertainty and truncation. Opt-in snippets require a decoded
source hash match and the source path policy. Export preserves original evidence,
including JSON comments in Mermaid/DOT. See [CONTEXT-EXPORT](CONTEXT-EXPORT.md).

Status supports old schemas 1/2; current query/delivery commands require explicit
re-indexing. Successful indexing replaces the old schema atomically. Read commands
never migrate or repair a database. Unknown nonempty schemas are rejected.

## Incremental indexing and freshness

`IncrementalIndexer` performs trust-gated preparation/preprocessing and SHA-256
inventory/content checks. Only an unchanged, complete dependency model allows a
no-op without workspace loading or replacement. All other cases rebuild the
entire selected project closure. Custom build/generator dependencies outside the
validated model disable reuse.

Each read uses its own snapshot's manifest. Optional freshness checks read the
filesystem/environment without MSBuild, restore, or subprocesses. Default is
`notChecked`; required freshness rejects stale/unknown with exit 9. See
[INCREMENTAL](INCREMENTAL.md).

## SDK, dependencies, distribution, and trust

`global.json` pins **10.0.201**, `allowPrerelease: false`, `rollForward: disable`.
Central exact package versions and lock files pin dependency resolution. The
[license inventory](DEPENDENCIES.md) includes development fixtures and bundled
native/BuildHost components; it is not a list of tool runtime dependencies alone.
Every dependency change requires stable-version, compatibility, and license
verification. Non-permissive or unknown licenses are not silently accepted.

The `UO.Graphs.Cli` .NET tool packages the four product projects, required payloads,
licenses, documentation, and distribution helpers. GitHub CI tests and packs one
artifact; a separate publishing workflow verifies that artifact before OIDC-based
NuGet publication. No runtime upload is added. [RELEASE](RELEASE.md) describes
installation and platform evidence; [PERFORMANCE](PERFORMANCE.md) records measured
costs rather than assumed budgets.

The Copilot skill and its byte-matched standalone template are terminal consumers.
The installer refuses existing/symlinked destinations and changes no permissions.
They do not introduce a product dependency on Copilot. See [COPILOT-SKILL](COPILOT-SKILL.md).

Trust does not isolate custom tasks or generators. Preparation subprocess output
and cancellation bounds do not automatically constrain Roslyn BuildHost or
in-process generators. The exact boundaries are in [SECURITY](SECURITY.md).
