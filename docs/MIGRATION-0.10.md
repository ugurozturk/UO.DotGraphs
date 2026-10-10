# CLI 0.10 migration

CLI 0.10.0 changes query JSON `schemaVersion` and status `contractVersion` to **5**.
Index/doctor remain **4**, context/export **1**, declaration identity **2**, and
SQLite **3**. Existing schema 3 indexes can be read without re-indexing. A stored
snapshot retains its original tool version; it is not the running CLI version.

Query/status default to compact `summary` output capped at 12,000 Unicode scalar
values, counting JSON escapes and the final LF. Read `budget.actualChars` for the
actual size. Use `--max-chars 1..1000000` to change this cap. Too-small mandatory
envelopes return exit 2, empty stdout and the minimum budget on stderr.

Full analysis and fingerprint limitations remain intact. Some existing large
snapshots have mandatory metadata larger than 12,000 scalars; their default read
returns a minimum-budget error. Supply an explicit larger bounded budget for
that task. The cap is not a promise that every snapshot has a small envelope.

Consumers must move analysis diagnostics from `analysis[Context].diagnostics`
and duplicate query `diagnostics` into `analysisDiagnostics.samples[].message`.
Query `diagnostics` now contains query explanations only. Analysis limitations
occur once at `completeness.limitations`. Check diagnostic `total`/`omitted` and
sample `truncated`; ordinal examples are not severity-ranked. Summary project
contexts exclude analyzers, reference contexts and detailed MSBuild metadata.
Read `totalProjects`, `omittedProjects` and `metadataSummary` for metadata omissions,
separately from graph coverage and result truncation.

For full analysis metadata/diagnostics, use `--details full`. **Full remains v5; it
does not restore v4 field layout.** It preserves query traversal and evidence
limits and rejects any explicit `--max-chars`. Its `budget.maxChars` is null.
Keep full diagnostics out of model context unless a concrete task requires them.

Items retain their original graph records and evidence. `resolvedStart` and
`resolvedTarget` retain resolved endpoints and related project scope even for
empty traversals. Character pruning removes whole items and preserves at least
one result/candidate; a whole path is indivisible. Read `limits.returned`,
`truncated` and `limits.reasons`. A single displayed ambiguous candidate still
requires explicit selection. Freshness requirements and domain exits remain.

Plugin 0.2.0 and all repository/template skills target CLI 0.10.0 / query-status
v5. They use short definition, semantic-relation and impact workflows. Unsupported
CLI output requires an explicit compatible setup; there is no automatic install,
index update or unbounded fallback. Installed plugins are not modified by this
repository change. Agent discovery/model E2E needs separate acceptance.
