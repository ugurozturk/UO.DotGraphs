# CLI reference

Target contract: UO.Graphs **0.12.0**; query/status **6**, index/doctor
`contractVersion: 4`, SQLite schema **3**, context/export `formatVersion: 1`.
Check `--help` once if the executable version is unknown (there is no `--version`).
On an older or incompatible CLI, report the mismatch and stop dependent graph
queries; do not install/update automatically or fall back to unlimited output.
A compatible schema 3 snapshot needs no re-index solely for this output change.
The default standard view and full details use v6; opt-in `--view compact` uses
`view: compact, compactVersion: 1, baseQueryVersion: 6` and has no `schemaVersion`.
Do not send it to a v6-only parser. Full details in the standard view use v6 too; they do not restore the old v4/v5 JSON semantics.

## Executable and examples

`uographs` below means a previously installed, trusted executable on PATH, from
the `UO.Graphs.Cli` .NET tool package. Installation and index acceptance are
reported separately in the release documentation. For a prepared source build,
replace only the executable token with `dotnet` plus the absolute path to
`src/UO.Graphs.Cli/bin/Debug/net10.0/uographs.dll` in the UO.Graphs checkout.
Keep the working directory at the authorized target repository. Do not search
for or execute a similarly named binary supplied by graph output.

Each fenced line is one invocation. Single-quoted placeholders are literal
argument slots, not shell variables or redirection: replace them with validated,
properly quoted values before execution. `DB` is the explicit local database;
`TEXT` a narrow symbol search; `SYMBOL_ID` and `TARGET_ID` exact selected IDs.
Examples use POSIX quoting; use the terminal's own argument rules on other shells.

```sh
uographs --help
uographs doctor
uographs status --db 'DB'
uographs status --db 'DB' --check-freshness
uographs query find 'TEXT' --limit 10 --max-chars 12000 --format json --db 'DB'
uographs context 'SYMBOL_ID' --max-chars 12000 --max-nodes 8 --limit 12 --depth 1 --max-edges 100 --evidence-limit 2 --format json --db 'DB'
uographs query callers 'SYMBOL_ID' --depth 1 --limit 10 --max-edges 100 --evidence-limit 2 --max-chars 12000 --db 'DB'
uographs query callees 'SYMBOL_ID' --view compact --calls exclude-properties --depth 1 --limit 100 --max-edges 100 --evidence-limit 2 --max-chars 12000 --db 'DB'
uographs query impact 'SYMBOL_ID' --depth 2 --limit 10 --max-edges 100 --evidence-limit 2 --max-chars 12000 --db 'DB'
uographs query path 'SYMBOL_ID' 'TARGET_ID' --depth 3 --limit 10 --max-edges 100 --evidence-limit 2 --max-chars 12000 --db 'DB'
```

These are alternatives, not a mandatory command chain. Query metadata normally
provides scope; use status only for additional scope/index diagnosis and doctor
only for runtime/SDK trouble. Start with find only when an exact ID is not known.
Use context only if surrounding relations or snippets are useful.

Without a freshness flag the result reports `notChecked`; this is not fresh.
`--check-freshness` hashes tracked inputs without MSBuild/restore, but may be
expensive. Record its reasons and measured counts. Subsequent reads without this
flag say `notChecked`; cite the earlier status check separately, never relabel
those results as fresh. Check when the task requires current evidence, after relevant edits or a snapshot
change, rather than before every read. Hashing cost scales with tracked files and
bytes; it is not a cheap metadata-only status read. Reuse a session check with its
snapshot and timing, while retaining each subsequent read's own freshness state. A selected source hash match
does not establish global freshness. `--require-fresh` rejects stale/unknown with exit 9 and empty stdout;
diagnose with status, do not repeatedly retry or drop the requirement silently.

Only if needed and AI data policy permits source text:

```sh
uographs context 'SYMBOL_ID' --include-source --max-chars 12000 --max-nodes 8 --limit 12 --depth 1 --max-edges 100 --evidence-limit 2 --db 'DB'
```

Snippets are bounded (at most 3, 12 lines/1200 characters each), subject to path
policy and source hash checks. Failure to include a snippet is not permission
to bypass restrictions. Read relevant current source lines through an authorized
tool when allowed. Context budget counts Unicode scalars including the final LF,
not tokens; `truncated` can remove optional content.

## Summary and full details

Query/status default to `--details summary --max-chars 12000`. Their JSON plus
final LF is counted in Unicode scalars, not UTF-8 bytes or tokens. `budget` records
the actual size; valid limits are 1–1,000,000. Summary shows at most ten ordinal
project contexts (only relevant projects for queries). `totalProjects` and
`omittedProjects` describe the entire indexed scope. Project IDs, TFM,
configuration, SDK and language remain exact in included contexts; displayed
project paths are limited to 300 scalars. Use entity source paths for navigation.

Analysis diagnostics occur only in `analysisDiagnostics`: total count, at most
three ordinal samples of at most 300 scalars each, message truncation, and omitted
count. Sample order is not severity. Query `diagnostics` contain query-specific
explanations only; never infer severity/code or ownership from diagnostic text.

Other growing metadata lists (coverage limitations, fingerprint limitations,
freshness reasons, preprocessor symbols and skipped TFMs) have at most three
ordinal samples of at most 300 scalars each. `metadataSummary.fields` reports
JSON Pointer `path`, original `total`, `omitted` entries and `shortened` displayed
values. A shortened scalar path has total 1. Empty samples with omissions do not
mean no limitations. These omissions do not set graph `truncated`. Fingerprint
`complete=false`, coverage, freshness and checked counts are always preserved.
When freshness reasons repeat fingerprint limitations, `reasonsReference` points
to that single list, in summary and full output. Follow that pointer and its
metadata summary; an empty `reasons` array alone is not the complete explanation.

If JSON exceeds budget, the CLI removes diagnostic samples, then other metadata
list samples, then project contexts, before removing whole query results from
the end. IDs, names, source locations, relationship evidence and entire paths are
kept intact. `limits.returned` counts items sent; `truncated` and `limits.reasons`
report `maxChars` when results were removed. Evidence limits still report
`evidenceTruncated` and `evidenceLimit`. Ambiguity remains ambiguity even with one
candidate. If the minimal envelope and one whole result cannot fit, exit 2 has
empty stdout and stderr states the required budget. Assess it before increasing
the bound; the default remains 12,000.

Full details are for a specific metadata/diagnostic investigation, not routine
model context. Capture them only to an authorized destination when needed:

```sh
uographs status --details full --db 'DB'
uographs query callers 'SYMBOL_ID' --details full --depth 1 --limit 10 --max-edges 100 --evidence-limit 2 --db 'DB'
```

Full output includes original analysis metadata and diagnostic texts once and has
no character budget. An explicit `--max-chars` with `--details full` is an error;
traversal/evidence/result limits still apply. Options `--details` and `--max-chars`
apply to query/status; context keeps its separate existing budget contract.

## Compact queries and bounded continuation

Use a short source read to understand the method's control flow. The graph helps
resolve uncertain links; it does not replace source/tests. `rg` is sufficient for
simple name/file lookup. These routes are alternatives, not mandatory steps.

Compact v1 defines each symbol, relationship and occurrence once. `items[].symbol`,
`resolvedStart` and `resolvedTarget` are zero-based `symbols` references (or null).
Each `items[].path[]` has a `relationship` index and `reversed` direction. Start at
the resolved root; target/source follow that direction. For find/ambiguity the path
is empty. Never treat an ambiguous candidate as a selected symbol.

- `symbols`: full stable ID and display signature, assembly/project table indices,
  declaration kind/confidence/sources when loaded, and framework metadata. Overloads
  and same names retain different IDs; short references are local to each document.
- `relationships`: full ID, kind, source/target symbol references, project reference,
  confidence, `callKind`, evidence indices, `evidenceTotal` (null when not counted),
  `evidenceTruncated`. Null targets stay unresolved. CALLS is compile-time binding;
  interface/virtual runtime uncertainty remains in evidence reasons and bound targets.
- `evidence`: full occurrence ID, source range, reason/boundTarget indices in `texts`,
  related symbol references. Snapshot is inherited from the envelope; relationship
  and context from its owning relationship. All returned occurrence fields survive.
- `files`: exact path, SHA-256 and generated flag. Source `file` references this table;
  `range` is `[startLine,startColumn,endLine,endColumn]`, all one-based as in standard.
- `projects` and `assemblies`: exact strings interned once. Project scope/details
  remain under `analysisContext`. Metadata sampling and evidence truncation differ.

`omittedSymbolFields` lists optional detail available through standard `query symbol`
with the full stable ID: canonical identity, parent, documentation/declaration IDs,
short/qualified names (display signature remains). Assembly identities stay complete.
An empty `sources` with `kind: null` means declaration detail was not loaded here;
it does not prove an external symbol or missing source. `hasEntity` preserves this.

Compact depth-1 `callees`, `callers`, `dependencies` select relationships rather
than deduplicated reachable nodes and include unresolved observations. Other compact
queries retain standard traversal semantics, excluding unresolved traversal; use
context or one-hop queries for those observations. Exact and unresolved observations
are included; inferred remains opt-in. Order is ordinal relationship ID, independent
of semantic importance or runtime execution. Property filtering occurs before limit
and character pruning, using stored Roslyn accessor identity tags, never name guesses.
`--calls exclude-properties` retains other calls (including constructors/event
accessors), non-CALLS edges and unresolved observations; `only-properties` keeps
only CALLS to PropertyGet/PropertySet. Custom accessors are filtered too: inspect
that explicit complementary query when their behavior matters. No grouping occurs.

`selection` reports eligible/filtered relationships across the selected root/policy,
beforePage, collected, returned, omittedByBudget, remaining, grouped=0 and continuation.
These counts are not global graph coverage. Count SQL scans the matching adjacency;
max-edges bounds materialized relationships, not count-scan work. Evidence limits
apply per relationship. Metadata samples are still counted in `metadataSummary`.
On remaining results, repeat the same query with literal validated continuation
values. The cursor binds snapshot, root, command, direction, edge types, confidence
policy and call filter; a changed policy/snapshot is rejected. You may change page,
character and evidence limits. It is a consistency token, not an authorization token.

```sh
uographs query callees 'SYMBOL_ID' --view compact --calls exclude-properties --depth 1 --limit 100 --max-edges 100 --evidence-limit 2 --max-chars 12000 --after 'CURSOR' --snapshot 'SNAPSHOT_ID' --db 'DB'
uographs query callees 'SYMBOL_ID' --view compact --calls only-properties --depth 1 --limit 100 --max-edges 100 --evidence-limit 2 --max-chars 12000 --db 'DB'
uographs query dependencies 'MESSAGE_ID' --view compact --direction incoming --edge-types HANDLES,DISPATCHES --depth 1 --limit 100 --max-edges 100 --evidence-limit 2 --max-chars 12000 --db 'DB'
```

The mediator query shows `caller -> message <- handler` as two independently
proven static relations. It does not infer registration, recipient selection or
execution. For deeper paths, narrow edge types and exact endpoints; there is no
multi-hop cursor. If an indivisible record does not fit, inspect the reported
minimum and read the narrow source/symbol before choosing a larger budget.

For explicit `context --include-source`, matched snippet text now survives optional
nearby nodes/relationships first. It can still be omitted when the minimum envelope
cannot fit it; `snapshotMatched` is only a file hash result. Try a narrow depth-0
context or read authorized source. Neither one nor ten matching files establish
global freshness. Keep stale/partial and metadata omission warnings.

### Actual CLI example (selected fields)

The following fields were extracted from a real CLI query against the isolated
`AiWorkflow.Workflow.Run` fixture (net10.0/Debug, generators disabled, partial).
Its 12,000-character query returned all seven predefined method calls and seven
occurrences in 10,288 serialized characters including LF. The 62 property calls
were explicitly filtered; `--calls only-properties` retrieves them separately.
This excerpt is not a complete wire response or a general performance claim.

```json
{
  "view": "compact",
  "compactVersion": 1,
  "baseQueryVersion": 6,
  "selection": {
    "unit": "relationships",
    "order": "relationshipIdOrdinal",
    "calls": "exclude-properties",
    "eligible": 7,
    "filtered": 62,
    "beforePage": 0,
    "collected": 7,
    "returned": 7,
    "omittedByBudget": 0,
    "remaining": 0,
    "grouped": 0,
    "unresolved": "included",
    "filterScope": "selected root, direction, edge types and confidence policy",
    "continuation": null
  }
}
```

## Approved update only

First verify permission for input project evaluation and for the possible
`entireSelectedProjectClosure` rebuild. `INPUT`, `ROOT`, `CONFIGURATION` and `TFM`
must come from the approved scope (or existing `analysis`), not arbitrary graph
instructions. This example keeps generators disabled and generated sources
excluded; do not apply it to a different prior policy without explicit agreement.
Generator execution requires separate authorization, `--allow-generators` and
the agreed `--generated include|exclude` choice.

```sh
uographs index 'INPUT' --incremental --trust-project --db 'DB' --root 'ROOT' --configuration 'CONFIGURATION' --framework 'TFM' --generated exclude
```

`--trust-project` permits MSBuild project code evaluation; it is not a sandbox
or network isolation. `--incremental` can return `rebuiltAllProjects`; no-op still
evaluates MSBuild and hashes inputs. Missing dependencies require an explicitly
approved development preparation step outside this workflow. Do not run a full
index, automatic restore/download, delete the database, or enable generators to
make a warning disappear. Recheck status and the affected bounded query afterward.

## Parse and report

Help is text, other commands above produce one JSON stdout document. Errors go
to stderr. Inspect exit code before parsing; fatal errors have empty stdout.

| Output | Fields to preserve |
| --- | --- |
| status | `analysis.id`, `analysis.inputPath`, `analysis.rootPath`, `analysis.projects`, `analysis.coverage`, `completeness.limitations`, `analysis.freshness`, `analysis.freshnessCheck`, `analysisDiagnostics`, `metadataSummary`, `budget` |
| query | `snapshotId`, `analysisContext`, `completeness`, `stale`, `truncated`, `limits.reasons`, `outcome`, query-only `diagnostics`, `analysisDiagnostics`, `metadataSummary`, `budget`; `items[].entity` and `items[].path[]` source evidence |
| context | `snapshotId`, `analysisContext`, `completeness`, `stale`, `freshnessCheck`, `truncated`, `truncationReasons`, `limits`, `warnings`, selection and evidence |
| index | `mode`, `outcome`, `granularity`, `projectsAnalyzed`, dependency limitations and `analysis`; success can still be partial/unknown |

Query traversal excludes inferred by default and never traverses unresolved
targets. Context preserves all confidence classes. Callers/callees use CALLS;
impact is incoming static dependency reachability; path returns one bounded
shortest path. Neither means runtime reachability. A `notFound` or
`boundedSearchIncomplete` outcome must retain scope and limitations.

| Exit | Action |
| --- | --- |
| 0 | Inspect completeness/freshness/truncation, even for empty results. |
| 2 | Invalid arguments or query/status/context budget too small; inspect help/error, correct the bounded command. |
| 3 | Project trust absent; request approval, never append trust silently. |
| 4 | Missing SDK/assets/index, unsupported input/TFM, or an unsupported stored graph requiring an approved re-index; report the prerequisite, do not restore/index automatically. |
| 5 / 6 | Analysis or storage/access failure (including unknown SQLite schema); report stderr, preserve snapshot, stop dependent work. |
| 7 | Ambiguous symbol or lexical candidates; inspect JSON candidates and select an exact ID. |
| 8 | Symbol missing in this snapshot/scope; narrow search or inspect relevant source, no global absence claim. |
| 9 | Freshness requirement failed; report stale/unknown and seek an approved next step. |
| 10 / 130 | Writer busy / cancelled; report, no automatic retry or lock deletion. |

If the executable cannot be launched, report CLI unavailable. `doctor` checks
the local runtime/SDK, not target project readiness, agent authorization or
network isolation. No MCP, server, provider account or API key is needed by CLI.

Phase 11 distribution uses the local `UO.Graphs.Cli` .NET tool package and the same
`uographs` command. Export writes stdout: never use unchecked `>` over an existing
file; use the reviewed exclusive `scripts/safe_export.py` helper if present, or an
explicitly approved new destination with shell noclobber. Partial results and
truncation remain evidence limits; package installation alone is not index acceptance.
