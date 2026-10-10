# Phase 4 — Bounded persistent graph queries

Recorded **2026-10-09**. Phase 4 passed the following macOS ARM64 query acceptance;
Phases 5–11 had not started. Prior evidence: [Phase 3](PHASE3.md). Current results:
[STATUS](../STATUS.md).

## Delivered scope

Find, symbol, callers, callees, dependencies, path, and impact read persistent
SQLite without MSBuild or runtime acquisition. Exact-ID/exact-name selection
preserves ambiguity (exit 7, including limit 1) and missing-symbol output (exit 8).
Direction, edge kinds, depth/results/work/evidence bounds, and inferred opt-in are
explicit. SQL is parameterized; arguments are not executed as shell commands.

Deterministic BFS handles cycles/self-calls/diamonds and gives one bounded shortest
path with original edge/source evidence. Impact then used incoming CALLS/USES_TYPE/
INHERITS/IMPLEMENTS/OVERRIDES/PROJECT_REFERENCES only, excluding DECLARES/REFERENCES.
Later framework extensions are documented in [QUERIES](../QUERIES.md).

Product **0.4.0**, query JSON **4**, SQLite **3**. The envelope separates analysis
coverage, notChecked freshness, truncation, options/limits/reasons, diagnostics,
and outcome. Endpoint/relationship/occurrence FKs and adjacency/evidence indexes
support bounded read-only transactions without loading the whole graph. Legacy
schema 1/2 status remains readable; current queries require re-indexing, with no
read-time migration. Atomic upgrade failure retains old data/schema. No new package.

## Recorded verification

macOS 27.0 ARM64, SDK 10.0.201, MSBuild 18.3.0, runtime 10.0.5, Roslyn 5.0.0.0.

| Command/check | Result |
| --- | --- |
| `dotnet --info` | PASS |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, 4.35 s, 0 warnings/errors |
| `dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false` | PASS **59/59**: 10 unit + 49 integration, zero skipped; 741 ms / 1m45s |
| `python3 scripts/audit_licenses.py --check` | PASS, 50 packages |
| Whitespace/XML/JSON/Python syntax/local links | PASS, 50 files |

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --allow-generators --db .uographs/phase4-smoke.db
python3 scripts/phase4_smoke.py --db .uographs/phase4-smoke.db
```

Both exit 0. Real graph: **15 / 20 / 23** entities/relationships/occurrences,
completeWithinDeclaredScope, snapshot `6b3ff3606a944e5ca0df7cb1217f0f85`.
The script made **13 separate CLI calls** using argv: status, two finds, symbol,
callers/callees/dependencies, repeated path, impact, reverse/depth-zero paths, and
missing symbol. The last returned expected exit 8; others exit 0. All JSON parsed.
Welcome → Greet CALLS and `new Greeter().Greet(name)` evidence were verified;
callers/impact returned only Welcome, repeated path matched, and DB SHA-256 was unchanged.
The script did not index or restore.

## Coverage and intermediate results

Tests covered complete caller/callee/impact sets, every direction, shortest/self/
unreachable paths, cycles/diamonds, depth/work/result/evidence bounds, 250-neighbor
limit+1 probes, inferred opt-in, unresolved non-traversal, external endpoints,
ambiguous names and both path arguments, invalid options, partial metadata,
byte-stable repeat, literal SQL/shell values, selected corrupt payloads, read-only
bytes, unrelated-row avoidance, FKs/rollback/upgrade, and EXPLAIN adjacency-index use.
The Phase3 evidence round-trip was moved to v4 traversal while retaining assertions.

An xUnit2013 collection-size warning stopped an interim build; Assert.Single fixed
it without weakening the assertion. A stale-binary test run started after that
failed build was cancelled and not counted. A subsequent 56/56 run passed; final
additional cases produced the recorded 59/59.

At this phase Windows/Linux, physical offline, installed-tool acceptance, Release
JIT, and large-solution performance/memory were NOT RUN. Incremental/freshness,
framework inference, all-path search, and export were not yet implemented. v3
consumers required adaptation to v4 items/path. Git metadata was absent (status
128), so Git diff was NOT RUN; file checks passed. AGENTS was preserved and no
commit/push/publish occurred.

## Changed files

Added Core GraphQuery and Storage GraphQueries; updated GraphSnapshot, GraphStore,
CLI Program, version/indexer, CliTests/Phase3Tests/StorageTests; added QueryTests
and phase4_smoke.py. Added QUERIES and Phase3 history; updated README/ARCHITECTURE/STATUS.

Suggested commit: `feat: implement bounded persistent graph queries with evidence paths`
