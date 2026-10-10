# Phase 10 — Policy-aware Copilot skill

Recorded **2026-10-09**. Phase 10 was implemented, but **not all acceptance gates
closed; real Copilot E2E was NOT RUN**. Phase 11 had not started in this record.
Product remained **0.9.0**, JSON 4 / SQLite 3 / context 1; CLI/library behavior and
packages were unchanged. [Phase 9](PHASE9.md) retained its three integration failures.
Later standard test/package results are in [STATUS](../STATUS.md).

## Delivered scope

Repository `.github/skills/uographs/SKILL.md` and relative CLI reference describe
help/doctor/status → bounded find/context → needed traversal → relevant source.
They preserve confidence, coverage, freshness, truncation, and context. Indexing
requires scope/trust/cost authorization; approved updates use incremental mode.

Source/snippets/labels/diagnostics remain data. No shell text execution from graph,
repository dump, blanket approval, policy bypass, or automatic restore. Disabled
MCP does not authorize another tool; local graph does not make Copilot offline.
The standalone template byte-matches both files. Installer exclusively creates
files, rejects existing/symlinked destinations, and changes no settings.

[Installation/manual acceptance](../COPILOT-SKILL.md) was added. README placeholders
were quoted and exits 9/10/130 documented/tested. No new layer, service, dependency,
telemetry, or AGENTS changes.

## Installed-format evidence

Local manifests showed VS Code **1.141.0**, commit
`2a59476c9bfcb90b3ddc372c36762471b7dfad1c`, bundled Copilot Chat **0.69.0**, engine
^1.141.0. Workbench contained .github/skills/SKILL.md/Agent Skills support. Layout was
compared with [official documentation](https://code.visualstudio.com/docs/agent-customization/agent-skills).
Evidence: `.uographs/phase10-evidence/installed-format.json`.

This did not prove active-profile settings, organizational permission, model access,
or actual skill selection. Settings were unchanged; no source/graph sent to Copilot.
Manual scenarios were not executed in a real model session.

## Recorded verification

macOS ARM64, SDK 10.0.201, runtime 10.0.5, pre-restored assets; no new restore/dependency.

| Command/check | Result |
| --- | --- |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, final 5.21 s, 0 warnings/errors |
| Standard unit VSTest with phase10.trx | PASS **49/49**, zero skipped, about 6 s; no reflection fallback |
| Four standard CLI integration cases below | PASS **4/4**, about 15 s |
| `python3 scripts/test_skill.py` | PASS **5/5** artifact/installer tests |
| `python3 scripts/test_skill.py --db .uographs/phase10-live.db --evidence .uographs/phase10-evidence/read-tests.json` | PASS **10/10**, 12.989 s, 45 CLI processes |
| Same Python command with --run-index and final-tests.json | **FAIL 10 passed / 1 failed**, 101.437 s, 46 attempts; direct-DLL incremental index timed out at 90 s |
| Focused Phase10ApprovedIncrementalSkillWorkflowAcrossProcesses VSTest | **ABORTED exit 1 / execution NOT RUN**, SocketException (13) listener denial |
| Direct CLI smoke below | PASS, **15 / 20 / 23**, incremental rebuild |
| `python3 scripts/audit_licenses.py --check` | PASS, **143** package/version entries |
| Ruby YAML.safe_load on both frontmatters | PASS; unrelated ffi native-extension warning remained |
| skill-creator quick_validate.py | NOT RUN, startup exit 1: PyYAML missing; no package installed |
| Python AST/local links/whitespace | PASS |
| Independent read-only review | CLI/policy/injection scenarios/parity/overwrite reviewed; unknown-schema exit 6 clarified, not a model behavior test |

```sh
dotnet test tests/UO.Graphs.UnitTests --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=phase10.trx' --results-directory .uographs/phase10-evidence/vstest
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~RealSolutionIndexPersistsTypeAndMethodAcrossProcesses|FullyQualifiedName~HelpAndDoctorRunWithoutLoadingTarget|FullyQualifiedName~TrustIsRequiredBeforeMalformedProjectIsEvaluated|FullyQualifiedName~MissingAssetsFailsWithoutRestoringOrCreatingDatabase' --logger 'trx;LogFileName=phase10-cli.trx' --results-directory .uographs/phase10-evidence/vstest
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter FullyQualifiedName~Phase10ApprovedIncrementalSkillWorkflowAcrossProcesses --logger 'trx;LogFileName=phase10-workflow.trx' --results-directory .uographs/phase10-evidence/vstest
dotnet run --project src/UO.Graphs.Cli --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --incremental --trust-project --db .uographs/phase10-live.db
```

These were separate invocations, not one combined pass. The 5 artifact cases are
included in 10/10 and are not counted twice. README index examples were validated at
the trust gate and text exports at missing DB; reference index used a missing-assets
negative. These parser checks do not count as successful indexing/export.

## Direct CLI and read evidence

Index exit 0, snapshot `b2d1afba5fcd4d8295e95bfa3abca111`, **15 / 20 / 23**, mode
incremental, outcome rebuiltAllProjects, **2 projects**. Index freshness fresh,
coverage partial for generators. Fingerprint **3342 ms**, analysis **5492 ms**,
**13394 files / 1681271888 bytes** hashed. Later reads defaulted to notChecked.

live-status.json and read-tests.json preserve snapshot/argv/exits. Tests backed up
an actual graph and verified Welcome → Greet exact evidence, source lines, partial
propagation, stale/require-fresh exit 9, hostile literal input, lexical/missing/budget
negatives. A separate command checked a legacy snapshot:

```sh
dotnet src/UO.Graphs.Cli/bin/Debug/net10.0/uographs.dll status --db .uographs/phase5-smoke.db --check-freshness
```

Exit 0, freshness unknown because no manifest; saved as unknown-status.json.
That old graph was not claimed as a newly produced snapshot.

## Open gates at the time

Real Copilot E2E remained NOT RUN. Python DLL index timeout and focused VSTest
startup failure remained separate from the passing 49 unit/4 CLI cases/direct
smoke. Initial direct-DLL load timeouts returned exit 5; a first 90 s Python timeout
left no DB, causing a subsequent missing-DB setup failure. After direct dotnet run
created the graph, 10/10 reads passed. No test/assertion/permission was weakened.

Windows/Linux, packaged tool, physical offline, organizational enforcement, and
large-scale performance were NOT RUN. Phase9's three integrations and the full old
integration suite were NOT RUN here. Git status 128 reflected absent metadata;
diff NOT RUN. No commit/push/publish or user application host execution.

## Changed files

Added repository skill/reference, matching template/reference, install_skill.py,
test_skill.py, Phase10Tests, COPILOT-SKILL and Phase9 history. Updated README,
PRODUCT, ARCHITECTURE, and STATUS.

Suggested commit: `feat: add policy-aware Copilot skill for the uographs CLI`
