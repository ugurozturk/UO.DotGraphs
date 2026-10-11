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
uographs export --format mermaid --profile backend --symbol '<endpoint-or-method-id>' --depth 4 --output flow.mmd
```

All accept `--db <path>`, default `.uographs/graph.db`, and freshness options.
Export accepts `--profile graph|backend`; `graph` is the unchanged default.
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
Context and the default graph profile use BFS in both directions, prioritizing CALLS, then inheritance/
implementation/overrides, type/project dependencies, and REFERENCES. DECLARES and all framework kinds
share the remaining priority; ties use ordinal relationship ID.
Cycles/diamonds do not duplicate nodes or edges. Inferred edges are included with
their confidence and can be traversed; unresolved records retain null targets and
are not traversed. These commands do not accept include-inferred, direction, or
edge-types; use [queries](QUERIES.md) for those policies. Backend's fixed selection
policy is described below.

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

## Backend profile

`export --profile backend` requires an exact `--symbol` selection, with the same
ambiguity/candidate output and exits 7/8. It is available for JSON, NDJSON, Mermaid
and DOT. Only outgoing `HANDLED_BY`, `CALLS`, `DISPATCHES`, `PUBLISHES`,
`POSSIBLE_TARGET`, `POSSIBLE_BEHAVIOR`, `INVOKES_HTTP` and `POSSIBLE_ENDPOINT`
relationships are eligible. Inferred candidates are included. No extractor,
index update, schema migration, runtime dependency or execution order is added.

Defaults: **depth 4, max-nodes 40, limit 80, max-edges 1000, evidence-limit 2**.
The corresponding existing options override them within the ranges above. BFS,
cycle detection, CALLS-first/ordinal-ID selection, boundary-edge handling and
truncation reasons are preserved. Direction, relationship-kind and property filters
are applied in SQL before traversal budgets and LIMIT; excluded nodes/edges do not
consume those budgets. The examined count covers eligible adjacency rows, not
physical SQLite work. A selected root always counts as one node.

`--calls all|exclude-properties|only-properties` is accepted for backend export:

- `exclude-properties` (default) removes CALLS to Roslyn `PropertyGet`/`PropertySet`
  accessor declaration identities, including accessors with custom bodies.
- `all` retains all eligible relationship kinds and calls.
- `only-properties` keeps only accessor CALLS; other relationships and unresolved
  calls with no accessor identity are excluded. Start from the relevant method;
  an endpoint root cannot traverse HANDLED_BY under this filter.

No name matching or business-layer inference is used. Excluding property bodies
can hide meaningful work; inspect them separately when needed. Backend metadata
adds `selection` with the profile, outgoing kinds, call policy, inferred inclusion,
unresolved policy and ordering. The default graph/context envelopes are unchanged.

Backend Mermaid uses `flowchart LR`, rounded endpoint/HTTP-contract nodes,
hexagonal message nodes, rectangular method/other symbols and a distinct root
style. Shapes use stored entity metadata, not names. Exact links are solid and
inferred links dashed; both retain kind/confidence labels. Unresolved relationships
have no drawn target; their source label reports the number of **collected**
unresolved records. Full evidence and reasons remain in JSON comments. A note box
shows static-analysis limits, freshness, coverage, call filter and truncation. It
does not claim runtime dispatch, pipeline order or complete unresolved counts.
DOT uses the same selection/evidence with its existing directed-graph notation.

## File output

`--output <path>` is available for every export format/profile. The parent directory
must already exist. Existing files, directories and symlinks (including dangling
links) are refused with exit 6; there is no overwrite switch. Output is UTF-8
without a BOM, byte-identical to stdout for the same successful command.

The CLI finishes selection/read/serialization before creating a temporary sibling,
flushes it and publishes without replacing an existing destination. Success leaves
stdout empty and writes a short confirmation to stderr. Selection errors retain
their candidate/outcome payload on stdout and exits 7/8, creating no file. Read or
freshness errors likewise create no output file. Filesystem failures return exit 6
and clean up the temporary file. Without `--output`, stdout behavior is unchanged.
Shell redirection has its own overwrite semantics and is not protected by this
option. No renderer or PNG/SVG output is included.

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
