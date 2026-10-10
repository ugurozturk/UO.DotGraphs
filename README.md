# UO.Graphs

UO.Graphs turns local SDK-style C# projects and solutions into a Roslyn-based code
knowledge graph in SQLite. Use the `uographs` CLI to find symbols, trace static
calls and dependencies, inspect potential change impact, and export bounded context.

Analysis stays local. The CLI requires no Copilot account, MCP server, hosted
service, API key, or telemetry. It never automatically restores or downloads
project dependencies. Indexing requires explicit permission to evaluate the
project with MSBuild through `--trust-project`.

Source version: **0.12.0**. Package: **UO.Graphs.Cli**. Standard query/status JSON: **6**; opt-in compact query: **1**;
SQLite schema: **3**; context/export format: **1**; declaration identity: **2**.
Validation is recorded per version and platform in [verified status](docs/STATUS.md).
Full release acceptance remains incomplete, including Windows, physical offline
validation, and real Copilot E2E.

## What you can inspect

- C# symbols, declarations, compile-time calls, type dependencies, inheritance,
  implementations, and potential change impact.
- Microsoft DI registrations and ASP.NET Core endpoint declarations.
- UO.Mediator, MediatR, and MassTransit message/handler contracts and candidates.
- ABP service/module/repository contracts, EF Core mapping declarations, and
  Refit HTTP client contracts.
- Bounded context for an assistant, or JSON, NDJSON, Mermaid, and DOT exports.

Framework support has explicit API/version limits. The linked support matrices
under [Documentation](#documentation) describe those limits; static evidence
does not establish runtime activation, message delivery, or HTTP routing.

## Install

As checked on **2026-10-10**, the latest version listed in the
[NuGet version index](https://api.nuget.org/v3-flatcontainer/uo.graphs.cli/index.json)
is **0.10.0**. This README describes **0.12.0 source**; the skill in this checkout
requires CLI 0.12.0 / query-status v6. The published 0.10.0 tool uses v5 and is
not compatible with that skill. Use [Build from source](#build-from-source) for
0.12.0; its publication is not claimed here.

A .NET 10 runtime is required to run the tool. Indexing also requires the target's
selected SDK, reference packs, and previously restored assets.

To install the published 0.10.0 tool:

```sh
dotnet tool install --global UO.Graphs.Cli --version 0.10.0 --source https://api.nuget.org/v3/index.json
uographs --help
uographs doctor
```

Update an existing installation with
`dotnet tool update --global UO.Graphs.Cli --version 0.10.0 --source https://api.nuget.org/v3/index.json`.
If the command is missing,
check the global tool directory in PATH: `$HOME/.dotnet/tools` on macOS/Linux or
`%USERPROFILE%\.dotnet\tools` on Windows. The version appears in `--help`;
there is no `--version` option.

For a repository-local installation, run `dotnet new tool-manifest` once if no
manifest exists, then:

```sh
dotnet tool install --local UO.Graphs.Cli --version 0.10.0 --source https://api.nuget.org/v3/index.json
dotnet tool run uographs --help
# On another machine, install the version recorded in the committed manifest:
dotnet tool restore
```

Use `dotnet tool run uographs` in place of `uographs` for a local manifest.
Available versions are listed on the [NuGet package page](https://www.nuget.org/packages/UO.Graphs.Cli).
Installation and dependency preparation are explicit steps, separate from runtime.

## First index and query

Run from the target repository. Prepare dependencies explicitly before indexing:

```sh
dotnet restore Example.slnx
uographs index Example.slnx --trust-project
uographs query find MyService --limit 20
uographs status
```

The default database is `.uographs/graph.db` in the current directory. Pass
`--db <path>` to index, status, query, context, or export to select another database.
`--trust-project` permits MSBuild evaluation and project-defined code execution;
it is not a sandbox or network block.

```sh
uographs query symbol '<symbol-id>'
uographs query callers '<symbol-id>' --depth 2
uographs query callees '<symbol-id>' --edge-types CALLS
uographs query dependencies '<symbol-id>' --include-inferred
uographs query path '<from-id>' '<to-id>' --depth 8 --max-edges 10000
uographs query impact '<symbol-id>' --depth 5 --limit 100
```

Replace quoted placeholders with IDs returned by `find`. Exact, unique names also
work. Ambiguous names return candidates and require selection; the first candidate
is never selected silently. Path returns one bounded shortest path. Impact reports
potential static dependencies with evidence, not guaranteed runtime effects.

## Query and status output

Query and status default to compact JSON with a **12,000 Unicode-scalar** budget,
including serialized escapes and the final newline. This is a character budget,
not a token count. Increase it for a larger answer, or request full metadata for
a specific investigation:

```sh
uographs query callers '<symbol-id>' --max-chars 20000
uographs status --details full
uographs query symbol '<symbol-id>' --details full
```

In v6, summary output samples diagnostics, limitations, and project metadata
before removing whole result items. `metadataSummary.fields` records sampled
fields and their omitted/shortened counts; `budget.actualChars` records output
size. Metadata omissions are separate from graph `truncated` and coverage.
An empty sampled list does not prove the absence of limitations. Follow
`freshnessCheck.reasonsReference` when it points to shared fingerprint reasons.

`--details full` preserves original metadata without a character cap. It still
uses v6 and respects query traversal/result/evidence limits; it cannot be combined
with `--max-chars`. If the minimum envelope and an indivisible result cannot fit,
the command returns exit 2 with empty stdout and reports the required budget.

Existing schema 3 indexes need no rebuild for 0.12.0. Consumers of v5 must account
for metadata sampling; see [0.11 migration](docs/MIGRATION-0.11.md).

For workflow review, read the short source body and relevant tests, then request
only the graph links you need:

```sh
uographs query callees '<symbol-id>' --view compact --calls exclude-properties --evidence-limit 2 --max-chars 12000
```

Compact v1 interns symbols and evidence, preserves full IDs, and explicitly filters
property accessors (including custom bodies). One-hop queries provide snapshot-bound
continuations instead of requiring a larger budget. Use `--calls only-properties`
for the complementary inspection. The standard v6 view is unchanged. See the
[compact contract, actual examples and measurements](docs/AI-OUTPUT.md).

## Context and export

```sh
uographs context Greet --max-chars 12000 --format json
uographs context '<symbol-id>' --include-source --max-chars 20000
uographs export --format json
uographs export --format ndjson --symbol '<symbol-id>' --depth 2
uographs export --format mermaid --symbol Greet
uographs export --format dot --symbol Greet
```

Context contains the selected symbol, nearby relationships, caller/callee summaries,
source locations, uncertainty, and suggested follow-up queries. `--max-chars` counts
Unicode scalar values across the entire JSON stdout, including the final newline.
It is not a token budget. Whole records are removed to fit; JSON stays valid and
truncation stays visible. A budget below the minimum envelope produces exit 2 and
empty stdout. Lexical matches always require explicit ID selection.

Source text is off by default. `--include-source` adds bounded C# snippets for the
selected symbol only, subject to path/privacy checks and a matching snapshot hash.
Export does not read source files. Graph labels, comments, snippets, and diagnostics
are untrusted data. Sending them to an AI assistant crosses a separate privacy
boundary governed by your organization's policy.

To write an export without overwriting an existing file:

```sh
python3 scripts/safe_export.py new.ndjson -- uographs export --format ndjson
```

Ordinary shell `>` redirection may overwrite a file. See
[context/export](docs/CONTEXT-EXPORT.md) and [security](docs/SECURITY.md).

## Indexing options and freshness

| Option | Behavior |
| --- | --- |
| `--trust-project` | Required before target MSBuild evaluation in both index modes |
| `--incremental` / `--full` | Incremental is the default; full always rebuilds the selected scope |
| `--configuration <name>` | Default `Debug`; evaluates conditional compilation and references |
| `--framework <TFM>` | Required for multi-target projects; other TFMs are reported as skipped |
| `--root <directory>` | Common logical root for portable IDs; must contain the input and projects |
| `--allow-generators` | Allows generator assembly loading and execution; off by default |
| `--generated include\|exclude` | Declaration policy, default `exclude`; does not grant generator permission |

Generators/analyzers are removed from workspace compilations by default. Projects
with such references, including SDK references, can therefore be `partial`.
Generated-code inclusion does not make arbitrary generator code safe or isolated.

Incremental granularity is **the entire selected project closure**. An unchanged,
complete dependency fingerprint skips Roslyn analysis and retains the snapshot ID.
Changed inputs rebuild the whole scope. Unknown custom build/generator dependencies
disable reuse. Even a no-op performs SDK/MSBuild preparation and content hashing.

Read commands default to `notChecked`, which makes no freshness claim.
`--check-freshness` hashes tracked inputs without MSBuild or restore;
`--require-fresh` rejects `stale` and `unknown` with exit 9.
Coverage and freshness are independent. See [INCREMENTAL](docs/INCREMENTAL.md).

## Evidence and compatibility

`exact` means the declared static relationship is proven in the analysis context.
`inferred` preserves a candidate rule and its evidence. `unresolved` preserves the
reason without inventing a target. Interface/virtual `CALLS` remain bound to their
compile-time slots; dynamic, delegate, and reflection targets are not guessed.

Entities include solution, project, assembly, namespace, type, method, constructor,
property, field, event, accessor, local function, lambda, and specialized framework
records. Partial declarations share an ID and retain multiple source locations.
IDs are independent of absolute checkout paths and line numbers within the
[identity contract](docs/IDENTITY.md).

Each database contains one snapshot. A usable partial analysis can atomically
replace it and returns exit 0 with explicit coverage. Preparation, fatal analysis,
or write failures preserve the previous committed snapshot. SQLite WAL and an OS
writer lease protect publication; readers see one consistent generation.

Help is plain text; export uses the selected format. Other successful commands
emit one JSON document to stdout; progress and errors go to stderr. Fatal errors
leave stdout empty. Query JSON v6 contains `snapshotId`, `analysisContext`,
`completeness`, `stale`, `truncated`, `items`, `diagnostics`, effective options,
limits, and outcome. Status carries `contractVersion: 6`; index/doctor retain
`contractVersion: 4`.
The status `schemaVersion` describes SQLite, currently 3.

Old SQLite schemas 1/2 can be inspected with status; current query/context/export
require explicit re-indexing. Successful indexing replaces them atomically with
schema 3. Unknown nonempty schemas are not overwritten. Old identity-v1 records
are not reinterpreted as identity v2. See the
[CLI envelope schema](docs/schemas/cli-envelope.schema.json).

| Exit | Meaning |
| --- | --- |
| 0 | Success, including partial snapshots and empty queries; inspect coverage |
| 2 | Invalid command/options/input/root, or insufficient query/status/context envelope budget |
| 3 | Missing trust flag; target not evaluated |
| 4 | Missing SDK/assets/index prerequisite, unsupported input, or missing TFM selection |
| 5 | No analyzable compilation or fatal analysis failure |
| 6 | SQLite/schema/file access or corrupt graph JSON failure |
| 7 | Ambiguous symbol or lexical candidates; select an exact ID |
| 8 | Symbol not found in the searched snapshot/scope |
| 9 | Required freshness not established: stale or unknown |
| 10 | Another writer holds the lease; no automatic retry |
| 130 | Operation cancelled |

## Build from source

Development uses SDK **10.0.201**, pinned with roll-forward disabled in
`global.json`. Restore is an explicit development action:

```sh
dotnet restore UO.Graphs.slnx --locked-mode -m:1 -nr:false
dotnet restore samples/Phase2/Fixtures.slnx --locked-mode -m:1 -nr:false
dotnet restore samples/Phase3/Semantics.csproj --locked-mode -m:1 -nr:false
dotnet build samples/Phase2/Generator/Generator.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false
python3 scripts/audit_licenses.py --check
```

`Fixtures.slnx` contains negative fixtures and is not built as a whole; restore
reports an intentionally missing reference. Generator fixture warnings and host
limits are documented in [WORKSPACE](docs/WORKSPACE.md).

From the repository root, without installing a tool:

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- --help
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- doctor
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- query find Greet --format json
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- status
```

## GitHub releases and agent skills

The [CI workflow](.github/workflows/ci.yml) restores locked dependencies, builds
and tests Release, audits licenses, packs, and installs the actual tool to test
index/query/context. Automatic runs require a change matching `src/**/*.cs` on a
`main` push or pull request. README, documentation, tests, scripts, configuration,
and version-only changes do not trigger CI by themselves; manual dispatch is
available. A successful first-party `main`-push CI run triggers
[publishing](.github/workflows/publish-nuget.yml) of that verified artifact through
NuGet Trusted Publishing. PR and manually triggered CI runs do not publish.
Version comes directly from `<Version>` in `Directory.Build.props`; no run number
is appended. Existing NuGet versions are skipped. See
[release setup](docs/RELEASE.md#github-actions-and-nuget-trusted-publishing).

The [shared agent plugin](plugins/uographs/README.md), version **0.3.0**, supports
GitHub Copilot and Codex and requires a separately prepared **CLI 0.12.0 / v6**.
The NuGet 0.10.0 installation above does not satisfy this requirement. With the
compatible CLI available, install the plugin once through Copilot CLI:

```sh
copilot plugin marketplace add ugurozturk/UO.DotGraphs
copilot plugin install uographs@uographs
```

These commands require the Copilot catalog to be published to the remote repo.
VS Code supports discovering Copilot CLI-installed plugins. Invoke the skill as
`/uographs:uographs` in Copilot Chat. For local plugin loading, use VS Code user
`chat.pluginLocations`; see [Copilot installation](docs/COPILOT-SKILL.md).
**Live VS Code/Copilot acceptance remains NOT RUN.**

The existing [standalone Copilot skill](.github/skills/uographs/SKILL.md) remains
available for a single repository, with the same overwrite protections:

```sh
python3 scripts/install_skill.py '/path/to/authorized/repository'
```

The [Codex plugin](plugins/uographs/README.md) provides the same bounded CLI
workflow across your repositories. With `uographs` installed separately, add the
marketplace and plugin once for your Codex account:

```sh
codex plugin marketplace add ugurozturk/UO.DotGraphs
codex plugin add uographs@uographs
```

These GitHub commands require the marketplace files to be published to the remote
repository; local implementation alone does not establish remote availability.
The plugin contains the skill and CLI reference. It does not install the CLI or
create graph indexes. Each repository needs its own prepared index. Existing
standalone skills are preserved and may appear alongside the plugin skill.
Invoke the plugin skill as `$uographs:uographs` (verified with Codex CLI 0.162.1).
See [Codex installation and acceptance](docs/CODEX-SKILL.md) for local testing,
NuGet prerequisites, the existing Python installer, and verification boundaries.

## Documentation

| Topic | Reference |
| --- | --- |
| Product, architecture, acceptance | [PRODUCT](docs/PRODUCT.md), [ARCHITECTURE](docs/ARCHITECTURE.md), [ACCEPTANCE](docs/ACCEPTANCE.md) |
| Workspace, identity, semantic edges | [WORKSPACE](docs/WORKSPACE.md), [IDENTITY](docs/IDENTITY.md), [RELATIONSHIPS](docs/RELATIONSHIPS.md) |
| Queries, bounded delivery, freshness | [QUERIES](docs/QUERIES.md), [CONTEXT-EXPORT](docs/CONTEXT-EXPORT.md), [INCREMENTAL](docs/INCREMENTAL.md) |
| DI and ASP.NET Core | [FRAMEWORKS](docs/FRAMEWORKS.md) |
| Mediator and messaging | [MESSAGING](docs/MESSAGING.md) |
| ABP, EF Core, Refit | [ABP-EF-REFIT](docs/ABP-EF-REFIT.md) |
| Delivery, measurements, trust | [RELEASE](docs/RELEASE.md), [PERFORMANCE](docs/PERFORMANCE.md), [SECURITY](docs/SECURITY.md) |
| Actual results and license inventory | [STATUS](docs/STATUS.md), [DEPENDENCIES](docs/DEPENDENCIES.md), [notices](THIRD-PARTY-NOTICES.txt) |
| CLI 0.11 / JSON v6 migration | [MIGRATION-0.11](docs/MIGRATION-0.11.md) |

UO.Graphs is licensed under [MIT](LICENSE). Third-party components retain their
own licenses; see [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.txt).
