# Phase 3 — Semantic relationships and occurrences

Recorded **2026-10-08**. This phase passed its macOS ARM64 static relationship
acceptance; Phases 4–11 had not started. [Phase 2](PHASE2.md) contains prior evidence;
[STATUS](../STATUS.md) is current.

## Delivered scope

Central DECLARES, PROJECT_REFERENCES, INHERITS, IMPLEMENTS, OVERRIDES, USES_TYPE,
REFERENCES, and CALLS contracts using SemanticModel/symbol binding/IOperation,
without text/regex/name-based target matching. CALLS retains compile-time slots;
interface/virtual/override/runtime implementations are not conflated.

Unique relationships retain multiple occurrences with source spans/hashes,
analysis/context IDs, bound generic signatures, reasons, and candidates. Method
groups are REFERENCES; runtime delegate/dynamic/reflection targets unresolved.
Lambdas/local functions are separate callers; external/implicit endpoints do not
become invented source entities. Erroneous executable declarations do not promote
recovery binding to exact; valid independent members remain analyzable. Mismatched
TFM/source/parse reference contexts remain separate and partial.

Then-current product **0.3.0**, JSON **3**, SQLite **2**. Relationships/occurrences
were added to atomic replacement; v1 read/upgrade/rollback preserved old context.
Index/status gained counts; find returned adjacent evidence. Traversal/export were
not yet implemented. No new package; Phase3's net10.0 lock was empty and the existing
50-package inventory passed. See [RELATIONSHIPS](../RELATIONSHIPS.md) for current behavior.

## Recorded verification

macOS 27.0 ARM64, SDK 10.0.201, MSBuild 18.3.0, runtime 10.0.5, Roslyn 5.0.0.0.

| Command/check | Result |
| --- | --- |
| `dotnet --info` | PASS |
| `dotnet restore samples/Phase3/Semantics.csproj --locked-mode -m:1 -nr:false` | PASS; explicit fixture preparation |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, 7.50 s, 0 warnings/errors |
| Semantics.csproj build with same flags | PASS, 1.31 s, 0 warnings/errors |
| `dotnet test UO.Graphs.slnx --no-build --no-restore -m:1 -nr:false` | PASS **40/40**: 10 unit + 30 integration, zero skipped; 730 ms / 1m27s |
| `python3 scripts/audit_licenses.py --check` | PASS, 50 packages |
| XML/JSON/whitespace/links | PASS; final 37 source/config/document files |

```sh
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/Phase3/Semantics.csproj --trust-project --allow-generators --db .uographs/phase3-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- query find Overload --format json --db .uographs/phase3-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- status --db .uographs/phase3-final.db
dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --trust-project --allow-generators --db .uographs/phase3-hello-direct.db
```

All exit 0. Final Semantics graph: **41 entities / 117 relationships / 142
occurrences**, completeWithinDeclaredScope, snapshot
`ae45a0f760264507aca6f54b50dce6bc`. Separate find returned two correct overloads,
nine adjacent relationships, and ten occurrences under the then-v3 format.
HelloGraph: **15 / 20 / 23**, two projects, completeWithinDeclaredScope; Greet bound
to the actual Domain entity and implicit constructors remained external endpoints.

## Coverage and intermediate failures

Assertions compared all exact CALLS targets and prohibited wrong overload,
implementation, method-group, outer-lambda, or reverse edges. Tests covered two
call sites, real syntax/hash evidence, generics/extensions, virtual/base/explicit
implementation, partials, accessors/read-write/ref-return, nameof, delegate/dynamic,
invalid/ambiguous recovery, and independent valid members. SQLite/separate-process
round trips and schema/FK rollback retained all occurrence evidence.

Redirected/filtered tests aborted on VSTest SocketException (13); redirected index
failed exit 5 on BuildHost timeout without publication. Direct permitted runs passed.
A C# 14 contextual `field` fixture error was fixed by renaming it `backing`.
A mistaken invalid-call count was replaced by comparison against all six expected
source expressions, including nested new object(); assertions were strengthened.

Windows/Linux, physical offline, package install, Release JIT, and large-solution
performance were NOT RUN in this phase. No runtime dispatch/data-flow/reflection
resolution or exhaustive implicit lowering. Freshness remained notChecked and
project-reference evidence was explicitly not a precise XML span. Git metadata
was absent (status 128); file checks replaced unavailable Git diff. No commit/push/publish.

## Changed files

Core GraphSnapshot/new Relationships; Roslyn indexer/collector/new RelationshipCollector
and test AssemblyInfo; Storage GraphStore; CLI Program; version; new RelationshipTests,
Phase3Tests and fixture, updated existing tests; RELATIONSHIPS, IDENTITY, README,
ARCHITECTURE, STATUS, Phase2 history. Existing instructions/packages were unchanged.

Suggested commit: `feat: implement phase 3 semantic relationships and source occurrences`
