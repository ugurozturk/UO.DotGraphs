# Phase 7 — Static Microsoft DI and ASP.NET Core extraction

Recorded **2026-10-09**. Phase 7 passed its stated macOS ARM64 static acceptance;
Phases 8–11 had not started. Product **0.7.0**, JSON 4 / SQLite 3 / delivery 1.
Prior evidence: [Phase 6](PHASE6.md); current status: [STATUS](../STATUS.md).
No application/container/HTTP host/database was started or analyzed assembly loaded
through reflection.

## Delivered scope

Small internal IFrameworkExtractor/evidence context with static DI and ASP.NET
modules; no plugin loader, marketplace, or new layer. AddSingleton/Scoped/Transient
forms include generic/self/interface, typeof/open generic, instance/factory,
multiple/conditional declarations, opaque scanning/wrappers, and computed types.
Separate registration entities retain lifetime/form/types/limits; factory method
binding does not prove its returned implementation or runtime selection.

Controller attributes and Minimal MapGet/MapPost/MapMethods support bounded groups,
immutable aliases, method/lambda/top-level handlers, with unresolved runtime values.
Endpoint identities separate duplicate declarations and verbs; collisionKey is not
identity. REGISTERS/IMPLEMENTED_BY/USES_FACTORY/INJECTS/HANDLED_BY/ACTIVATES preserve
source/context/confidence through SQLite/query/context/export. Constructor selection
is inferred and endpoint activation notProven; CALLS remains compile-time.

Two actual framework fixtures were built, including a Web SDK app that was never
run. No NuGet dependency was added; 50-package inventory unchanged. Full matrix:
[FRAMEWORKS](../FRAMEWORKS.md).

## Recorded verification

macOS 27.0 ARM64, SDK 10.0.201, MSBuild 18.3.0, .NET/ASP.NET runtime/reference pack
10.0.5, Roslyn 5.0.0.0; dotnet --info exit 0.

| Command/check | Result |
| --- | --- |
| `dotnet restore UO.Graphs.slnx --locked-mode --ignore-failed-sources -m:1 -nr:false -p:NuGetAudit=false` | PASS, explicit development preparation |
| Restore each Phase7 Frameworks/MinimalWeb csproj with ignore-failed-sources, NuGetAudit=false, single worker | PASS; new empty package locks; MinimalWeb emitted CSSM_ModuleLoad but completed |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, 10.65 s, 0 warnings/errors |
| Final unit TRX | PASS **25/25**, 5.33 s |
| Initial full solution test session | Terminal reported Unknown process id; total unverified, not counted as full success |
| Split integration TRX groups | PASS **1/1** (5m37s) + **81/81** (5m47s) = **82/82** |
| Final framework integration after MVC boundary fix | PASS **2/2**, 17.62 s |
| `python3 scripts/audit_licenses.py --check` | PASS, 50 packages |
| Whitespace/config/links | PASS, 112 files |

The 82 integration cases preceded the final small MVC inheritance fix. After it,
all 25 unit and two framework integrations, build, and smoke were repeated.
This covers 107 distinct cases, not one final-binary 107-case run. TRX counters
under `.uographs/phase7-tests/` had no failed/error/skipped results.

```sh
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~IncrementalMatchesCleanFullForEveryMutationAndNoOpSkipsSemanticAnalysis' --logger 'trx;LogFileName=incremental.trx' --results-directory .uographs/phase7-tests --logger 'console;verbosity=normal'
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName!~IncrementalMatchesCleanFullForEveryMutationAndNoOpSkipsSemanticAnalysis' --logger 'trx;LogFileName=integration.trx' --results-directory .uographs/phase7-tests --logger 'console;verbosity=normal'
dotnet test tests/UO.Graphs.UnitTests --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=unit-final.trx' --results-directory .uographs/phase7-tests --logger 'console;verbosity=normal'
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~FrameworkDeclarations|FullyQualifiedName~MinimalWebSdk' --logger 'trx;LogFileName=framework-final.trx' --results-directory .uographs/phase7-tests --logger 'console;verbosity=normal'
```

## Real CLI smoke

Each argument row used the real prefix
`dotnet run --project src/UO.Graphs.Cli/UO.Graphs.Cli.csproj --no-build --no-restore --`.

| Arguments | Result |
| --- | --- |
| `index samples/Phase7/Frameworks.csproj --full --trust-project --db .uographs/phase7-frameworks.db` | exit 0; **69 / 243 / 301** entities/relationships/occurrences |
| `index samples/Phase7/MinimalWeb/MinimalWeb.csproj --full --trust-project --db .uographs/phase7-web.db` | exit 0; **12 / 47 / 50** |
| `query find AddScoped --db .uographs/phase7-frameworks.db` | exit 0; three distinct scoped declarations, open generic/unresolved metadata |
| `query dependencies 'GET /api/time' --depth 3 --db .uographs/phase7-web.db` | exit 0; handler → service and IClock.Now CALLS evidence |
| `query dependencies 'GET /api/Messages/{id}' --depth 3 --include-inferred --db .uographs/phase7-frameworks.db` | exit 0; exact HANDLED_BY → inferred ACTIVATES → exact INJECTS |
| `query symbol 'GET /duplicate' --db .uographs/phase7-frameworks.db` | Expected exit 7; two candidates, no merge |
| `context 'GET /api/time' --max-chars 30000 --db .uographs/phase7-web.db` | exit 0 |
| `export --format json --db .uographs/phase7-frameworks.db` | exit 0; 20 endpoints, 10 registrations |
| `export --format json --db .uographs/phase7-web.db` | exit 0; two endpoints, one registration |

Snapshots: framework `2d069f6bd5f0498cbfa88a6ea2d142b7`, web
`b6b0d688b0f248ef840144df6f67a26e`. Evidence: `.uographs/phase7-evidence/`.
Both partial; index freshness unknown because custom item/Web SDK dependencies were
unproven. Phase6's conservative reuse policy was not weakened.

## Coverage, failures, and limits

Fourteen new unit/two integration cases covered actual symbol binding/lookalikes,
lifetimes/multiple registrations/open generics/constructors, factory uncertainty,
conditional/opaque forms, group/method arrays/collections, duplicates/verbs, dynamic
routes/delegates, MVC prefixes/tokens/absolute routes and exclusions, inherited/custom
route limits, erroneous compilations, relocation/context identity, and real Web SDK
persistence. No runtime registration/activation was asserted.

Interim nullable-array and missing-assets build failures were corrected. A stalled
parallel restore was cancelled; single-worker restore passed. Duplicate-route
negative fixtures were separated into methods instead of suppressing diagnostics.
Fixture counts/culture ordering and mistaken root-metadata/export options were
corrected while retaining behavior assertions. Redirected indexes failed BuildHost
timeout/exit 5 and preserved snapshots; direct repeats passed. Redirection causality
was not established.

Windows/Linux, packaged tool, physical offline, and large-scale performance were
NOT RUN. Runtime host/HTTP/container/database execution was outside static scope.
Git status returned 128 without metadata; diff NOT RUN, file checks passed.
No commit/push/publish or AGENTS modification.

## Changed files

Core snapshot/relationships/query; new Roslyn FrameworkExtraction/MicrosoftDiExtractor/
AspNetCoreExtractor and indexer/collector changes; CLI/version/solution; Phase7 library
and Web fixtures/locks; FrameworkTests/Phase7Tests and unit references; FRAMEWORKS,
Phase6 history, README/PRODUCT/ARCHITECTURE/RELATIONSHIPS/QUERIES/STATUS.

Suggested commit: `feat: add evidence-based Microsoft DI and ASP.NET Core extraction`
