# Graph queries — JSON v6

Queries read a local committed snapshot without MSBuild, restore, downloads, or
shell execution. All accept `--db <path>` (default `.uographs/graph.db`),
`--format json`, and the freshness flags described in [INCREMENTAL](INCREMENTAL.md).

| Command | Default meaning |
| --- | --- |
| `uographs query find <text>` | Literal substring of entity name/qualified name, ordered by ID |
| `uographs query symbol <symbol-id>` | One entity or external endpoint and declaration evidence |
| `uographs query callers <symbol-id>` | Incoming compile-time CALLS |
| `uographs query callees <symbol-id>` | Outgoing compile-time CALLS |
| `uographs query dependencies <symbol-id>` | Outgoing dependencies |
| `uographs query path <from-id> <to-id>` | One bounded shortest path in the selected direction |
| `uographs query impact <symbol-id>` | Potentially affected nodes through incoming dependencies |

## Symbol resolution

Except for `find`, arguments resolve first by exact ID, then case-sensitive exact
entity name or qualified name. One match is usable. Multiple matches return exit
**7**, `outcome: ambiguousSymbol`, and candidates in `items`; `--limit 1` does not
silently resolve ambiguity. Truncated candidates appear in `limits.reasons`.
No match returns exit **8** with `symbolNotFound`. Both path arguments follow this
rule. External endpoints resolve by exact ID and retain `entity: null`.

`find` uses SQLite ASCII case folding; `%`, `_`, and apostrophes are literal input,
not SQL patterns. Use `--` before positional text starting with `-`. SQL values
are parameterized; shell expansion before the CLI receives arguments still follows
the user's shell quoting rules.

## Options and bounds

| Option | Default / accepted range |
| --- | --- |
| `--direction outgoing\|incoming\|both` | Incoming for callers/impact; outgoing otherwise |
| `--edge-types CALLS,USES_TYPE,...` | CALLS for callers/callees; dependency set below for other traversals |
| `--depth N` | 1; 5 for path/impact; range 0..100 |
| `--limit N` | 100; range 1..10000 results/candidates |
| `--include-inferred` | Off; enabling it preserves inferred confidence |
| `--max-edges N` | 10000; range 1..1000000 examined adjacency rows |
| `--evidence-limit N` | 20 occurrences per relationship; range 1..1000 |

`find` and `symbol` accept limit, database, JSON format, and freshness options;
traversal options are invalid. For `symbol`, limit bounds ambiguous candidates.
Path returns at most one item: limit bounds candidate lists, not path length.
Depth bounds path length and max-edges bounds search work. Re-examining a relationship
from another endpoint consumes budget again; a SQL limit+1 sentinel is not counted.
This is not a bound on every SQLite physical page read.

The current dependency set in `QueryOptions.Dependencies` is:

- Semantic: CALLS, USES_TYPE, INHERITS, IMPLEMENTS, OVERRIDES, PROJECT_REFERENCES.
- DI/HTTP: HANDLED_BY, ACTIVATES, INJECTS, REGISTERS, IMPLEMENTED_BY, USES_FACTORY.
- Messaging: HANDLES, CONSUMES, DISPATCHES, PUBLISHES, POSSIBLE_TARGET,
  BEHAVIOR_FOR, POSSIBLE_BEHAVIOR.
- ABP/EF/Refit: APP_SERVICE_CONTRACT, MODULE_DEPENDS_ON, REPOSITORY_ENTITY,
  CONTEXT_ENTITY, CONFIGURES_ENTITY, MAPS_ENTITY, HTTP_CONTRACT, INVOKES_HTTP,
  POSSIBLE_ENDPOINT.

General traversals can explicitly select DECLARES and REFERENCES. Impact accepts
only incoming direction and subsets of the dependency set; other policies return
exit 2. It does not propagate arbitrary ownership/reference edges or automatically
expand a changed type to every member. It reports potential static impact within
scope, not every possible runtime effect. Inferred candidates require opt-in;
unresolved null targets are never traversed, even with `--include-inferred`.

Symbol output contains declaration evidence. Traversal output contains a selected
evidence path per reached node, not every possible relationship/path. Analysis
limitations remain visible regardless of traversal success.

## Traversal and deterministic order

Unweighted BFS uses a FIFO frontier and ordinal relationship-ID adjacency order.
Nodes are returned once in discovery order; shared diamond targets are not duplicated.
The root starts visited. Callers/callees/dependencies may return a direct self-edge
once at distance 1; impact excludes the root. Depth 0 returns no neighbors.
`path A A` is a found, zero-distance path with no steps.

Path returns the first shortest path in this deterministic order. Exhausting the
reachable graph without finding the target yields `notFound`; hitting depth/work
bounds yields `boundedSearchIncomplete`. Both are exit 0 and neither proves
runtime unreachability or absence in incomplete analysis.

A depth limit is reported when an eligible unvisited neighbor remains, not merely
when a cycle points to an already visited node. Result and edge limits use an extra
probe: exactly filling a genuinely exhausted result set is not truncation.
Occurrence evidence is ordered by ID with a limit+1 probe. Truncation sets both
step `evidenceTruncated` and envelope `evidenceLimit`.

## Versioned envelope

Success and domain selection errors produce one JSON document. Syntax/options,
missing database/old schema, SQLite/IO, and corrupt selected JSON errors leave
stdout empty and explain the error on stderr.

| Field | Meaning |
| --- | --- |
| `schemaVersion` | Query contract version **6** |
| `snapshotId` | Analysis ID of the snapshot read |
| `analysisContext` | Snapshot/input/root, versions, policy and related compact project contexts; full adds all project details |
| `completeness` | Analysis coverage/limitations, independent of query truncation |
| `stale` | Default `notChecked`; explicit checks can report fresh/stale/unknown |
| `truncated` | At least one search/candidate/result/evidence bound removed information |
| `items` | `{symbol, entity, distance, path}` |
| `diagnostics` | `{code,message}` query explanations only |
| `command`, `arguments`, `options` | Query and normalized effective options, including defaults |
| `limits` | Depth/limit/maxEdges/evidenceLimit, returned, examinedEdges, ordered reasons |
| `outcome` | ok, found, notFound, boundedSearchIncomplete, ambiguousSymbol, symbolNotFound |

`path[]` runs from root to result. Each step carries `fromId`, `toId`, `reversed`,
the original relationship, evidence, and evidenceTruncated. Reverse traversal does
not rewrite the relationship's source/target. Confidence, context/analysis IDs,
source ranges/hashes, reasons, bound targets, and candidates survive round trips.
Queries add no random IDs or timestamps; identical snapshot/options are deterministic.

JSON v3 find fields `results/relationships/occurrences` are historical. Current
consumers use `items[].entity` and traversal `items[].path[]`. Index/doctor retain `contractVersion: 4`; status uses `contractVersion: 6`.
Status `schemaVersion` means
SQLite schema **3**, not query schema. Freshness adds its own cost/limitation data.

## Bounded query and status output

Both commands default to `--details summary --max-chars 12000`. The range is
1..1,000,000 Unicode scalar values in the compact serialized JSON including its
final LF. `budget.actualChars` is the actual count; this is neither bytes nor tokens.
`--details summary|full` is valid only on query/status. Full has no character cap;
combining it with explicit `--max-chars` returns exit 2 and empty stdout.

`analysisDiagnostics` contains `total`, `samples` and `omitted`. Summary samples
are the first three messages in ordinal order, each at most 300 Unicode scalars;
`truncated` marks a shortened message. This order is not severity ranking. No
severity, diagnostic code or related method is inferred from plain text.
Analysis limitations occur once in `completeness.limitations`. In summary this
is a sample, as are dependency fingerprint limitations, freshness reasons,
preprocessor symbols and skipped TFMs: at most three ordinal strings, each at
most 300 scalars. `metadataSummary.fields` identifies each sampled list by JSON
Pointer `path`, with original `total`, `omitted` entries and `shortened` displayed
values. Project paths are also clipped at 300 scalars (total 1 when shortened).
Do not use a shortened project path as a filesystem path. Original values are
available with full details; returned entity source locations stay intact.

Summary project contexts are the first ten by ordinal ID, restricted for queries
to resolved endpoints, items and their evidence paths. They contain exact ID,
TFM/configuration, SDK/language, plus sampled preprocessor/skipped-TFM lists.
`totalProjects` and `omittedProjects` describe the entire indexed scope. Metadata
omissions are separate from coverage and graph truncation. Fingerprint `complete`,
granularity, snapshot/input/root/generator policy and freshness state remain exact.
Identical freshness/fingerprint explanation lists are stored once: an empty
`freshnessCheck.reasons` with `reasonsReference` points to the fingerprint list.
Consumers must follow this JSON Pointer in summary and full output.

When needed, the writer removes diagnostic samples, other metadata list samples,
then project contexts before pruning whole query items from the tail. Query
removal sets `truncated` and `limits.reasons: ["maxChars", ...]`; `limits.returned`
counts actual items. Metadata reduction alone never sets those graph flags.
At least one complete item remains for nonempty queries (including ambiguous
candidates), and a path remains intact. Ambiguity/outcome/exit codes never change
because candidates were removed. Source locations, hashes, confidence, reasons
and path steps are never sliced. Evidence-limit truncation is unchanged and
explicit. If mandatory scope metadata plus that item cannot fit, exit 2 leaves
stdout empty and stderr states the minimum required budget.

```sh
uographs status --details full --db 'DB'
uographs query callers 'SYMBOL_ID' --details full --db 'DB'
```

Full returns every original analysis diagnostic and every project metadata field
once. Traversal/result/evidence limits still apply. See [the v6 migration](MIGRATION-0.11.md)
for consumer changes; full does not restore the old v4 envelope.

## SQLite behavior

Schema 3 has endpoint nodes with nullable entity FKs, relationship endpoint FKs,
and occurrence relationship FKs. Exact/inferred edges require a target; unresolved
edges require null. Name/qualified-name, directional adjacency, and occurrence
indexes support bounded reads. Substring find may scan in SQL, but loads only
limit+1 matches into the application.

Each query uses a read-only connection and one read transaction, reading selected
entities/neighbors/evidence by parameter and limit. It need not materialize the
whole graph. Corrupt selected payloads or inconsistent selected evidence fail;
queries are not whole-database integrity scans and never repair the database.

Status and legacy storage reads support schemas 1/2. Current query commands return
exit 4 and require explicit re-indexing. Successful indexing atomically replaces
with schema 3; failure preserves the old schema and snapshot.

## Opt-in compact query contract (0.12)

`--view compact` selects compact v1; the default standard view remains v6.
See [AI output](AI-OUTPUT.md) for shared tables, explicit property filtering,
snapshot-bound one-hop relationship pages, unresolved observations, counts,
compatibility, and measured examples. The BFS/node-deduplication contract above
still governs standard output and compact multi-hop/find/symbol/path/impact.
Compact depth-1 callees/callers/dependencies return individual relationships and
can therefore expose multiple kinds or confidence classes for the same endpoint.
