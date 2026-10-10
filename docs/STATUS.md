# Verified status

Last updated: **2026-10-10**.

**Phase 11 hardening is implemented; full release acceptance remains incomplete.**
Source **UO.Graphs 0.10.0**, package **UO.Graphs.Cli**, command **uographs**.
Query/status JSON **5**, index/doctor **4**, SQLite **3**, context/export **1**, declaration identity **2**.
No new product analysis domain, service, or runtime dependency was added by hardening.
[Release](RELEASE.md), [measurements](PERFORMANCE.md), [trust boundary](SECURITY.md),
and [previous phase](history/PHASE10.md) contain supporting details.

## Bounded query/status output and short skill workflow — 2026-10-10

Implemented CLI 0.10.0, query/status v5 and plugin 0.2.0. Query/status now project
stored analysis into dedicated output models, with compact summary JSON bounded
at 12,000 Unicode scalars including LF. Diagnostics occur once, with ordinal
three-message/300-scalar examples and explicit omissions; analysis limitations
remain full text. Resolved endpoints and related short project contexts survive
empty queries. Status samples ten projects by ID. Full details preserve every
original analysis diagnostic and project field; full plus explicit max-chars is
rejected. Whole-result pruning preserves at least one result/candidate and an
entire path, with accurate returned/truncation metadata and usable minimum errors.

SQLite schema 3 and stored graph records are unchanged; existing indexes were read
without updating them. Index/doctor stay v4 and context/export v1. The context
writer shares only serialization/counting code; its record selection is unchanged.
Skill entry files distinguish definition, semantic-relation and impact tasks;
help/doctor/status/freshness/context are conditional. Five skill/reference copies
match. No dependency, service, hashing optimization or index automation was added.
See [migration](MIGRATION-0.10.md), [query contract](QUERIES.md) and
[measured output](PERFORMANCE.md#querystatus-v5-output-measurements--2026-10-10).

Evidence is local under `.uographs/bounded-output/validation/`. Environment:
macOS 27.0.1 ARM64, SDK 10.0.201, runtime 10.0.5, Release binaries. Initial
sandbox VSTest invocations aborted before tests with SocketException (13);
explicitly permitted standard VSTest invocations ran separately. Build/test/pack
were kept separate from active semantic-oracle validation.

| Check | Actual result |
| --- | --- |
| Release build | PASS, zero warnings/errors; initial 22.40 s, implementation rebuild 4.12 s, test rebuilds 2.90 s / final 4.51 s |
| Unit VSTest | PASS 55/55, zero skipped, 7 s |
| New bounded-output integration cases | PASS 26/26, zero skipped, 24 s |
| Full integration | Initial run **122 PASS / 3 FAIL**, 125 total, zero skipped, 12m09s; three legacy unbounded-output invocations adapted without removing assertions |
| Final focused integration | PASS **29/29**, zero skipped, 55 s: all 26 new cases plus the three corrected real evidence/path tests |
| Live skill/template/plugin checks | PASS 17/17, 13.403 s; includes 12 artifact/installer checks and 14 real CLI reference examples |
| JSON schema subset | PASS 6 command envelopes + 6 wrong-version negatives; 84 measured summary/full/context envelopes also passed |
| License audit / CI package tests / release helper tests | PASS 143 locked entries / 10 tests / 2 tests |
| Real HelloGraph index | PASS 15 entities / 20 relationships / 23 occurrences; snapshot ec638832847c43d2846c3ed6cfd41712 |
| Real HelloGraph read workflow | PASS index/find/symbol/callers/callees/path/context/status; measurements have three process repeats |
| Isolated 0.10.0 package smoke | PASS: local-feed temporary tool-path install, help/doctor, trust refusal, fresh index, query/callers/callees/context/status; all v5 reads within 12,000 |
| Agent discovery/model E2E, Windows/Linux execution for 0.10 | NOT RUN; installed agents and other platforms were not exercised |
| Skill creator quick_validate.py | NOT RUN: PyYAML unavailable; repository skill linter and five Ruby YAML.safe_load checks passed |

The three initial integration failures were the Phase3 complete callee evidence
round-trip and the real ABP/UO.Mediator path checks. The round-trip now explicitly
requests full details; the indivisible long paths request a 50,000-character
budget. The six-step UO path additionally asserts that default budget failure
returns exit 2 with empty stdout. Existing evidence assertions were retained.
The final focused run passed all three adapted tests and all 26 new cases. A
second whole-suite run was NOT RUN; no product behavior changed after the full
run (only a Core comment and the three explicit test invocation policies).
The initial 122/125 run is not presented as a clean 125/125 run.

The controlled 1,930-diagnostic/32-project metadata fixture produced symbol/caller
summary output of **10,715 / 10,638** scalars. This is output regression evidence,
not application-analysis acceptance. Tests also preserve six occurrence records
as one reached target and expose evidence-limit truncation. Exact minimum and
one-below boundaries, escapes/Unicode/LF, oversized metadata/results, paths,
ambiguity/domain exits, empty/external results, partial/freshness guards and DB
immutability are covered. Manual status minimum retry/below check passed at 2,649.

Existing IslemKaydi was measured read-only (snapshot
`003729cdc2294eb88cf5da2d8b67b651`, 14 projects, 1,930 diagnostics). Main DB SHA-256
remained unchanged. Its 114 full fingerprint limitations make mandatory metadata
larger than 12,000: default symbol/callers correctly returned exit 2, empty stdout,
and minimum budgets **30,434 / 32,383**. With explicit 50,000, both returned one
record with evidence at **31,454 / 33,403** scalars; full was
**1,181,618 / 1,183,567**. This does not claim successful default-budget acceptance
for that real snapshot. Freshness remained `notChecked`; no re-index or hashing
check was run. Three repetitions, actual argv, snapshot/environment, exit, UTF-8
bytes and scalar counts are in measurement manifests. No token or general speed
improvement percentage is claimed.

Commands used (all read measurements preserve explicit DB paths):

```sh
dotnet build UO.Graphs.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/UO.Graphs.UnitTests -c Release --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=unit-permitted.trx' --results-directory .uographs/bounded-output/validation
dotnet test tests/UO.Graphs.IntegrationTests -c Release --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~BoundedOutput' --logger 'trx;LogFileName=bounded-permitted.trx' --results-directory .uographs/bounded-output/validation
dotnet test tests/UO.Graphs.IntegrationTests -c Release --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=integration-full.trx' --results-directory .uographs/bounded-output/validation
dotnet test tests/UO.Graphs.IntegrationTests -c Release --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~BoundedOutput|FullyQualifiedName~SemanticEvidenceSurvivesSQLiteAndSeparateCliProcess|FullyQualifiedName~Phase9AbpRealPackagePersistsContractsRepositoriesAndInferredCandidates|FullyQualifiedName~UoRealPackageEndpointServiceRequestHandlerChainSurvivesPathAndContext' --logger 'trx;LogFileName=final-focused.trx' --results-directory .uographs/bounded-output/validation
dotnet src/UO.Graphs.Cli/bin/Release/net10.0/uographs.dll index samples/HelloGraph/HelloGraph.slnx --full --trust-project --db .uographs/bounded-output/validation/hello.db
python3 -B scripts/test_skill.py --cli src/UO.Graphs.Cli/bin/Release/net10.0/uographs.dll --db .uographs/bounded-output/validation/hello.db --evidence .uographs/bounded-output/validation/skill-live.json
python3 -B scripts/check_contracts.py --db .uographs/bounded-output/validation/hello.db --index-raw .uographs/bounded-output/validation/index.json --evidence .uographs/bounded-output/validation/contracts
python3 -B scripts/measure_read_output.py --db .uographs/bounded-output/validation/hello.db --symbol 'HelloGraph.Domain.Greeter.Greet(string)' --find 'HelloGraph.Domain.Greeter.Greet' --evidence .uographs/bounded-output/validation/hello-read
python3 -B scripts/measure_read_output.py --db /Volumes/KioxiaDepom/Calismalarim/IslemKaydi/backend/.uographs/graph.db --symbol 1c71b97f2d8206e6d866be2e3b7b2e6bf52df8a3e3fa1b032b10900eef6d9acc --find App.DrivingSchool.DrivingExamQueryService.GetAsync --evidence .uographs/bounded-output/validation/islem-read
dotnet pack src/UO.Graphs.Cli -c Release --no-build --no-restore -m:1 -nr:false --output .uographs/bounded-output/validation/packages
python3 -B scripts/smoke_package.py .uographs/bounded-output/validation/packages --version 0.10.0 --evidence .uographs/bounded-output/validation/package-smoke
python3 -B scripts/audit_licenses.py --check
python3 -B scripts/test_ci_package.py
python3 -B scripts/test_release_tools.py
git diff --check
```

Local package `UO.Graphs.Cli.0.10.0.nupkg` SHA-256:
`4c1a7fce1eb260a5df1ceda8a5242bddce1437b5f46009f7b49cd67d772e4d35`.
The package verifier passed version/HEAD metadata/payload checks and both packaged
templates match source bytes. This was built from the uncommitted local tree;
nuspec HEAD is not proof of published source. The package contains STATUS just
before its smoke result was appended here. No release/upload claim is made.

Changed areas: Core read-output models/writer/shared JSON counting, SQLite query
result projection, CLI parser/help and version; integration contract/budget tests;
all five skill/reference copies and plugin catalog/version; measurement/package/
skill/contract scripts; current contract, migration, performance and status docs.
No commit, push, publication or installed-plugin replacement was performed.
Suggested commit: `feat: bound query output and streamline the uographs skill`.

## Earlier release acceptance snapshot (before 0.10)

This table records the earlier published/package validation. It does not validate
0.10 on Linux or installed agent sessions. The bounded-output record above gives
the current change's results. Historical failures retain their original scope.

| Check | Latest verified result |
| --- | --- |
| macOS ARM64 Release build | PASS, SDK 10.0.201; Copilot distribution validation build 22.56 s, zero warnings/errors |
| macOS standard VSTest | PASS **55/55 unit**, **99/99 integration**, zero skipped; integration 11m54s |
| Linux CI at `5b52a01` | PASS **55/55 unit**, **99/99 integration**, zero skipped; Release build, audit, pack, installed-tool fresh index/query/context passed |
| NuGet Trusted Publishing | PASS; verified 0.9.0 artifact pushed through OIDC on 2026-10-10 |
| Local Release tool-path package smoke | PASS; isolated install/help/doctor/trust refusal/fresh index/query/context |
| Temporary local manifest install/read smoke | PASS; its index NOT RUN in that historical invocation |
| Global-scope installation | NOT RUN; user installation was not changed |
| Windows / on-prem Azure execution | NOT RUN |
| Physical offline / physical disk exhaustion | NOT RUN |
| 1000-project analysis / peak process-tree RSS / true cache-cold measurement | NOT RUN |
| Copilot marketplace artifacts | PASS **12/12** artifact tests; both catalogs select the shared plugin **0.1.1** |
| Copilot CLI plugin installation / VS Code discovery / model E2E | NOT RUN; standalone Copilot CLI unavailable on PATH, live VS Code session not exercised |
| Codex skill packaging / installation | PASS on macOS; repository/template parity, installer negatives, actual-package extraction/install |
| Codex plugin installation / discovery | PASS on macOS, Codex CLI 0.162.1; local marketplace, isolated profile, installed/enabled plugin and user-scope discovery in two repositories |
| Model-driven Codex E2E | NOT RUN; model selection/answers remain separate from verified installation/discovery |

## Copilot marketplace for the shared agent plugin — 2026-10-10

The new `.github/plugin/marketplace.json` catalog exposes
`uographs@uographs` for Copilot and selects the same `./plugins/uographs`
package as Codex. Plugin version is now **0.1.1**; the .NET CLI remains **0.9.1**.
Skill wording is agent-neutral, with byte parity across both repository skills,
both standalone templates, and the shared plugin. No CLI source, runtime
dependency, service, MCP server, automatic restore/indexing, or permission
setting changed.

After the catalog is published to GitHub, the installation commands are:

```sh
copilot plugin marketplace add ugurozturk/UO.DotGraphs
copilot plugin install uographs@uographs
```

[Copilot installation](COPILOT-SKILL.md) also documents Agent Host chat commands,
local VS Code user `chat.pluginLocations`, and invocation as
`/uographs:uographs`. CLI installation and each repository's prepared graph
remain separate prerequisites. No user settings or global installation changed.
No commit, push, publication, or model request was made.

Commands run on macOS ARM64 with prepared SDK/assets (no restore):

```sh
python3 -B scripts/test_skill.py --evidence .uographs/copilot-plugin/artifacts.json
python3 -B scripts/test_ci_package.py
python3 -B scripts/audit_licenses.py --check
dotnet build UO.Graphs.slnx -c Release --no-restore -p:ContinuousIntegrationBuild=true -m:1 -nr:false -p:UseSharedCompilation=false
dotnet pack src/UO.Graphs.Cli -c Release --no-build --no-restore -p:ContinuousIntegrationBuild=true -m:1 -nr:false --output .uographs/copilot-plugin/packages
python3 -B scripts/smoke_package.py .uographs/copilot-plugin/packages --version 0.9.1 --evidence .uographs/copilot-plugin/package-smoke
git diff --check
```

| Check | Actual result |
| --- | --- |
| Release build | PASS **22.56 s**, zero warnings/errors |
| Skill/plugin artifact tests | PASS **12/12**, **0.147 s**; shared catalog identity/version/source and owner negatives, template parity and existing installer checks |
| Package gate tests / license inventory | PASS **10/10**, **0.015 s**; **143** locked package/version entries, no dependency changes |
| Installed NuGet package smoke | PASS local-feed install/help/doctor/trust refusal/fresh HelloGraph index/query/context; index subprocess **9.998 s** |
| Copilot CLI plugin installation | NOT RUN; `copilot` is absent on PATH. VS Code's bundled SDK package is not an available standalone CLI |
| Live VS Code discovery / model E2E | NOT RUN; documented loading routes and artifact tests do not establish live host acceptance |
| Codex plugin 0.1.1 live reinstall | NOT RUN; previous 0.1.0 installation/discovery evidence below remains historical |
| Full .NET suite / Linux / Windows / live CI | NOT RUN for this catalog/docs change; earlier results remain historical |
| Remote marketplace installation | NOT RUN; new catalog has not been pushed |

Evidence: `.uographs/copilot-plugin/artifacts.json` and
`package-smoke/commands.json`, with per-command stdout/stderr in that directory.
The validation NuGet package predates final doc/status updates and was not
published. Existing CI runs the extended artifact tests without adding a Copilot
runtime dependency.

Changed: `.github/plugin/marketplace.json`; shared plugin manifest/README and
skill/reference; both repository skills and standalone templates;
`scripts/test_skill.py`; README and docs/COPILOT-SKILL, CODEX-SKILL, ARCHITECTURE,
PRODUCT, STATUS.
Suggested commit: `feat(copilot): add marketplace for shared uographs plugin`

## Codex plugin marketplace distribution — 2026-10-10

The repository now exposes marketplace `uographs` and plugin `uographs@uographs`.
The portable plugin manifest starts at version **0.1.0**, independent of the CLI
NuGet version **0.9.1**. The plugin bundles the MIT license, documentation, and
byte-matched standalone Codex skill/reference. It adds no dependency, MCP server,
hook, setup skill, service, or automatic installation/restore/indexing.

The requested GitHub installation is:

```sh
codex plugin marketplace add ugurozturk/UO.DotGraphs
codex plugin add uographs@uographs
```

These commands become usable after the marketplace files are published remotely.
**Remote GitHub installation: NOT RUN**; this work did not commit, push or publish.
CLI installation remains a separate explicit NuGet step. Existing repository
skills, Python installer, NuGet templates, user configuration and global CLI
installation were preserved. The unpublished `uographs skill add codex` command
from the preceding conversation was removed, including only its new installer,
new tests, embedded resources, and package-smoke additions. The CLI source and
package-smoke script have no remaining diff against the starting commit.

Commands run on macOS ARM64 with prepared SDK/assets (no restore):

```sh
python3 -B scripts/test_skill.py --evidence .uographs/codex-plugin/artifacts.json
dotnet build UO.Graphs.slnx -c Release --no-restore -p:ContinuousIntegrationBuild=true -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/UO.Graphs.IntegrationTests -c Release --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~Phase10ApprovedIncrementalSkillWorkflowAcrossProcesses|FullyQualifiedName~BadArgumentsProduceOnlyStderr|FullyQualifiedName~HelpAndDoctorRunWithoutLoadingTarget' --logger 'trx;LogFileName=plugin-cli.trx' --results-directory .uographs/codex-plugin
dotnet pack src/UO.Graphs.Cli -c Release --no-build --no-restore -p:ContinuousIntegrationBuild=true -m:1 -nr:false --output .uographs/codex-plugin/packages
python3 -B scripts/smoke_package.py .uographs/codex-plugin/packages --version 0.9.1 --evidence .uographs/codex-plugin/package-smoke
python3 -B scripts/test_ci_package.py
python3 -B scripts/audit_licenses.py --check
git diff --check
```

Real Codex checks used only a child-process profile at
`.uographs/codex-plugin/codex-test-profile`; no user config/auth files were copied
or changed. The commands were executed with that child's `CODEX_HOME` configured
for the test directory, not the user's normal profile:

```sh
codex --version
codex plugin marketplace add /Volumes/KioxiaDepom/Calismalarim/UO.DotGraphs --json
codex plugin add uographs@uographs --json
codex plugin list --marketplace uographs --available --json
codex app-server generate-json-schema --out .uographs/codex-plugin/protocol
codex app-server --stdio
```

The fresh stdio process received `initialize`, `initialized`, then `skills/list`
with `forceReload: true` and two unrelated temporary repository directories.
Both returned `uographs:uographs`, `scope: user`, `enabled: true`,
`pluginId: uographs@uographs`, the installed cache path, and no discovery errors.
No model turn was invoked and no source/graph was sent to a model. This is local
installation/discovery evidence, not model-driven acceptance.

| Check | Actual result |
| --- | --- |
| Release build | PASS **5.29 s**, zero warnings/errors |
| Artifact tests | PASS **10/10**; includes marketplace/portable manifest, plugin/template parity, and seven negative package mutations |
| Focused Release integration tests | PASS **8/8**, zero skipped, **32 s**; existing argument/help/doctor and incremental skill workflow |
| Real Codex marketplace/plugin add | PASS, CLI **0.162.1**; plugin **0.1.0**, installed and enabled in isolated profile |
| Fresh Codex skill discovery | PASS in **two unrelated directories**; plugin skill `uographs:uographs` at user scope |
| Installed NuGet package smoke | PASS local-feed install/help/doctor/trust refusal, fresh HelloGraph index (**15 entities**, index subprocess **9.854 s**), separate query/context |
| Package gate tests / licenses | PASS **10/10**; **143** locked package/version entries, no dependency changes |
| Full .NET suite / Linux / Windows / live CI | NOT RUN for this distribution change; earlier results above remain historical |
| Model-driven Codex E2E | NOT RUN; isolated profile has no copied authentication, no model request was made |
| Remote marketplace install | NOT RUN; local files were not pushed |

Evidence: `.uographs/codex-plugin/artifacts.json`, `plugin-cli.trx`,
`marketplace-add.json`, `plugin-add.json`, `plugin-list.json`, `discovery.json`,
and `package-smoke/commands.json`. The test NuGet package predates final doc/status
updates and was not published. The existing CI artifact-test step automatically
runs the extended plugin checks; no Codex runtime dependency was added to CI.

Changed: `.agents/plugins/marketplace.json`; `plugins/uographs/` (manifest, README,
license, skill/reference); `scripts/test_skill.py`; README; docs/CODEX-SKILL,
ARCHITECTURE, PRODUCT, STATUS.
Suggested commit: `feat(codex): distribute uographs through a plugin marketplace`

## Codex skill support — 2026-10-10

User-requested Codex support adds `.agents/skills/uographs/` and its byte-matched
`templates/codex/uographs/` distribution, each containing SKILL.md and the CLI
reference. The existing installer accepts `--agent codex`; its default remains
Copilot. It preserves existing skill directories, AGENTS.md, and Copilot content,
and rejects symlinks at every Codex destination level, including dangling links.
No personal/global skill or permission setting was changed.

The .NET tool package includes the Codex template; both package gates require it
and the installer. CI now runs the skill artifact tests. This is an optional CLI
consumer extension, not a new analysis phase or runtime dependency. Version stays
0.9.0; the local validation package was not published and does not replace the
previously published artifact. Repository skill discovery follows the dated
[official documentation](https://learn.chatgpt.com/docs/build-skills); actual Codex
selection and model behavior remain **NOT RUN**. See [CODEX-SKILL](CODEX-SKILL.md).

Commands run on macOS ARM64 with prepared SDK/assets (no restore):

```sh
dotnet build UO.Graphs.slnx -c Release --no-restore -p:ContinuousIntegrationBuild=true -m:1 -nr:false -p:UseSharedCompilation=false
python3 -B scripts/test_skill.py --db .uographs/docs-english/validation/graph.db --evidence .uographs/codex-skill/reads.json
python3 -B scripts/test_ci_package.py
dotnet test tests/UO.Graphs.IntegrationTests -c Release --no-build --no-restore -m:1 -nr:false --filter FullyQualifiedName~Phase10ApprovedIncrementalSkillWorkflowAcrossProcesses --logger 'trx;LogFileName=skill-workflow.trx' --results-directory .uographs/codex-skill
dotnet pack src/UO.Graphs.Cli -c Release --no-build --no-restore -p:ContinuousIntegrationBuild=true -m:1 -nr:false --output .uographs/codex-skill/packages
python3 -B scripts/verify_ci_package.py .uographs/codex-skill/packages --version 0.9.0 --commit b4c9c0d937b67274888751e73f4137976f41fe4c
python3 -B scripts/audit_licenses.py --check
python3 -B scripts/install_skill.py --help
git diff --check
```

| Check | Actual result |
| --- | --- |
| Release build | PASS **23.54 s**, zero warnings/errors |
| Skill artifacts / real CLI reads | PASS **13/13**, **14.803 s**, **48 CLI subprocesses**; prepared Debug binary and actual existing HelloGraph snapshot |
| Package gate tests | PASS **10/10**, including missing Codex template/reference/installer negatives; synthetic test packages |
| Release incremental skill integration | PASS **1/1**, zero skipped, **16 s**; separate-process index/update/status/find/context/callers |
| Actual local package | PASS package identity/layout and source parity; extracted installer successfully installed both Codex files into a temporary repository |
| License inventory | PASS **143** locked package/version entries; no dependency change |
| YAML / docs / whitespace | PASS Ruby YAML.safe_load for both new frontmatters, stdlib skill lint, eight documents / 63 local links / balanced fences, git diff --check |
| Bundled skill-creator quick_validate.py | NOT RUN: Python PyYAML unavailable (ModuleNotFoundError); no package installed; independent YAML parse and repository lint passed |
| Full .NET suite / Linux / Windows / live CI / Codex E2E | NOT RUN for this change; earlier results above remain historical evidence |

Evidence: `.uographs/codex-skill/reads.json`, `skill-workflow.trx`, `package.json`,
and `packages/UO.Graphs.Cli.0.9.0.nupkg`. Package SHA-256:
`c9317d932b029495ca5c586c138aff3de5e8777a4558553751382b3d78137ec9`.
The temporary extraction check used Python zipfile, copied only the packaged
installer/Codex template, executed the extracted installer with `--agent codex`,
and compared installed bytes to the source template. The package was built from
an uncommitted tree before this STATUS record; its repository commit metadata
is not proof that the new skill was committed or published.

Existing English documentation changes were preserved; commit `b4c9c0d` appeared
during this task. The agent did not commit/push/publish.

Changed: `.agents/skills/uographs/{SKILL.md,references/cli.md}`;
`templates/codex/uographs/{SKILL.md,references/cli.md}`; `scripts/install_skill.py`,
`test_skill.py`, `test_ci_package.py`, `verify_ci_package.py`, `verify_release.py`;
`src/UO.Graphs.Cli/UO.Graphs.Cli.csproj`; `.github/workflows/ci.yml`; README;
`docs/CODEX-SKILL.md`, PRODUCT, ARCHITECTURE, STATUS.
Suggested commit: `feat(skills): add Codex skill and safe repository installation`

## English documentation refresh — 2026-10-10

README and all Markdown under docs, including phase history, were reviewed against
current code, settings, workflow definitions, and existing evidence. The generated
DEPENDENCIES inventory was already English and remains unchanged so the license
generator's --check continues to reproduce it. The other 28 documents were rewritten
in English, preserving dated results, failures, and limitations. Existing uncommitted
STATUS additions about the SDK fixture fix and CI were retained in English below.

Corrected stale current-document claims include JSON/SQLite versions, tool packaging,
query freshness flags, the complete framework dependency set, Linux CI status, and
NuGet publication. Historical versions/counts are explicitly labeled as historical.
No product code, tests, lock files, workflows, skills, or AGENTS were changed.
Documentation-specific validation results are recorded at the end of this document.

### Live CI and publication refresh

Read-only GitHub inspection on 2026-10-10 verified
[build-test-pack 37995066871](https://github.com/ugurozturk/UO.DotGraphs/actions/runs/37995066871)
for commit `5b52a01f68003232e612c114bb709783b2338a0e`: **completed / success**.
The Ubuntu 24.04 job completed at **2026-10-09 22:05:58 UTC**. Release build took
**50.88 s**; tests passed **55/55** (18 s) and **99/99** (21m9s), zero skipped.
License/release-tool checks, pack, package verification, isolated actual-package
install and fresh index/query/context, and artifact upload all succeeded.
This replaces the prior last-observed `in_progress` status; it does not validate Windows.

[Publish 37997326843](https://github.com/ugurozturk/UO.DotGraphs/actions/runs/37997326843)
for the same source commit completed successfully. Logs confirmed artifact identity/
commit/hash verification, successful OIDC token exchange, and an actual package push
at **2026-10-10 06:21:31 UTC**, not a duplicate-version skip. Published nupkg SHA-256:
`f800123c914e32fb5f2827c789350966fae885171c5ef87093e1976875548afb`.
The documentation agent inspected existing runs; it did not trigger CI or publish.

Read-only commands used:

```sh
gh run view 37995066871 --repo ugurozturk/UO.DotGraphs --json status,conclusion,headSha,jobs,url
gh run view 37995066871 --repo ugurozturk/UO.DotGraphs --log
gh run list --repo ugurozturk/UO.DotGraphs --workflow publish-nuget.yml --limit 3 --json databaseId,status,conclusion,headSha,url
gh run view 37997326843 --repo ugurozturk/UO.DotGraphs --log
```

## Shared fixture SDK selection — 2026-10-10

[Linux CI 37991790585](https://github.com/ugurozturk/UO.DotGraphs/actions/runs/37991790585)
had **55/55 unit PASS**, **94 integration PASS / 4 FAIL**. Full logs included
`SDK outside the validated base-SDK dependency model`. Besides generator startup,
three Phase6 failures shared an incomplete fingerprint: Complete false,
require-fresh rejecting unknown, and rebuilding instead of no-op.

The generator-fix commit `1bc8e5e` was then inspected read-only in
[CI 37994084225](https://github.com/ugurozturk/UO.DotGraphs/actions/runs/37994084225):
**55/55 unit PASS**, **95 integration PASS / 3 FAIL**, 8m9s. Generator cancellation
passed; the three Phase6 failures remained. That run did not contain the shared
CopyFixture SDK fix.

CopyFixture previously copied only samples/Phase2 into a temporary directory,
leaving repository global.json behind. On multi-SDK runners, the temporary target
could resolve a different SDK. The helper now copies the repository pin to the
fixture root, preserving the generator fixture's earlier pin. Product dependency
completeness checks, freshness expectations, and timeout bounds were not weakened.

`CopiedFixtureSelectsRepositorySdkOutsideCheckout` creates a conflicting **99.0.100**
SDK request in a temporary parent and checks actual dotnet --version selection.
The regression failed before the fix (`sdk-selection-red.trx`) because resolution
used the parent request. An earlier test-authoring failure due to missing temporary
directory setup was corrected and is not counted as regression evidence. Phase6
Complete assertions now show limitation reasons; the mutation oracle also asserts
initial snapshot completeness.

Recorded local macOS ARM64 / SDK 10.0.201 evidence: `.uographs/ci-sdk-fixture-fix/`.

```sh
dotnet build UO.Graphs.slnx -c Release --no-restore -p:ContinuousIntegrationBuild=true -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/UO.Graphs.UnitTests -c Release --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=unit.trx' --results-directory .uographs/ci-sdk-fixture-fix
dotnet test tests/UO.Graphs.IntegrationTests -c Release --no-build --no-restore -m:1 -nr:false --logger 'console;verbosity=normal' --logger 'trx;LogFileName=integration.trx' --results-directory .uographs/ci-sdk-fixture-fix
dotnet msbuild samples/HelloGraph/HelloGraph.Domain/HelloGraph.Domain.csproj -nologo -getProperty:MSBuildToolsPath,MSBuildExtensionsPath,MSBuildSDKsPath
```

- Build PASS **17.31 s**, zero warnings/errors.
- Unit PASS **55/55**, zero skipped, **6 s**.
- Integration PASS **99/99**, zero skipped, **11m54s**. New SDK regression and all
  four originally failing cases passed. Mutation oracle **5m2s**, implicit import
  **21 s**, freshness **31 s**, generator cancellation **3 s**. Includes actual CLI
  index/separate-process query; no build/pack overlapped the suite. The extra test
  accounts for the count increasing from 98 to 99.
- The explicit user-authorized MSBuild property query passed without restore/build
  targets: tools/extensions `/usr/local/share/dotnet/sdk/10.0.201`, SDKs beneath its
  Sdks directory. This was a macOS observation.
- `git diff --check`: PASS.

During tests, commit `5b52a01` appeared in the working tree and retained the test
fixes; the agent did not commit/push/publish. Final results were added to uncommitted
STATUS. Linux was last observed in progress then; the completed result is above.
Windows remains NOT RUN. No new dependency/restore.
Changed: Phase2Tests.cs, Phase6Tests.cs, STATUS.
Suggested commit: `fix(tests): preserve pinned SDK in copied integration fixtures`

## Generator cancellation CI test — 2026-10-10

The earlier Linux output had 55/55 unit passing, but the integration test never saw
the generator-start marker within 90 s. It did not reach cancellation assertions,
so this was not evidence that cancellation itself failed. The old test passed
locally in normal, DOTNET_PROCESSOR_COUNT=2, and =1 runs, 1/1 each; Linux timeout
was not locally reproduced.

The test was moved to a nonparallel xUnit collection to avoid process-wide MSBuild
state collisions, copied global.json outside the checkout, ran the real indexer
on a separate task from xUnit's synchronization context, and added timed progress
to output/timeouts. The 90 s start, 10 s cancellation, 20 s cleanup bounds and
all token/observation/exception assertions stayed intact. Product code did not change.

Evidence: `.uographs/generator-ci-fix/`. Release build with the same flags above
passed **23.65 s**, zero warnings/errors.

```sh
DOTNET_PROCESSOR_COUNT=2 dotnet test tests/UO.Graphs.IntegrationTests -c Release --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~Phase11GeneratorCancellationTests|FullyQualifiedName~GeneratorsNeedPermissionAndGeneratedPolicyDoesNotRunDiagnosticAnalyzers|FullyQualifiedName~NewlyCreatedImplicitUserImportIsStaleAndRebindsCompilation|FullyQualifiedName~RealSolutionIndexPersistsTypeAndMethodAcrossProcesses' --logger 'console;verbosity=normal' --logger 'trx;LogFileName=focused.trx' --results-directory .uographs/generator-ci-fix
DOTNET_PROCESSOR_COUNT=1 dotnet test tests/UO.Graphs.IntegrationTests -c Release --no-build --no-restore -m:1 -nr:false --filter FullyQualifiedName~Phase11GeneratorCancellationTests --logger 'console;verbosity=normal' --logger 'trx;LogFileName=single-cpu.trx' --results-directory .uographs/generator-ci-fix
```

Two-CPU: **4/4 PASS**, zero skipped, **69.88 s**, generator **3.749 s**; includes real
HelloGraph index/query/status, permission policy, and implicit user import. One-CPU:
**1/1 PASS**, zero skipped, **6.07 s**. TRX contained timed fingerprint/invalidation/
workspace progress. Diff check passed. Full suite/Linux were not rerun during that
specific change; later results above supersede that historical gap. No dependency,
restore, or agent commit/push/publish. Changed Phase11GeneratorCancellationTests and
STATUS. Suggested commit:
`fix(tests): isolate generator cancellation acceptance from runner state`

## GitHub/NuGet automation and exact version — 2026-10-09

User-requested workflows added build/test/pack and NuGet Trusted Publishing under
uozturk. Main-push success publishes the exact verified artifact; PR/manual CI does
not. No permanent API key. Policy/release instructions are in [RELEASE](RELEASE.md).
Live OIDC/publication was NOT RUN during implementation; later success is above.

CI uses Ubuntu 24.04, pinned SDK, locked restore, Release VSTest, license checks, and
fresh installed-package smoke. Publishing verifies run ID/package/version/commit/
SHA-256 without rebuilding. Upload scope is nupkg/manifest/TRX, not raw graphs.
Version is exactly Directory.Build.props `<Version>0.9.0</Version>`; no run-number
suffix. Help and new graph toolVersion read assembly version. Tests select CLI and
contract fixture binaries from their own build configuration; target analysis still
defaults to Debug. The generator fixture's explicit Debug reference is built separately.

### Exact-version validation

Local macOS ARM64 evidence: `.uographs/static-version/`.

| Check | Result |
| --- | --- |
| `python3 -B scripts/test_ci_package.py` | PASS **9/9**, exact props version across CI-run values and invalid-version rejection |
| `python3 -B scripts/verify_ci_package.py --print-version` | **0.9.0** |
| Release build, same command above without Version override | PASS **21.93 s**, zero warnings/errors |
| `dotnet pack src/UO.Graphs.Cli -c Release --no-build --no-restore -p:ContinuousIntegrationBuild=true -m:1 -nr:false --output .uographs/static-version/packages` | PASS, UO.Graphs.Cli.0.9.0.nupkg |
| Verifier --version 0.9.0 --commit HEAD, write/verify manifest | PASS; SHA-256 `5d236ea8e24d83f13480d5dc39e2aface9cbf35fa93c3d8391285f9de643aa3a` |
| `python3 -B scripts/smoke_package.py .uographs/static-version/packages --version 0.9.0 --evidence .uographs/static-version/smoke` | PASS; isolated install/help/doctor/trust refusal, fresh index **8.357 s / 15 entities**, query/context; help/graph version 0.9.0 |
| Two YAML parses, 11 shell blocks with bash -n, git diff --check | PASS |

Full .NET suite was not repeated for this version-only workflow correction. New
versions require manually changing props; duplicates are skipped. Changed workflows,
verifier/package tests, README/RELEASE/STATUS. Suggested commit:
`fix(ci): use the exact version from Directory.Build.props`

### Earlier local 0.9.1 experiments

**0.9.1 was an explicit local test override, not the configured/published version.**
Evidence: `.uographs/trusted-publishing/`. Build/test/pack used `-p:Version=0.9.1`.

| Check | Result |
| --- | --- |
| Release build | PASS **22.70 s**, zero warnings/errors |
| Standard Release unit | PASS **55/55**, zero skipped, **6 s** |
| Standard Release integration before final version-output fix | PASS **98/98**, zero skipped, **11m23s**; no concurrent build/pack |
| Pack | PASS, **218 entries**, local 0.9.1 package |
| Manifest/identity/MIT/commit/payload/exclusion verifier | PASS |
| First actual-package smoke | PASS; install/help/doctor/trust exit 3/fresh index **10.070 s / 15 entities**/query/context |
| test_ci_package / test_release_tools | PASS **8/8 / 2/2**; synthetic negatives not substitutes for installation |
| License audit | PASS **143** |
| Three CI restore targets in clean source copy, locked mode and NuGetAudit=false | PASS using local cache, not live vulnerability audit; CI does not disable audit |
| YAML/11 shell blocks/Python AST/diff | PASS, not live OIDC evidence |

After the final version-output source fix and completion of the full suite:
Release build **8.89 s**; unit **55/55** in **7 s**; focused real-index/help-doctor
integration **2/2** in **12 s**, including version assertions. The entire 98-case
suite was not repeated after that final string change. Final package manifest
SHA-256 `b7c0a48c27fc393af532c31f965791ad19f0b68c6c8e8cfc40683cf95d528f24`.
Final local-feed install **0.791 s**, fresh index **8.593 s / 15 entities**, help/graph
0.9.1, separate query/context all passed. License/diff/local links passed.

These packages came from a then-uncommitted tree; the nuspec commit was HEAD, not
proof of a published commit/package. CI later verified a clean checkout/artifact.
Changed workflows, verify/smoke/test_ci_package, CLI metadata/Program,
Core ProductVersion, Roslyn indexer, CLI/Phase6/Messaging tests and release documents.
Suggested commit:
`ci: publish verified UO.Graphs tool packages with NuGet trusted publishing`

## Phase 11 hardening and performance evidence

Implemented a controlled 10/100/1000-project generator; measured 10 and 100, but
1000 analysis NOT RUN. Same-TFM, diagnostic-free, generator-disabled solutions share
one workspace/analyzer-free immutable Solution. Other load paths remain separate;
initial shared-load failure is not retried for every project. Reference context
comparison now includes aliases/EmbedInteropTypes, module MVID, and recursive
compilation references. No selective project/method incremental reuse.

Context pruning favors nearby flow/framework evidence and retains uncertainty;
Refit's 50,000-character regression passed without relaxing its budget/assertions.
Preparation subprocess output/timeouts cover pipe drain after parent exit. Real
parent/child and cooperative-generator cancellation tests, SQLITE_FULL rollback,
corrupt config/non-DB, sidecar symlinks, SIGINT/SIGKILL/contention, and exclusive
export helpers were added. Tool packaging includes licenses and standalone scripts;
the self-hosted Azure example adds no hosted service. Skill/template parity and
real reads passed; Copilot E2E remains NOT RUN.

Measurements used macOS **27.0.1 ARM64**, **24 logical CPUs / 64 GiB RAM**, SDK
**10.0.201**, runtime **10.0.5**. CPU model/process inventory permissions were denied.
Raw evidence: `.uographs/phase11-evidence/`; detailed methodology in [PERFORMANCE](PERFORMANCE.md).

| Scenario | Fingerprint ms | Analysis + validation ms | Result |
| --- | ---: | ---: | --- |
| 10 projects, full before sharing | 12667 | 27338 | PASS, 401/1305/1335 records |
| 10 projects, full after sharing | 13360 | 16129 | PASS, equal graph contents |
| 10 projects, warm | 14356 | 0 | PASS noOp / zero projects |
| 10 projects, leaf edit | 13903 | 17035 | PASS, all ten rebuilt |
| 10 projects, dependency edit | 13933 | 16334 | PASS, all ten rebuilt |
| 100 projects, full | 122481 | 136951 | PASS, 4001/13635/13935 |
| 100 projects, warm | 111733 | 0 | PASS noOp / zero projects |

One successful repetition per index scenario. Timers exclude startup/SQLite commit;
OS cache was not flushed and concurrent load was uncontrolled, so no percentage
improvement claim. SQLite sizes **6,631,424 / 53,084,160 bytes**. Three read repeats
per operation: 10-project **0.118–0.225 s**, 100-project **0.213–0.218 s** with 50,000
character context, including new processes and 100 ms polling. A 12,000 budget on
100 projects correctly returned exit 2 for a 27,146-character minimum envelope.
Peak process/child RSS and token savings were not measured.

### Initial Phase 11 validation

| Recorded command/check | Result |
| --- | --- |
| `dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` | PASS, final **12.83 s**, zero warnings/errors |
| Unit VSTest release-unit.trx | PASS **52/52**, zero skipped, about 5 s |
| Final-binary focused integration release-integration.trx | PASS **16/16**, 48 s; real index/trust/config/storage/Phase9/context |
| Interim full-integration.trx | **94 PASS / 3 FAIL**, 97 total, **11m25s** |
| Stable-binary final-incremental-oracle.trx | PASS **1/1**, **5m1s** |
| context-regression.trx | PASS **3/3**, 26 s; real ABP/EF/Refit integrations, closing their initial Phase9 assertion gate |
| final-focused.trx | PASS **11/11**, 16 s; context/TFM/config/SQLite/symlink/non-DB |
| hardening.trx | PASS **14/14**, 539 ms; storage/rollback/concurrency |
| regression.trx | PASS **8/8**, 20 s; HelloGraph/trust/Phase6 |
| test_process_safety.py | PASS twice: SIGINT **130**, SIGKILL **-9**, competing writer **10**, real DB backup preserved |
| test_skill.py against phase11-live.db | PASS **10/10**, **14.168 s** |
| test_release_tools.py / audit_licenses.py --check | PASS **2/2 / 143 packages** |
| check_contracts.py on perf-direct-100-final.db and optimized-direct-10.raw | PASS **6 actual envelopes + 6 wrong-version negatives**; validator checks only used schema keywords |
| Source/config/document checks | PASS: five original HelloGraph files retained initial hashes; AST/links/whitespace/YAML parse |
| Independent correctness/security reviewers | Two read-only reviews; context/reference/cancellation/harness issues corrected |

### Follow-up validation

Evidence: `.uographs/phase11-followup/commands.json` and vstest/. Product behavior
was unchanged while tests were added/strengthened.

| Check | Result |
| --- | --- |
| Same build, final test sources | PASS **14.31 s**, zero warnings/errors |
| followup-final-unit.trx | PASS **55/55**, zero skipped; actual subprocess tests |
| followup-full-integration.trx | PASS **98/98**, zero skipped, **11m35s**; generator **4.994 s** |
| followup-final-full-integration.trx | PASS **98/98**, zero skipped, **11m34s**; no concurrent build/pack |
| Separate focused generator invocations | ABORTED before tests, VSTest SocketException (13); distinct from passing full suites |
| Release tools / licenses | PASS **2/2 / 143** |
| Updated skill/template real reads | PASS **10/10**, **12.831 s** |
| Documentation | PASS, 29 Markdown files, three archived relative links fixed; AST/whitespace/parity |
| Temporary local manifest install/help/query/context | PASS, isolated CLI home/cache/local feed; existing repo DB, index NOT RUN |
| Follow-up review | Pipe scheduling false-pass and cleanup timeout fixed; no critical remaining source-review finding |

Final unit/full integration covered strengthened handshake/cleanup tests. Product
DLL and five HelloGraph file comparisons are in stable-inputs-final-build.json and
current-package-parity.json. Interim failures were Refit context pruning (fixed,
3/3 repeat), a rebuild during mixed-TFM indexing (guard correctly rejected, 11/11
repeat), and pack changing tool publish binaries during the oracle (guard rejected,
stable repeat 1/1). No test/assertion was removed/weakened. The final total increased
with a new test; 94/97 is not presented as 98/98.

Real WAL-publication/process kills, storage cancellation, literal SQL/shell input,
snippet/traversal/budgets, and concurrent readers/writers passed. SQLite code 13 was
real fault injection, not physically filling a disk. The trusted custom target wrote
a controlled marker only with trust, illustrating rather than replacing OS isolation.

### Historical package/IPC evidence

Initial Debug pack used no-build/no-restore, single worker. The first archive had
187 entries / 46,219,482 bytes; delivery archive **218 / 46,350,948 bytes**, SHA-256
`4180eb28775f4c1fd0179159194e493de83933514899802c73feb4529caf7041`, under
`.uographs/phase11-evidence/delivery-feed/`; manifest package-final.json.
BuildHost/native SQLite payload and ABP exclusion were audited; skill overwrite
refusal passed. A later documentation/skill archive under phase11-followup/feed/
has entry/hash/DLL/document parity in package.json and read smoke in
refreshed-tool/commands.json. Earlier archives remain historical artifacts.

Local-feed isolated tool-path installs and help/query/context passed, but their
initial packaged index failed **exit 5**, workspace IPC timeout, **63.939 s** in
delivery-tool-smoke/raw.jsonl. Read smoke used a repo-created DB and did not prove
package indexing. Separate bounded package/repo BuildHost probes both started runtime
10.0.5 and failed named-pipe bind with **SocketException (13), exit -6**. Their
**20/20 files matched SHA-256**, recorded in
phase11-followup/buildhost-probe-locale.json. No permission bypass or missing-payload
claim followed. Later permitted Release and Linux package success is recorded above.
Direct repo HelloGraph full smoke was **15 / 20 / 23**, read in separate processes.

## Remaining gates and backlog

- Windows, on-prem Azure, and global-scope installation remain NOT RUN. Linux CI
  is now verified and must not be listed as unexecuted. Temporary manifest indexing
  remains unverified for that invocation; tool-path indexing has later passes.
- Physical offline, physical disk exhaustion, peak process-tree RSS, true OS-cache-
  cold runs, 1000-project analysis, and 100-project leaf/dependent runs remain NOT RUN.
- Cooperative generators/post-exit pipes passed; forced malicious-generator stopping
  and reparented-child containment require external isolation and are unverified.
- Some restricted Python/redirected/DLL workspace invocations timed out; permitted
  direct successes do not erase them. A historical `kill -INT 62720` was denied.
  Follow-up lsof found no process entry, and the old perf-100.db writer lease was
  acquired/released nonblocking (prior-lease.json); no repeat signal was sent.
- Real Copilot E2E remains NOT RUN. File/CLI checks are not model-session acceptance.
- Git metadata was absent in initial Phase11; later automation work had repository/
  origin and diff checks. AGENTS and user changes remain preserved. This agent made
  no commit/push/publish; the independently triggered successful publication is above.
- Backlog: broader permitted agent/platform/offline matrix, process-tree RSS,
  1000-project baseline, metadata-envelope growth, and reducing hashing/per-project
  evaluation cost without weakening correctness.

Historical Phase11 changed areas: Roslyn indexer/preparation, Core DeliveryWriter,
SQLite GraphStore/AssemblyInfo, CLI package metadata; Phase11 unit/integration/process/
generator tests; synthetic_solution, benchmark, verify_release, safe_export,
test_release_tools, test_process_safety, check_contracts; on-prem pipeline, README,
architecture/delivery/release/performance/security/status/history/schema and skill
references. Suggested commit:
`fix: harden indexing and package the uographs CLI with release evidence`

## Documentation refresh validation

All checks below ran on macOS ARM64 / SDK 10.0.201 for this documentation change.
Local evidence is under `.uographs/docs-english/validation/`. The complete integration
suite was **NOT RUN in this documentation-only pass**; the separate earlier 99/99
macOS and Linux runs remain identified above. No restore, package publication,
commit, or push was performed by this documentation task.

```sh
dotnet build UO.Graphs.slnx -c Release --no-restore -p:ContinuousIntegrationBuild=true -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/UO.Graphs.UnitTests -c Release --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=docs-unit.trx' --results-directory .uographs/docs-english/validation
dotnet test tests/UO.Graphs.IntegrationTests -c Release --no-build --no-restore -m:1 -nr:false --filter 'FullyQualifiedName~RealSolutionIndexPersistsTypeAndMethodAcrossProcesses|FullyQualifiedName~HelpAndDoctorRunWithoutLoadingTarget|FullyQualifiedName~TrustIsRequiredBeforeMalformedProjectIsEvaluated|FullyQualifiedName~MissingAssetsFailsWithoutRestoringOrCreatingDatabase|FullyQualifiedName~Phase10ApprovedIncrementalSkillWorkflowAcrossProcesses|FullyQualifiedName~CliTests.Phase9' --logger 'trx;LogFileName=docs-integration.trx' --results-directory .uographs/docs-english/validation
dotnet run --project src/UO.Graphs.Cli -c Release --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --full --trust-project --db .uographs/docs-english/validation/graph.db
python3 -B scripts/test_skill.py --db .uographs/docs-english/validation/graph.db --evidence .uographs/docs-english/validation/skill-read-tests.json
python3 -B scripts/phase5_smoke.py --db .uographs/docs-english/validation/graph.db --output-dir .uographs/docs-english/validation/context-export
python3 -B scripts/audit_licenses.py --check
git diff --check
```

| Check | Actual result |
| --- | --- |
| Release build | PASS, **22.68 s**, **0 warnings / 0 errors** |
| Standard Release unit tests | PASS **55/55**, zero skipped, **6 s** |
| Focused Release integration tests | PASS **8/8**, zero skipped, **54 s**: basic CLI/trust/assets, approved incremental workflow, and real ABP/EF/Refit cases |
| Real Release CLI index | PASS, **15 entities / 20 relationships / 23 occurrences**, two projects; fingerprint **3492 ms**, analysis/validation **5113 ms** |
| Existing skill/README tests | PASS **10/10**, **10.766 s**, including real parser/read subprocesses and installer negatives |
| Existing context/export smoke | PASS, **17 CLI calls**, expected negative exits, all four export formats, deterministic repeat, source hash checks, unchanged DB bytes |
| License inventory | PASS **143** locked package/version entries; generated English inventory unchanged |
| Markdown checks | PASS: **29 files**, **139 local links/heading targets**, balanced fences, **36 shell blocks** parsed with bash -n; upstream proper-name attribution retained |
| Scope and whitespace | PASS: **28 changed Markdown files**, all within README/docs; git diff --check clean |

Standalone smoke snapshot: `e94a504000c24445affa764a59017b8b`. Coverage remains
**partial** because generators were disabled; index freshness was fresh and later
reads default to notChecked. The two existing Python scripts use the prepared
**Debug CLI** for their read/parser checks; the build, VSTest, and standalone index
above used **Release**. Their results are not presented as one launcher/configuration.
Context measured **10,398 / 12,000 characters**, with the same Welcome → Greet
source evidence as the untruncated **63,736-character** full export. This is only
that fixture/task's character measurement, not a token or general savings claim.

The initial documentation character scan flagged the upstream author name
Günther Foidl in DEPENDENCIES. It was reviewed and preserved as attribution, not
translated or removed. The first read-only shell validation could not create a
heredoc temporary file; the permitted rerun completed all checks. These were
validation-environment/heuristic issues, not product test failures.

Changed files: README; the 17 non-generated top-level docs Markdown files;
all ten docs/history phase records. DEPENDENCIES, AGENTS, source/test code,
workflows, package/lock files, and skill/template files are unchanged.
Suggested commit: `docs: rewrite documentation in English and refresh verified status`
