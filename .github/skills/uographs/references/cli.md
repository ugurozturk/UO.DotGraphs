# CLI reference

Verified contract: UO.Graphs **0.9.0**; query JSON **4**, status/doctor
`contractVersion: 4`, SQLite schema **3**, context `formatVersion: 1`.
If the installed CLI differs, read its help and verify compatibility before using
these examples. Do not invent flags or update/install automatically.

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
uographs query find 'TEXT' --limit 10 --format json --db 'DB'
uographs context 'SYMBOL_ID' --max-chars 12000 --max-nodes 8 --limit 12 --depth 1 --max-edges 100 --evidence-limit 2 --format json --db 'DB'
uographs query callers 'SYMBOL_ID' --depth 1 --limit 10 --max-edges 100 --evidence-limit 2 --db 'DB'
uographs query callees 'SYMBOL_ID' --depth 1 --limit 10 --max-edges 100 --evidence-limit 2 --db 'DB'
uographs query impact 'SYMBOL_ID' --depth 2 --limit 10 --max-edges 100 --evidence-limit 2 --db 'DB'
uographs query path 'SYMBOL_ID' 'TARGET_ID' --depth 3 --limit 10 --max-edges 100 --evidence-limit 2 --db 'DB'
```

The initial status reports `analysis.freshness: notChecked`; this is not fresh.
`--check-freshness` hashes tracked inputs without MSBuild/restore, but may be
expensive. Record its reasons and measured counts. Subsequent reads without this
flag say `notChecked`; cite the earlier status check separately, never relabel
those results as fresh. If source changes or time matters, check again within
the budget. `--require-fresh` rejects stale/unknown with exit 9 and empty stdout;
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
| status | `analysis.id`, `analysis.inputPath`, `analysis.rootPath`, `analysis.projects`, `analysis.coverage`, `analysis.limitations`, `analysis.freshness`, `analysis.freshnessCheck`, `analysis.diagnostics` |
| query | `snapshotId`, `analysisContext`, `completeness`, `stale`, `truncated`, `limits.reasons`, `outcome`, `diagnostics`; `items[].entity` and `items[].path[]` source evidence |
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
| 2 | Invalid arguments or context budget too small; inspect help/error, correct the bounded command. |
| 3 | Project trust absent; request approval, never append trust silently. |
| 4 | Missing SDK/assets/index, unsupported input/TFM, or an old query schema requiring re-index; report the prerequisite, do not restore/index automatically. |
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
