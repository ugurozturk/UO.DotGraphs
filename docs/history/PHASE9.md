# Phase 9 — ABP, EF Core, and Refit

Recorded **2026-10-09**. The modules were implemented and limited local static
acceptance passed, but **the full Phase 9 gate remained open in this run** because
standard VSTest/startup and subprocess workspace tests did not pass. Phases 10–11
had not started. Later standard integration passes are recorded in
[STATUS](../STATUS.md); they do not rewrite the failures below.
Product **0.9.0**, JSON 4 / SQLite 3 / delivery 1. Prior: [Phase 8](PHASE8.md).

## Delivered scope

- ABP **10.1.1** service/module/repository contracts and conventional DI candidates,
  lifetime/exposure/disable overrides. Conventional endpoint candidates require
  static Create assembly configuration; RemoteService/NonAction filters apply.
  Candidates remain inferred with null route/verb. Repositories do not imply tables.
- EF Core **10.0.5** context/set/entity/configuration contracts and Table/Column/
  narrow fluent mapping declarations. Overrides remain separate declarations;
  runtime/scanning/Ignore/owned/custom-builder configuration stays unresolved.
  No effective model or SQL claim.
- Refit **8.0.0** real attributes/bound interface contracts and call edges. Equal
  routes across services remain distinct; base address/remote target unresolved.
  Local route similarity never creates a service edge. Custom/multiple/static/
  default-method/runtime configuration negatives remain separate.
- Nine relationship kinds and optional Framework/ModelMapping/HttpContract metadata
  use existing atomic storage/query/delivery. No product dependency or new layer.
- ABP LGPL-3.0-only closure isolated in a non-packable fixture, excluded from
  product/test/runner closure by audit. EF/Refit MIT and NUglify/MyCSharp license
  evidence checked. Inventory: **143** package/version entries.

Exact matrices, sources, risks, and test names: [ABP-EF-REFIT](../ABP-EF-REFIT.md).

## Recorded verification

macOS 27.0 ARM64, SDK 10.0.201, MSBuild 18.3.0, runtime/reference pack 10.0.5,
Roslyn 5.0.0.0.

| Command/check | Result |
| --- | --- |
| `dotnet --info` | PASS |
| Solution restore with ignore-failed-sources/NuGetAudit=false/single worker | Initial Refit PackageId collision NU1108; corrected, then PASS |
| `dotnet restore UO.Graphs.slnx --locked-mode --ignore-failed-sources -m:1 -nr:false -p:NuGetAudit=false` | PASS; explicit development step |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, 8.71 s, 0 warnings/errors; all three real-package fixtures |
| TestRunner restore with ignore-failed-sources/NuGetAudit=false/single worker | PASS, existing test dependencies only |
| TestRunner build with no-restore/single worker/no shared compilation | PASS, 1.97 s, 0 warnings/errors |
| `dotnet scripts/TestRunner/bin/Debug/net10.0/TestRunner.dll UnitTests .uographs/phase9-evidence/unit-final.json` | PASS **49/49**, sequential socket-free invocation of xUnit assertions; not VSTest |
| Standard filtered Phase9 unit VSTest | **ABORTED exit 1**, execution NOT RUN, SocketException (13) listener denial |
| `dotnet scripts/TestRunner/bin/Debug/net10.0/TestRunner.dll CliTests.Phase9 .uographs/phase9-evidence/integration-initial.json` | **FAIL 0/3**, each CLI workspace timeout after about 63 s, exit 5 |
| Three direct CLI indexes below | PASS **3/3**, exit 0 |
| `python3 scripts/phase9_smoke.py` | PASS, three module checks / 12 separate CLI processes |
| `python3 scripts/audit_licenses.py --check` | PASS, **143** package/version entries |
| Source/config/whitespace/links | PASS, 184 files |

The 49 unit cases included 14 new ones: ABP 5, EF 5, Refit 4, including their three
compilation-error theory rows once. Previous 35 passed. The new three integrations
were not successful in this run; old integration regressions were NOT RUN.
[TestRunner](../../scripts/TestRunner/README.md) uses fresh instances, awaits Tasks,
disposes IDisposable, and rejects unsupported lifecycle/data/skip shapes; it does
not verify xUnit discovery/parallelization or replace standard VSTest.

```sh
dotnet test tests/UO.Graphs.UnitTests --no-build --no-restore -m:1 -nr:false --filter FullyQualifiedName~Phase9 --logger 'trx;LogFileName=phase9-initial.trx' --results-directory .uographs/phase9-tests
dotnet run --project src/UO.Graphs.Cli --no-build --no-restore -- index samples/Phase9/Abp/Abp.csproj --full --trust-project --db .uographs/phase9-abp.db
dotnet run --project src/UO.Graphs.Cli --no-build --no-restore -- index samples/Phase9/EfCore/EfCore.csproj --full --trust-project --db .uographs/phase9-efcore.db
dotnet run --project src/UO.Graphs.Cli --no-build --no-restore -- index samples/Phase9/Refit/Refit.csproj --full --trust-project --db .uographs/phase9-refit.db
```

## CLI evidence

| Module | Entities / relationships / occurrences | Snapshot |
| --- | --- | --- |
| ABP | **47 / 220 / 323** | `9b841a6252664013b88b80419fcbee81` |
| EF | **53 / 200 / 247** | `164a0081932c4a5bb3bc00d7377c99a1` |
| Refit | **42 / 147 / 163** | `c029944fc1e545c7acb7a4ea05ab90f3` |

All partial and freshness unknown due to runtime/configuration/generator/dependency
limitations. Separate DLL reads verified ABP inferred handler→repository paths and
hidden-service negatives; EF annotation/fluent/dynamic/captured/computed mapping
boundaries; Refit equal-route separation, call→contract paths, and no local endpoint
binding. Full exports were untruncated. Commands, results, status/path/context/export
are in `.uographs/phase9-evidence/`, including smoke-commands.json and smoke-results.json.
Actual SQLite CLI output, not hardcoded graphs, supplied assertions.

## Corrections, refusal, and open gates

Refit fixture AssemblyName/PackageId fixed NU1108; ABP lifetime used its real
constructor after CS0655; Refit static HTTP CS0539 negative moved to generator-off
semantic compilation while normal fixtures built with generators. Duplicate TPA
fixture paths and a stalled implicit runner build were corrected by explicit build
and direct DLL invocation. Review found EF computed/captured property and runtime
schema/column, plus Refit multiple-attribute false certainty; implementation and
negative tests were fixed without weakening assertions.

A request to run standard VSTest outside the sandbox was automatically rejected
(`sandbox_approval=false`). Policy was not bypassed; fallback/direct successes did
not make the rejected command pass. This run left standard unit/new integration/
regression execution for an IPC-permitted environment. Later closure appears in STATUS.

Windows/Linux, installed tool, physical offline, and large-scale performance were
NOT RUN. No user application or inaccessible system was analyzed; no host/DbContext/
migration/DB/HTTP/MCP/LLM/telemetry was started or added. Git status 128 reflected
absent metadata; diff NOT RUN, file checks passed. AGENTS preserved; no commit/push/publish.

## Changed files

Core snapshot/relationships/query; new Abp/EfCore/Refit extractors and FrameworkSymbols;
extractor/indexer wiring, CLI/version, package/solution/locks, three fixtures,
Phase9 unit/integration/direction tests, phase9_smoke/TestRunner/audit tooling,
ABP-EF-REFIT/Phase8 history/NUglify license, README and related contracts/status/inventory/notices.

Suggested commit: `feat: analyze ABP EF Core and Refit with bounded semantic evidence`
