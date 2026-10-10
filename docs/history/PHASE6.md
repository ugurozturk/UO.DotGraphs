# Phase 6 — Incremental indexing, freshness, and atomic snapshots

Recorded **2026-10-09**. Phase 6 passed the following macOS ARM64 acceptance;
Phases 7–11 had not started. Product **0.6.0**, CLI uographs; JSON 4 / delivery 1 /
SQLite 3, now with optional fingerprint data. [Phase 5](PHASE5.md) is prior evidence;
[STATUS](../STATUS.md) contains later SDK-fixture corrections and platform results.

## Delivered scope

Incremental defaults on; full forces rebuilding. Both require trust before MSBuild,
reject combined modes, and never restore/download. Raw SHA-256 plus inventory and
evaluated/preprocessed context tracks sources/projects/imports/ancestor config,
assets/locks, SDK/compiler/tool/analyzer/package contents, options, and implicit
MSBuild user/extensions/CustomBefore/After search locations, including absent inputs.
Timestamps do not establish reuse.

Granularity is **entireSelectedProjectClosure**. Equal complete fingerprints allow
no-op; changes rebind the entire scope and replace obsolete entities/edges/occurrences.
Custom behavior outside base SDK 10.0.201's validated model and generator execution
disable reuse with explicit limitations. Unreadable/symlink scans fail closed.
Pre-publication evaluation/content/consumed-source checks use the invocation's
initial environment, avoiding Locator-induced differences.

Canonical DB paths and OS writer lease span analysis to commit; contention exit 10.
SQLite WAL/FULL synchronous transaction publishes graph/manifest together. Readers
see one generation; failures/cancellation/process death preserve the last commit.
Default reads have notChecked and zero hashing; check-freshness reads filesystem/
environment only, require-fresh rejects stale/unknown with empty stdout/exit 9.
Costs and incomplete counting remain visible. No new dependency/layer/framework
extractor. Details: [INCREMENTAL](../INCREMENTAL.md).

## Recorded verification

macOS 27.0 ARM64, SDK 10.0.201, MSBuild 18.3.0, runtime 10.0.5, Roslyn 5.0.0.0.

| Command/check | Result |
| --- | --- |
| `dotnet --info` | PASS |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, 7.76 s, 0 warnings/errors |
| `dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false --logger 'console;verbosity=normal'` | PASS **91/91**: 11 unit + 80 integration, zero skipped; 1.04 s / 10m19s |
| Focused NewlyCreatedImplicitUserImport/FreshnessChecks tests | PASS **2/2**, 49.69 s |
| `python3 scripts/audit_licenses.py --check` | PASS, 50 packages |
| Whitespace/XML/JSON/local links | PASS, 57 files |

```sh
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~NewlyCreatedImplicitUserImport|FullyQualifiedName~FreshnessChecks' --logger 'console;verbosity=normal'
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --full --trust-project --db .uographs/phase6-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --incremental --trust-project --db .uographs/phase6-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- status --db .uographs/phase6-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- status --require-fresh --db .uographs/phase6-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- query callers Greet --require-fresh --db .uographs/phase6-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- context Greet --require-fresh --max-chars 12000 --db .uographs/phase6-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- export --format json --symbol Greet --require-fresh --db .uographs/phase6-final.db
```

All exit 0. Snapshot `d04f1120b4d547499c1d36aadeba1de8`, **15 / 20 / 23**,
unchanged across full/no-op. Coverage remained partial due to skipped generators.

| Measurement | Full | No-op |
| --- | ---: | ---: |
| Projects analyzed | 2 | 0 |
| Fingerprint ms | 3506 | 3284 |
| Analysis/pre-publication ms | 5874 | 0 |
| Hashed files | 13394 (two scans) | 6697 |
| Hashed bytes | 1681017320 | 840508660 |
| Outcome | rebuiltAllProjects | noOp |

No-op had no workspace/symbol progress or replacement, but still read about 840 MB
and evaluated/preprocessed MSBuild. Default status had zero hash I/O. Required-fresh
status/callers/context/export reported fresh and 6697 files/840508660 bytes in
1431/1429/1424/1207 ms. The first three ran concurrently, not a single-user latency
comparison. Read-only DB SHA-256 stayed
`087d7b8bb61b9d9b344b595f781b7a5f4d4858ccb589f8a289f943cef8e2c139`.
JSON evidence resides in `.uographs/phase6-evidence/`.

## Coverage and intermediate results

Eleven integration and one unit case retained the previous 79. The full/incremental
oracle compared all semantic records/contexts across **15 mutation scenarios**,
normalizing only analysis IDs/timestamps, including unchanged-consumer rebinding,
removed APIs, same-mtime edits, global usings, moves/deletions, config/references,
assets/locks/imports, and framework context. Other tests covered unknown/no-op
refusal, newly created external user imports, freshness costs/errors, 30000-entity
concurrent publication, real process contention/kill after WAL writes, cancellation,
inference record cleanup, interrupted first publication, and symlink rejection.

Initial focused 9/9 and full 89/89 passed. After implicit-import tracking, an interim
run had **20/80 integration failures**, with 11/11 unit passing: Locator environment
mutation changed the second evaluation. Fail-closed publication protected snapshots.
Fixing subprocess environment resolved it without weakening assertions; focused,
smoke, and final 91/91 results above are separate. Two sandbox wrapper index attempts
failed BuildHost timeout/exit 5 without publication; direct commands passed.

Windows/Linux, physical offline, installed tool, Release JIT, large-scale budgets,
network filesystems/hardlink aliases, physical disk/power-loss acceptance were NOT
RUN. Process kill/cancellation were executed. Filesystem checks are not atomic
source snapshots; stable inputs are required. Git metadata was absent (status 128),
so diff was NOT RUN and file checks were used. No commit/push/publish; AGENTS preserved.

## Changed files

Added Core IndexFingerprint, Roslyn IncrementalIndexer, Phase6Tests/FingerprintTests,
INCREMENTAL, and Phase5 history. Updated snapshot/delivery, preparation/indexer,
storage/query/delivery, CLI/version, README/PRODUCT/ARCHITECTURE/QUERIES/CONTEXT-EXPORT/STATUS.

Suggested commit: `feat: add conservative incremental indexing and atomic snapshots`
