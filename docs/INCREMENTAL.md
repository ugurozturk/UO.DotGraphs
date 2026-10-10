# Incremental indexing and freshness

Incremental indexing uses query JSON v5, SQLite schema 3 with an optional
`fingerprint` table, and context/export format 1. It was introduced in product
0.6.0; the current source is **0.10.0**. Legacy snapshots without a manifest remain
readable where their schema allows; checked freshness is unknown, and the next
index rebuilds the selected scope.

```sh
uographs index App.slnx --trust-project --incremental
uographs index App.slnx --trust-project --full
uographs status --check-freshness
uographs query callers MyMethod --require-fresh
uographs context MyMethod --require-fresh
uographs export --format json --check-freshness
```

## Index contract

Incremental is the default and cannot be combined with `--full`. Both modes
require trust before target MSBuild evaluation; neither restores dependencies.
Granularity is **entireSelectedProjectClosure**: any changed input/context rebuilds
all selected projects and their reference closure, including unchanged files.
There is no method/file reuse or selective project reuse.

Only an equal, complete dependency manifest and evaluation allow `outcome: noOp`,
`projectsAnalyzed: 0`, and `analysisMilliseconds: 0`. No Roslyn workspace,
compilation, semantic extraction, or database replacement occurs; snapshot ID is
preserved. SDK selection, MSBuild evaluation/preprocessing, and SHA-256 scans
still run, so a no-op is not zero I/O or zero MSBuild cost.

All other cases produce `rebuiltAllProjects`; full mode always takes this path.
Source/symbol additions, deletions, moves, signatures, overloads, global usings,
public API, configuration/TFM/defines, and dependencies follow this broad
invalidation policy. Entities, edges, occurrences, and framework inference records
are completely replaced in one transaction.

Index JSON reports mode, granularity, outcome, analyzed project count, fingerprint/
analysis milliseconds, hashed file/byte counts, and dependency limitations.
Analysis timing includes pre-publication validation. Index freshness uses
`indexEvaluationAndContentValidation` and covers evaluation, analysis, and final
validation, excluding SQLite commit. This result is not reused as freshness for
later reads, whose default remains `notChecked`.

## Dependency fingerprint

Reuse depends on **raw-byte SHA-256** plus sorted file inventory, not timestamps.
Same-size/same-mtime edits and additions/deletions/moves therefore invalidate.
The tracked model includes:

- Root source/input inventory, solution/project files, additional inputs,
  editorconfig, ancestor Directory.Build.props/targets, Directory.Packages.props,
  global.json, and NuGet config. Missing ancestor files are tracked for later creation.
  MSBuild user/extensions/SDK/Roslyn locations and evaluated CustomBefore/After
  imports are included, even where an implicit import does not yet exist.
- Actual preprocessed imports/digest; evaluated Compile, ProjectReference,
  AdditionalFiles, Analyzer, EditorConfigFiles, TFM/configuration/defines, and
  language/nullable/optimization options. Item access/create/modify timestamps are excluded.
- Assets/locks and NuGet-generated props/targets; package-root contents; selected
  SDK, reference packs, workload manifests, tool-directory contents, SDK installation
  membership, and dotnet host. Compiler/analyzer/rule/reference changes invalidate.
- Index options and runtime/OS/architecture/culture/working-directory/environment
  digest. Environment values/secrets are not stored. Preparation subprocesses use
  the invocation's initial environment, excluding Locator's internal mutations.

Ordinary scanning excludes bin, .git, and .uographs. Under obj it tracks source C#,
editorconfig, JSON/props/targets, not arbitrary caches/binaries. Standard obj sources
created during initial loading are compared against Roslyn's consumed decoded text,
then their final raw hashes are stored. The chosen database and WAL/SHM/journal/
writer-lock files are excluded.

Completeness refers to the validated **SDK 10.0.201 base Microsoft.NET.Sdk
declarative input model**, not every MSBuild program's external dependencies.
Custom imports/targets/tasks, property functions/existence conditions, unknown
properties, external/output item patterns, direct assembly references, other SDKs,
and generator execution can leave dependencies unproven. Known files are still
hashed; `dependencyFingerprint.complete: false` and reasons are stored.
**No-op is disabled and each index rebuilds the scope.** Permitted generators also
disable reuse because arbitrary files/time/environment can affect their output.

Unreadable or symlinked inventory inputs fail explicitly; an unvalidated snapshot
is not published. Broad SDK/package hashing can be costly; see actual
[measurements](PERFORMANCE.md).

## Freshness

Status, query, context, and every export format share this contract:

| Option/result | Meaning |
| --- | --- |
| Default `notChecked` | No content hashing; method none and zero hash file/byte/ms |
| `--check-freshness` | Check manifest inventory, contents, and environment; no MSBuild/restore/subprocess |
| `fresh` | Tracked inputs match at check time and dependency model is complete |
| `stale` | Tracked content, membership, or environment changed |
| `unknown` | Legacy/missing manifest, dependency limitations, or read error |
| `--require-fresh` | Enables checking; stale/unknown yield exit 9, explanatory stderr, empty stdout |

`freshnessCheck` records method, actual hashed files/bytes, elapsed time, and
reasons. Missing-file/membership markers do not count as files. Interrupted reads
report `countsComplete: false`; counts are measured lower bounds. The manifest is
read in the same transaction as its graph, never from another generation. Read
commands do not modify the database.

Coverage is independent: a partial graph caused by compilation errors or skipped
generators can have fresh tracked inputs without becoming complete analysis.
A filesystem check is not an atomic source snapshot; files may change afterward.
Keep inputs stable during indexing. A second evaluation/content check plus consumed
source validation rejects observed concurrent changes before publication; no
unbounded retry is attempted. Do not build/pack the tool while indexing with it,
since its binaries are fingerprint inputs.

## Atomicity and concurrency

An exclusive cross-process OS writer lease is acquired before analysis and held
through commit. A competing writer gets exit **10**. The existence of the lock
file does not mean the lease is held; the OS releases its handle lock on process
exit. The lock file is retained to avoid races between different lock inodes.

SQLite WAL and one transaction publish graph/manifest together. Each reader uses
one read transaction and sees an old or new generation, never a partial mixture.
Existing readers can continue reading the old generation. Errors/cancellation roll
back; process death leaves the last commit recoverable. Ctrl-C returns **130**.
An interrupted first index has no completed snapshot yet.

Acceptance covers local filesystems. Network filesystems, accessing the same DB
through different hardlink names, and physical power-loss testing remain outside
that evidence. [SECURITY](SECURITY.md) distinguishes real process/cancellation
checks from physical disk/network isolation.
