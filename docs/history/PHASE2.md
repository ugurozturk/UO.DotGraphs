# Phase 2 — Workspace contexts and portable symbols

Recorded **2026-10-08**. Historical macOS ARM64 acceptance; later phases had not
started. [Phase 1](PHASE1.md) preserves the earlier six-node, absolute-path identity
baseline. Current contracts/results are in [STATUS](../STATUS.md).

## Delivered scope

Real SDK-style sln/slnx/csproj and multi-project/multi-TFM fixtures; trust/SDK/assets
preparation; evaluated MSBuild paths; Locator-before-load/NoInlining and Roslyn
version checks. Configuration, conditional Compile/ProjectReference, selected/skipped
TFMs, and mismatched reference contexts are explicit.

Entities expanded to solution/project/assembly/namespace/type/method/constructor/
property/field/event/accessor/local function, including primary constructors and
partial source locations. Canonical identity v2 separates overload/arity/nesting/
operators/explicit implementations/file-local/lexical scope while surviving root
relocation and blank lines. [IDENTITY](../IDENTITY.md) and [WORKSPACE](../WORKSPACE.md)
describe the maintained contracts.

Diagnostics and partial publication preserve missing project/document/reference
and generator failures. Preparation/fatal failure retains the old snapshot.
Generators require allow-generators; analyzer references are removed by default.
The driver uses AdditionalFiles/config and does not execute diagnostic analyzers.
Generated include/exclude uses workspace documents, not obj/bin directory scanning.

Then-current product **0.2.0**, JSON **2**, SQLite **1**, identity **2**. Product
package versions stayed fixed; fixtures added CSharp 5.0.0. Fourteen locks covered
50 audited package/version entries.

## Recorded verification

macOS 27.0 ARM64, SDK 10.0.201, MSBuild 18.3.0, runtime 10.0.5, Roslyn 5.0.0.0.

| Command/check | Result |
| --- | --- |
| `dotnet --info` | PASS |
| `dotnet restore UO.Graphs.slnx --locked-mode -m:1 -nr:false` | PASS; eight projects, no NuGet warnings/errors |
| `dotnet restore samples/Phase2/Fixtures.slnx --locked-mode -m:1 -nr:false` | PASS; six projects, intentionally missing Missing.csproj reported |
| `dotnet build UO.Graphs.slnx --no-restore -t:Rebuild -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, 10.45 s, no warnings/errors |
| Same build without Rebuild, final sources | PASS, 10.02 s, no warnings/errors |
| `dotnet build src/UO.Graphs.Cli/UO.Graphs.Cli.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, 5.48 s |
| Generator.csproj rebuild with same no-restore/single-worker flags | PASS, 0.81 s; four unsuppressed warnings: 2×RS1041, 2×RS1036 |
| `dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false` | PASS **28/28**: 3 unit + 25 integration, zero skipped; integration 1m21s |
| `python3 scripts/audit_licenses.py --check` | PASS **50** packages |
| XML/whitespace | PASS, 22 XML files and 14 locks |

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --db .uographs/phase2-hello.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- query find Greet --format json --db .uographs/phase2-hello.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- status --db .uographs/phase2-hello.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/Phase2/Phase2.slnx --trust-project --framework net10.0 --allow-generators --db .uographs/phase2.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj -c Release --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --allow-generators --db .uographs/phase2-release-direct.db
```

All passed. HelloGraph: **15 nodes / 2 projects**, snapshot
`7d50697401dc4814a627a62aecf757fd`, partial due to skipped generators. Independent
query/status confirmed Greet and identity v2. The then-current Phase2 fixture had
50 nodes/2 projects and completeWithinDeclaredScope; later file-local/primary/
conditional additions were checked in the final suite. Release HelloGraph had 15
nodes/2 projects, completeWithinDeclaredScope, snapshot
`ee6852d229b14f0383ccfce07ed66f5a`, validating real Release Locator/JIT initialization.

## Test coverage and corrections

Tests compared identities across input formats, relocated roots, and blank-line
edits while checking changed source ranges/hashes. They covered full declaration
shapes, partial locations, Debug/Release and net10.0/netstandard2.1 separation,
conditional references/Compile, context mismatch, missing/unsupported projects,
and actual generator/analyzer marker behavior. Old persistence/trust/error/rollback
assertions were retained.

Missing Compile files silently skipped by Roslyn prompted explicit existence checks.
Fixture-local package settings fixed relocation. The generator became incremental;
a netstandard2.0 experiment's old non-permissive Microsoft.NETCore.Platforms package
was removed. Final netstandard2.1 fixture acceptance targets the .NET 10 host,
not portable VS/.NET Framework distribution. Interim NU1900/NU1004 restore failures
were separate from clean final locked restores.

A redirected Release Phase2.sln index with framework net10.0 and generators enabled
failed exit 5 with two BuildHost timeouts and no publication; direct Release smoke
passed separately. Redirection causality was not established.

Windows/Linux, physical offline, other SDK combinations, solution-specific
configuration/platform/RID mappings, and tool publication were NOT RUN. Mixed SDKs
were explicitly rejected. Lambdas/relationships were outside this phase. Git status
returned 128 because metadata was absent; Git diff was NOT RUN, file checks passed.
No commit/push/publish; AGENTS and user files preserved.

## Changed files

Updated version/README, Core GraphSnapshot, Roslyn preparation/indexer, CLI Program,
existing tests, audit/inventory/notices, STATUS/ARCHITECTURE. Added SymbolCollector,
Phase2Tests, independent Phase2 fixtures/locks, IDENTITY, WORKSPACE, and Phase1 history.
SQLite storage implementation was unchanged.

Suggested commit: `feat: implement phase 2 workspace contexts and portable symbol identities`
