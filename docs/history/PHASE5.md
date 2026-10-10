# Phase 5 — Bounded context and graph export

Recorded **2026-10-09**. Phase 5 passed its macOS ARM64 delivery acceptance;
Phases 6–11 had not started. Prior evidence: [Phase 4](PHASE4.md); current status:
[STATUS](../STATUS.md). Existing workspace/trust/identity/query contracts and AGENTS
were preserved.

## Delivered scope

Context resolves exact IDs/names, then bounded literal lexical candidates over at
most 10000 entities. Even one lexical candidate requires selection (exit 7); missing
symbols return exit 8. It includes nearby edges, caller/callee and confidence
summaries, source locations, limitations, and suggested queries.

Whole-stdout Unicode-scalar budgeting includes JSON escapes, the counter, and final
LF. Whole-record pruning retains valid JSON, selection, uncertainty, and truncation;
minimum-envelope failure returns empty stdout/exit 2. No token-count claim.

Opt-in source text allows at most three C# snippets, 12 lines/1200 characters each,
1 MiB/file, with secret/config/generated/outside-root/symlink exclusions and decoded
hash matching. Mismatch/missing/exclusion remains explicit; source is untrusted data.
Export supports JSON/NDJSON/Mermaid/DOT, focused subgraphs, bounds, deterministic
aliases/escaping, and full evidence/context in diagram JSON comments. No invented
unresolved target or unrestricted full-graph promise.

Product **0.5.0**, delivery **1**, query/index JSON **4**, SQLite **3**. No new layer,
package, LLM, remote service, or renderer. Read-only transactions use bounded SQL.
Current details: [CONTEXT-EXPORT](../CONTEXT-EXPORT.md).

## Recorded verification

macOS 27.0 ARM64, SDK 10.0.201, MSBuild 18.3.0, runtime 10.0.5, Roslyn 5.0.0.0.

| Command/check | Result |
| --- | --- |
| `dotnet --info` | PASS |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, 5.30 s, 0 warnings/errors |
| `dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false` | PASS **79/79**: 10 unit + 69 integration, zero skipped; 819 ms / 2m5s |
| `python3 scripts/audit_licenses.py --check` | PASS, 50 packages |
| Whitespace/XML/JSON/Python/local links | PASS, 96 files |

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --allow-generators --db .uographs/phase5-smoke.db
python3 scripts/phase5_smoke.py --db .uographs/phase5-smoke.db
```

Both exit 0. Snapshot `20bc3d8ec952431a9834fc705d0cf993`, **15 / 20 / 23**,
completeWithinDeclaredScope. The script made **17 real CLI calls**; expected
negative exits 2/7/8 were separate from successes. It did no runtime acquisition or
indexing. Database SHA-256 was unchanged by reads.

## Measured context reduction

Task: identify Greet's static caller and source evidence. Full export and context
used the same snapshot and retained the same Welcome → Greet edge, occurrence ID,
source range/hash, and reason. Full export was untruncated and counts matched status.

| Measurement | Full JSON graph | Context at max-chars 12000 |
| --- | ---: | ---: |
| Entire stdout characters, including LF | 61,420 | 9,547 |
| UTF-8 bytes | 61,420 | 9,547 |

Reduction: `100 × (1 − 9547/61420)` = **84.46%**, only for this fixture/task/output,
not general repository or token savings. A 6500 budget produced 5531 characters,
valid JSON and maxChars truncation. Budget 1 returned exit 2/empty stdout.
Opt-in matched/untrusted source produced 10,038 characters. Evidence:
`.uographs/phase5-evidence/measurement.json`, full-graph.json, context.json, and four
greet-subgraph files. The directory is ignored.

## Coverage and limits

Twenty new integration cases (7 Fact + 13 negative option Theory) retained all 59
previous cases. Tests covered budgets 1/2000/3500/5000/8000/12000/50000, exact boundary
and counter, selection/ambiguity, cycle/diamond and all bounds, confidence summaries,
four-format parse/escaping/Unicode/ordering/DB preservation, default source omission,
real source mismatch, path/privacy/large-file exclusions, 1200-emoji boundaries,
10000-entity scan limit, corrupt selected/unrelated rows, old schema refusal, and
invalid options before DB access. Initial focused 18/18 preceded final 79/79.
No build/test/smoke failure; no weakened assertions. A newline check encountered
existing lock files without final LF; they were preserved and JSON parsing retained.

Windows/Linux, physical offline, installed package, Release JIT, large-solution
performance/memory, and external Mermaid/DOT visual rendering were NOT RUN.
Snippet hash matching was not global freshness (then notChecked). The path policy
was not a hardcoded-secret scanner; NDJSON was not unlimited streaming. Git metadata
was absent (status 128), so diff was NOT RUN; file checks passed. No commit/push/publish.

## Changed files

Added Core GraphDelivery/DeliveryWriter/SourceSnippets, Storage GraphDelivery,
Phase5Tests, phase5_smoke.py, CONTEXT-EXPORT and Phase4 history. Updated CLI,
version/indexer, README, PRODUCT, ARCHITECTURE, and STATUS.

Suggested commit: `feat: add bounded AI context and deterministic graph exports`
