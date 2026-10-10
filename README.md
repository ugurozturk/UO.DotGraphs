# UO.Graphs

UO.Graphs turns local SDK-style C# projects and solutions into a Roslyn-based code
knowledge graph in SQLite. Use the `uographs` CLI to find symbols, trace static
calls and dependencies, inspect potential change impact, and export bounded context.

Analysis stays local. The CLI requires no Copilot account, MCP server, hosted
service, API key, or telemetry. It never automatically restores or downloads
project dependencies. Indexing requires explicit permission to evaluate the
project with MSBuild through `--trust-project`.

Current version: **0.9.0**. Package: **UO.Graphs.Cli**. Query JSON: **4**;
SQLite schema: **3**; context/export format: **1**; declaration identity: **2**.
Linux CI and local macOS checks have passed; Windows, physical offline validation,
and real Copilot E2E remain open. See [verified status](docs/STATUS.md).

## Install

Version 0.9.0 was pushed to NuGet by the
[verified publishing workflow](https://github.com/ugurozturk/UO.DotGraphs/actions/runs/37997326843).
A .NET 10 runtime is required to run the tool. Indexing also requires the target's
selected SDK, reference packs, and previously restored assets.

```sh
dotnet tool install --global UO.Graphs.Cli --version 0.9.0 --source https://api.nuget.org/v3/index.json
uographs --help
uographs doctor
```

Update with `dotnet tool update --global UO.Graphs.Cli`. If the command is missing,
check the global tool directory in PATH: `$HOME/.dotnet/tools` on macOS/Linux or
`%USERPROFILE%\.dotnet\tools` on Windows. The version appears in `--help`;
there is no `--version` option.

For a repository-local installation, run `dotnet new tool-manifest` once if no
manifest exists, then:

```sh
dotnet tool install --local UO.Graphs.Cli --version 0.9.0 --source https://api.nuget.org/v3/index.json
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
leave stdout empty. Query JSON v4 contains `snapshotId`, `analysisContext`,
`completeness`, `stale`, `truncated`, `items`, `diagnostics`, effective options,
limits, and outcome. Index/status/doctor carry `contractVersion: 4`.
The status `schemaVersion` describes SQLite, currently 3.

Old SQLite schemas 1/2 can be inspected with status; current query/context/export
require explicit re-indexing. Successful indexing replaces them atomically with
schema 3. Unknown nonempty schemas are not overwritten. Old identity-v1 records
are not reinterpreted as identity v2. See the
[CLI envelope schema](docs/schemas/cli-envelope.schema.json).

| Exit | Meaning |
| --- | --- |
| 0 | Success, including partial snapshots and empty queries; inspect coverage |
| 2 | Invalid command/options/input/root, or insufficient context envelope budget |
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

## GitHub releases and Copilot skill

The [CI workflow](.github/workflows/ci.yml) restores locked dependencies, builds
and tests Release, audits licenses, packs, and installs the actual tool to test
index/query/context. A successful first-party `main` push triggers
[publishing](.github/workflows/publish-nuget.yml) of that verified artifact through
NuGet Trusted Publishing. PR and manually triggered CI runs do not publish.
Version comes directly from `<Version>` in `Directory.Build.props`; no run number
is appended. Existing NuGet versions are skipped. See
[release setup](docs/RELEASE.md#github-actions-and-nuget-trusted-publishing).

The [Copilot skill](.github/skills/uographs/SKILL.md) uses the CLI through the
terminal. Its [standalone template](templates/copilot/uographs/SKILL.md) can be
installed into an authorized repository without overwriting an existing skill:

```sh
python3 scripts/install_skill.py '/path/to/authorized/repository'
```

Review the destination and template first. The installer does not change terminal
permissions or bypass organizational restrictions. CLI tests and installed format
support have been checked; **real Copilot E2E remains NOT RUN**.
See [installation and manual acceptance](docs/COPILOT-SKILL.md).

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
