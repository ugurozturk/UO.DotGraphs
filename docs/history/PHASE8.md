# Phase 8 — Mediator and messaging flows

Recorded **2026-10-09**. Phase 8 passed its macOS ARM64 static acceptance; Phases
9–11 had not started. Product **0.8.0**; JSON 4 / SQLite 3 / delivery 1 unchanged.
Prior evidence: [Phase 7](PHASE7.md); current: [STATUS](../STATUS.md).
No broker, host, DI container, network service, or mediator runtime was started.

## Delivered scope

Actual UO.Mediator sources and UO.Mediator.Shared **2.4.2**, MediatR **12.4.1** /
Contracts **2.0.1**, and MassTransit.Abstractions **8.3.6** DLL/tag sources established
API/assembly binding. These permissive dependencies are fixtures, not product
packages. No nonexistent UO notification/Publish API was invented.

HANDLES/DISPATCHES/PUBLISHES/CONSUMES/POSSIBLE_TARGET/BEHAVIOR_FOR/POSSIBLE_BEHAVIOR
separate exact contracts from inferred candidates and unresolved runtime selection.
Closed message identity includes framework, assembly/declaration/source context,
and recursive generic arguments, separating equal names and constructed messages.
CALLS remains compile-time. Direct generic DI registration adds distinct occurrence
evidence without proving activation; scanning/topology/behavior order stay unknown.
SQLite/query/context/export preserve everything. Source-contract fixtures remain
separate from actual-package compatibility. See [MESSAGING](../MESSAGING.md).

## Recorded verification

macOS 27.0 ARM64, SDK 10.0.201, MSBuild 18.3.0, runtime/reference pack 10.0.5,
Roslyn 5.0.0.0; dotnet --info passed.

| Command/check | Result |
| --- | --- |
| `dotnet restore samples/Phase8/Uo/Uo.csproj --ignore-failed-sources -p:NuGetAudit=false -m:1 -nr:false` | PASS, actual cached UO package |
| Same restore flags for samples/Phase8/PackageCompatibility/PackageCompatibility.csproj | PASS, actual MediatR/MassTransit acquired |
| `dotnet restore UO.Graphs.slnx --locked-mode --ignore-failed-sources -m:1 -nr:false -p:NuGetAudit=false` | PASS, explicit development acquisition |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, final 6.08 s, 0 warnings/errors |
| Unit TRX | PASS **35/35**, about 4 s |
| Focused messaging integration TRX | PASS **2/2**, about 14 s |
| Full integration TRX | PASS **84/84**, 10m57s |
| `python3 scripts/phase8_smoke.py` | PASS, seven separate CLI processes on two real package graphs |
| `python3 scripts/audit_licenses.py --check` | PASS, **55** packages |
| Config/whitespace/local links | PASS, 138 files |

```sh
dotnet test tests/UO.Graphs.UnitTests --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=unit-final.trx' --results-directory .uographs/phase8-tests
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~UoRealPackageEndpoint|FullyQualifiedName~RealMediatrAndMassTransit' --logger 'trx;LogFileName=messaging-final.trx' --results-directory .uographs/phase8-tests
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=integration-final.trx' --results-directory .uographs/phase8-tests
```

**119 distinct final-binary cases** passed, with zero failed/error/notExecuted in
TRX; the repeated two messaging cases were not counted twice. Ten new unit/two
integration cases covered actual packages, response/no-response, chained dispatch,
multiple notifications/consumers, pipeline/open generics, conditional registration,
object/computed/explicit Type, interface initializers, same-named identities,
lookalikes, and compilation errors. Existing tests remained.

## Real CLI smoke

Prefix: `dotnet run --project src/UO.Graphs.Cli --no-build --no-restore --`.

| Arguments | Result |
| --- | --- |
| `index samples/Phase8/Uo/Uo.csproj --full --trust-project --db .uographs/phase8-direct-uo.db` | exit 0; **43 / 198 / 260** |
| `index samples/Phase8/PackageCompatibility/PackageCompatibility.csproj --full --trust-project --db .uographs/phase8-direct-packages.db` | exit 0; **44 / 219 / 279** |

Separate direct-DLL query processes recorded commands/exits in
`.uographs/phase8-evidence/commands.json`, with status/path/context/export/event
results. Exports were untruncated: five UO and seven other-package message entities.

```text
GET /phase8/start → FlowController.Start → Service.Run
  → RequestA → HandlerA.HandleAsync → RequestB → HandlerB.HandleAsync
HANDLED_BY exact, CALLS exact, DISPATCHES exact, POSSIBLE_TARGET inferred,
DISPATCHES exact, POSSIBLE_TARGET inferred
```

Without inferred opt-in this path was absent under the selected edge policy.
Context retained the same chain/source evidence and external-consumer limitations.
MediatR had First/Second and MassTransit Consumer1/Consumer2 as separate inferred
targets while each unresolved runtime-target record remained.

Snapshots: UO `ef7d776004ec4287ae6e82b8f89153ba`, packages
`c170d7275dc74ed8a51d98341383c241`. Both partial and freshness unknown due to runtime/
generator/custom-item/external-input limitations; no broader reuse claim.

## Intermediate failures and boundaries

Initial curl acquisition failed DNS (exit 6); explicit dotnet restore later passed.
Initial unit 32/35 and integration 0/2 failures exposed central enum-list and test
expectation/options issues; tests were corrected without removing behavior checks.
Python-captured indexing timed out exit 5; direct fixture indexes and subsequent
read smoke passed separately.

Windows/Linux, installed tool, physical offline, and large-scale performance were
NOT RUN. Concrete dispatchers/wrappers/streams, generic substitution, scanning,
and topology expansion remained outside support. Git status 128 reflected absent
metadata; diff NOT RUN, file checks passed. No AGENTS edit or commit/push/publish.

## Changed files

Core graph/relationships/query; new MessagingExtractor and extractor/indexer wiring;
CLI/version/central package/solution/locks; Phase8 package and source-contract fixtures
with provenance/licenses; MessagingTests/Phase8Tests/direction assertions;
phase8_smoke.py/audit header; MESSAGING, Phase7 history, README/PRODUCT/ARCHITECTURE/
RELATIONSHIPS/QUERIES/STATUS/DEPENDENCIES/notices.

Suggested commit: `feat: model mediator and messaging flows with semantic evidence`
