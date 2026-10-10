# Context and export contract

Current source: **0.12.0**; delivery `formatVersion: 1`, query/status contract 6, index/doctor 4,
SQLite schema 3. Each command reads one snapshot in a read-only transaction,
without MSBuild, restore, LLMs, embeddings, remote services, or renderers.
Legacy SQLite schemas 1/2 require explicit re-indexing (exit 4).

```sh
uographs context Greet --max-chars 12000 --format json
uographs context '<exact-symbol-id>' --include-source --max-chars 20000
uographs export --format json
uographs export --format ndjson --symbol '<exact-symbol-id>' --depth 2
uographs export --format mermaid --symbol Greet --depth 1
uographs export --format dot --symbol Greet --depth 1
```

All accept `--db <path>`, default `.uographs/graph.db`, and freshness options.
The tool is packaged; see [RELEASE](RELEASE.md). From a source build, use
`dotnet run --project src/UO.Graphs.Cli --no-build --no-restore --` as the prefix.

## Selection

Resolution prefers exact ID, then case-sensitive exact entity name/qualified name.
Multiple matches return `selectionRequired`, candidates, and exit 7, even with
limit 1. External endpoints require IDs and never become fake source entities.

If context has no exact match, it scans at most **10000 entities**, in ID order,
using .NET OrdinalIgnoreCase literal substring matching. Percent, underscore,
quotes, and shell text have no special meaning. This is not natural-language or
semantic search. Even one lexical candidate remains unselected: `match: lexical`,
`selected: null`, `selectionRequired`, exit 7. Repeat with the chosen ID. No
candidate yields `symbolNotFound`, exit 8. `searchScanLimit` means the remaining
snapshot was not searched and no absence claim covers it.

Export `--symbol` uses exact resolution only. Missing/ambiguous focus produces an
empty subgraph with outcome/candidate metadata in the selected format, exit 8/7.
Input length is 1..1024 UTF-16 code units. Use `--` for positional option-like input.
Follow-up suggestions are command/argument arrays, not executable shell strings;
templates are labeled and never run automatically.

## Bounds

| Option | Context default | Export default | Range |
| --- | --- | --- | --- |
| `--max-chars` | 12000 | Not accepted | 1..1000000 |
| `--max-nodes` | 40 | 1000 | 1..10000 |
| `--limit` | 80 relationships | 5000 relationships | 1..10000 |
| `--depth` | 1 | 1 for focused subgraph | 0..10 |
| `--max-edges` | 1000 | 10000 | 1..100000 |
| `--evidence-limit` | 2 occurrences/relationship | 20 occurrences/relationship | 1..100 |

The root counts toward max-nodes; candidates are bounded by min(limit,max-nodes).
Collection uses BFS in both directions, prioritizing CALLS, then inheritance/
implementation/overrides, type/project dependencies, and REFERENCES. DECLARES and all framework kinds
share the remaining priority; ties use ordinal relationship ID.
Cycles/diamonds do not duplicate nodes or edges. Inferred edges are included with
their confidence and can be traversed; unresolved records retain null targets and
are not traversed. These commands do not accept include-inferred, direction, or
edge-types; use [queries](QUERIES.md) for those policies.

A focused subgraph contains relationships collected while expanding nodes below
the depth bound. It is not guaranteed to contain every edge among boundary nodes.
Depth 0 keeps only the root. Omitted information is reported as depth, maxNodes,
limit/candidateLimit, maxEdges, or evidenceLimit. Max-edges counts inspected
adjacency rows, including repeated inspection from different endpoints but not
SQL limit+1 sentinels; it does not bound physical SQLite reads or output bytes.

Unfocused export first selects bounded nodes by ID, then bounded relationships by
priority/ID. Relationships whose endpoints are excluded are omitted with maxNodes
reported, so no dangling asserted targets are emitted. Depth without symbol is invalid.
Final serialization orders nodes, relationships, and occurrences by ordinal ID.

## Whole-output context budget

Context is one compact JSON document plus **one LF**. The budget counts Unicode
scalar values in the serialized stdout: keys, punctuation, escapes, metadata,
`actualChars`, and the final LF. It is neither UTF-8 bytes, UTF-16 Length, nor tokens.
A serialized `\uXXXX` counts as six characters. `budget` reports maxChars, unit,
and actualChars; the counter includes its own digit count until stable.

The writer removes whole records rather than cutting JSON strings. Snippet text,
distant nodes/relationships, trailing candidates, and suggestions can be dropped;
nearby flow/framework evidence is favored over generic type/reference/ownership
records at equal distance. Truncation and uncertainty remain visible. Snippet
status/hash/exclusion information survives text removal.

The mandatory envelope retains selection, snapshot/context, coverage/limitations,
bounds, uncertainty, counts, and snippet states. If it cannot fit, exit **2** leaves
stdout empty and reports the required size on stderr. That error takes precedence
over selection exit 7/8. No partial or invalid JSON is emitted.

| Field | Content |
| --- | --- |
| selected/candidates/nearby | Identity, name, kind, project, distance, confidence, source file/range/hash; no source text by default |
| relationships | Original IDs, direction, kind/confidence/context, occurrences, reasons, bound targets/candidates, evidence truncation |
| callSummary | Counts from the collected subgraph before character pruning, not repository totals; incomplete when collection/evidence is truncated |
| confidenceSummary | Inferred/unresolved counts and uncertainty warnings retained through pruning |
| analysisContext | Snapshot/version/source policy and relevant project SDK/TFM/configuration/language/preprocessor/skipped contexts; totalProjects covers the whole analysis |
| completeness | Analysis coverage/limitations, separate from query truncation |
| nextQueries | Exact-ID queries or explicit templates; no automatic runtime observation or indexing |

Context reports diagnostic counts instead of raw compiler diagnostic text; full
diagnostics remain in status/export. Default stale is notChecked. Explicit freshness
checks have their own cost and limitations, independent of snippet hash matching.

## Source and privacy policy

Only `context --include-source` reads source text: up to **3 locations**, each at
most **12 lines / 1200 Unicode scalar values**, starting at its declaration's first
line. Files must be at most **1 MiB**. Surrogate pairs are not split. Small symbols
or files can naturally fit in full. Export never opens source files.

Allowed files must be `.cs` inside the snapshot root. Hidden path components,
bin/obj/node_modules, and names containing secret, credential, password, token,
privatekey, private-key, appsettings, config, or .env (case-insensitively) are
excluded. Non-C# configuration/project files are outside the allowlist. Generated,
outside-root, and symlink/reparse-point paths, including root ancestors, are rejected.
This path policy is not a DLP scanner for arbitrary hardcoded secrets in ordinary
C# files; graph names and paths may also be sensitive.

One buffer is decoded with BOM handling, then hashed as decoded text → UTF-8 →
SHA-256, matching the source-evidence contract. A match yields snapshotMatched;
a mismatch yields snapshotMismatch with text null. Other no-text states include
missing, excludedByPolicy, outsideRoot, symlinkExcluded, generatedUnavailable,
rootUnavailable, fileTooLarge, invalidLocation, and unreadable.

A match proves only the buffer read at that moment, not global graph freshness or
filesystem isolation. [INCREMENTAL](INCREMENTAL.md) defines check-freshness and
require-fresh for all delivery formats. Each snippet carries an untrusted-source
data marker. Comments, labels, and diagnostics are data, never agent instructions.
The CLI sends nothing to a remote model; sharing output is a separate policy decision.

## Formats

- **JSON:** `{metadata, nodes, relationships}`. Metadata includes format version,
  snapshot, full analysis context, coverage/limitations, freshness, effective bounds,
  truncation, and scope. Nodes preserve RelationshipSymbol and optional GraphEntity;
  edges preserve GraphRelationship, occurrences, and evidenceTruncated.
- **NDJSON:** metadata record first, then nodes, then relationships; each line is
  independently valid compact JSON and preserves the same information as JSON.
- **Mermaid/DOT:** complete NDJSON records are retained in single-line `%%`/`//`
  comments. The diagram shows labeled directed edges; null unresolved targets are
  retained in comments without drawing invented target nodes.

Diagram aliases such as n00000 are deterministic; labels do not become IDs.
Mermaid special characters use numeric entities; DOT escapes quotes, backslashes,
and newlines. JSON comments escape newlines and preserve Unicode. No browser,
renderer, network access, or new dependency is required. Automated escaping/order/
comment-parse checks are recorded; external renderer visual acceptance is NOT RUN.

Even `scope: fullGraph` is bounded: inspect truncated/reasons. Export has record
bounds, not a whole-output byte/character budget. NDJSON describes the output format,
not a promise of unlimited internal streaming memory.

## Reproducible example

```sh
dotnet run --project src/UO.Graphs.Cli --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --allow-generators --db .uographs/phase5-smoke.db
python3 scripts/phase5_smoke.py --db .uographs/phase5-smoke.db
```

The script verifies untruncated full export counts against status and retained
Welcome → Greet evidence in context from the same snapshot. It measures complete
stdout characters/bytes and writes measurement.json and outputs under
`.uographs/phase5-evidence/`. Reduction is `100 × (1 − contextChars/fullGraphChars)`;
that fixture/task measurement does not imply repository-wide or token savings.
Historical values are in [Phase 5](history/PHASE5.md); current results are in
[STATUS](STATUS.md). Use [safe export](SECURITY.md#export-and-skill-installation)
when writing new files.

With 0.12, an explicit `--include-source` request protects verified snippet text
until optional nearby nodes and relationships have been removed. The source/hash
status always remains, even if the text ultimately cannot fit. Use a depth-0
context or authorized source read for a focused excerpt. This does not establish
global freshness or replace reading the complete method when needed.
