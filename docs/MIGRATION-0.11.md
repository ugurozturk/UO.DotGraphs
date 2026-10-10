# CLI 0.11 migration

CLI 0.11.0 changes query `schemaVersion` and status `contractVersion` from 5 to
**6**. Index/doctor remain 4, context/export 1, SQLite 3 and declaration identity
2. Existing indexes need no rebuild. Snapshot tool versions remain historical;
use CLI help for executable compatibility, once per verified executable/session.

This is an intentional compatibility boundary: consumers that assumed complete
metadata lists in v5 must recognize sampling in v6. All returned graph items,
identities, source locations, confidence, reasons and evidence retain their shape.
`--details full` is also v6, not a v5 compatibility mode. It has no character cap
and rejects an explicit `--max-chars`; query traversal/evidence limits still apply.

Summary retains its 12,000 Unicode-scalar budget, counting serialized JSON escapes
and the final LF. `budget.actualChars` is the measured size. In addition to
analysis diagnostics, these metadata fields are sampled: coverage limitations,
fingerprint limitations, freshness reasons, project preprocessor symbols and
skipped TFMs. Each has up to three ordinal examples of at most 300 scalars.
Summary project contexts are capped at ten and may be removed for space; only
query-relevant projects are eligible in queries. Long displayed project paths
are shortened at 300 scalars; entity/relationship source paths are never shortened.

`metadataSummary.fields[]` contains:

| Field | Meaning |
| --- | --- |
| `path` | JSON Pointer to the sampled list or shortened scalar project path |
| `total` | Original number of entries; 1 for a scalar |
| `omitted` | Entries not displayed (`total - displayed count`) |
| `shortened` | Displayed values shortened to 300 scalars |

Every sampled list has a descriptor, even if its original length is zero. Full
output has no sampled fields (`fields: []`). Diagnostic totals/omissions and
project totals/omissions retain their existing dedicated fields. Do not treat an
empty sampled list as absence of limitations. Do not navigate a shortened path.

When freshness reasons exactly equal fingerprint limitations, the list occurs
only under `dependencyFingerprint.limitations`; `freshnessCheck.reasons` is empty
and `freshnessCheck.reasonsReference` is a JSON Pointer to that list. Follow it
in both full and summary output, including its omission descriptor in summary.
The fingerprint `complete` flag, coverage/freshness states and check counters are
never omitted. Full preserves original list order and text at the referenced
location. No diagnostic, missing target, or framework reason is invented.

Budget reduction removes diagnostic samples, other metadata samples, then
project contexts before pruning whole result tails. Metadata omissions do not
set graph `truncated` or traversal reasons. Removed items set `maxChars` in
`limits.reasons`, with actual `limits.returned`. One whole item/path remains for
nonempty results; if it and the minimum reliable envelope cannot fit, exit 2
reports the required budget with empty stdout. An ambiguous result with one
remaining candidate stays ambiguous. Evidence-limit truncation remains explicit.

Input/root, snapshot identity and generator policy remain exact. Included project
IDs, TFM/configuration and SDK/language remain exact; omitted project contexts
are counted, never replaced with guessed scope. Request full metadata if a task
needs an omitted context. Unchecked freshness remains `notChecked`, checked
incomplete freshness can be `unknown`, and partial/stale data stays partial/stale.

Repository consumers updated: envelope schema, CLI integration assertions,
phase4 smoke, package smoke and skill verification. Plugin 0.3.0, standalone
skills and templates target CLI 0.11.0/v6. Installed plugin caches are unchanged;
no package publication or external consumer acceptance is claimed. Context/export
budgeting and semantic relationship extraction are unchanged.
