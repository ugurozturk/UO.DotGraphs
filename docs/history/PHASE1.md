# Phase 1 — Local Roslyn-to-SQLite skeleton

Recorded **2026-10-08**. This is historical evidence for Phase 1, not the current
feature/version matrix. See [STATUS](../STATUS.md) for later results.

Phase 1 passed its narrow macOS ARM64 end-to-end acceptance. Phases 2–11 had not
started. There was no application or Git repository initially; Git was not
initialized, and existing .DS_Store/user files and AGENTS.md were preserved.

## Delivered scope

- Core snapshot/project/entity/source/confidence contracts.
- Trust gate → installed SDK/existing assets → real MSBuildWorkspace → project,
  type, and method declarations, including project-reference loading without restore.
- SQLite schema 1, one snapshot, transactional replacement/rollback and read-only
  query/status. Failed analysis preserved the previous snapshot.
- CLI help, doctor, index with trust, status, literal find, JSON format, and db option.
  JSON stdout and progress/error stderr; the then-current contract was JSON v1.
- SDK 10.0.201, C# 14, nullable, central versions, eight locks, editorconfig,
  two test projects, and the real two-project HelloGraph sample.
- MIT product license and evidence for 42 locked direct/transitive packages,
  native SQLite, and upstream notices. Build/SQLite/evidence outputs were ignored.

## Recorded verification

macOS **27.0 ARM64/osx-arm64**, SDK **10.0.201**, MSBuild **18.3.0**, runtime **10.0.5**.
CLI invocations used the source project through dotnet run, not an installed tool.

| Command/check | Result |
| --- | --- |
| `dotnet --info` | PASS exit 0; versions above |
| `dotnet restore UO.Graphs.slnx --locked-mode -m:1 -nr:false` | PASS; eight projects, unchanged locks, no NuGet warnings |
| `dotnet build UO.Graphs.slnx --no-restore -t:Rebuild -m:1 -nr:false -p:UseSharedCompilation=false` | PASS; 0 warnings/errors, 9.91 s |
| `dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false` | PASS **18/18**: 2 unit + 16 integration, zero skipped; integration about 13 s |
| `python3 scripts/audit_licenses.py --check` | PASS; 42 packages, MIT/Apache-2.0/verified SQLite public domain |
| XML/newline/whitespace checks | PASS; eight csproj/eight locks and central configuration |

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --db .uographs/smoke.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- query find Greeter --format json --db .uographs/smoke.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- query find Greet --format json --db .uographs/smoke.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- status --db .uographs/smoke.db
```

All passed with **6 entities** (two projects/types/methods), snapshot
`92b5d53237b74d22854c55495ee3c1e0`. Separate processes found the expected type and
Greet(string) documentation IDs and the same snapshot. Independent assertions
checked SHA-256 IDs, project ownership, source hashes/ranges, names, and confidence.
These old IDs depended on absolute project paths:

- Greeter: `4b75c2e8d37286a64a734e8a2c995c83c6e56ebcbcf37b3d40bcf8a3d915d08b`
- Greet(string): `3674fe5c1a3a67d7834e5ab3f74567b7a21358e1a1448eaaba49bf912ee3fabe`

Help/doctor, csproj with references, and default DB were checked in real processes.
Missing trust/invalid arguments/missing assets/missing SDK returned 3/2/4/4 with
empty stdout and no created assets/DB. Duplicate-PK SQLite code 19 after deletion/
partial insertion rolled back to the old snapshot. Failed re-index preserved it.

## Failures and boundaries

NU1903 on SQLitePCLRaw.lib.e_sqlite3 2.1.11 was resolved by pinned bundle 3.0.2 and
SourceGear.sqlite3 3.50.4.5, not suppression. MSBL001 was resolved by excluding
Microsoft.Build.Framework runtime assets and making them private. Parallel restore
stalled in the sandbox; single-worker/no-reuse restore/rebuild passed.

Python/shell-wrapped index attempts timed out in BuildHost (exit 5), while direct
CLI and the full suite passed. No Python smoke wrapper was delivered. A separate
filtered RealSolutionIndexPersistsTypeAndMethodAcrossProcesses test invocation
aborted before execution with VSTest SocketException (13), exit 1; that same test
passed inside the recorded full suite. Permissions/assertions were not bypassed.

At this phase, Windows/Linux, physical offline, broad real-world/multi-TFM/config/
generator matrices, relationships, freshness, packaging, and publication were
**NOT RUN** or outside implemented scope. Freshness was always notChecked and
coverage meant only Phase 1. Git diff was NOT RUN because no Git metadata existed;
file-level checks were used instead. No commit/push/publish occurred in this run.

## Changed files

Added solution, SDK/central settings, NuGet.Config, editorconfig/gitignore, LICENSE,
README, four product projects, two test projects, HelloGraph projects/sources/locks,
audit_licenses.py, DEPENDENCIES, and THIRD-PARTY-NOTICES. Updated STATUS and ARCHITECTURE.

Suggested commit: `feat: implement phase 1 local Roslyn to SQLite CLI pipeline`
