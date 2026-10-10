# Distribution and release readiness

Package **UO.Graphs.Cli**, command **uographs**, source version **0.11.0**. Query/status envelope 6, index/doctor 4,
SQLite 3, and context/export 1 are unchanged. Phase 11 hardened the existing scope;
it added no new analysis domain or hosted service.

As verified on **2026-10-10**, [Linux CI](https://github.com/ugurozturk/UO.DotGraphs/actions/runs/37995066871)
passed build, **55 unit / 99 integration** tests, license checks, pack, and real
installed-tool index/query/context. The
[publishing run](https://github.com/ugurozturk/UO.DotGraphs/actions/runs/37997326843)
exchanged an OIDC token and successfully pushed **0.9.0** to NuGet. Local macOS
Release package smoke also passed. Publication does not close the remaining
Windows, physical offline, performance/memory, or Copilot gates in [STATUS](STATUS.md).

## GitHub Actions and NuGet Trusted Publishing

Two checked-in workflows implement delivery:

- [build-test-pack](../.github/workflows/ci.yml): main push, pull request, or manual
  invocation. Ubuntu 24.04 uses the pinned SDK, locked restore, Release build,
  standard unit/integration tests, licenses, and package checks. It packs with
  --no-build --no-restore, installs from a local feed into a new tool-path/cache,
  then requires trust refusal and real index/query/context on a **new database**.
- [publish-nuget](../.github/workflows/publish-nuget.yml): accepts only successful
  first-party main-push CI from ugurozturk/UO.DotGraphs. PR/fork/manual CI does not
  publish. It downloads the artifact from that exact run ID, verifies identity,
  version, source commit, and SHA-256 manifest, then uses NuGet/login@v1 and pushes
  the same nupkg. It does not restore/build/pack or execute artifact scripts.

The packages-and-test-results artifact retains nupkg, manifest, and TRX for 14 days.
Graphs, .uographs, raw context/index evidence, and local user files are not uploaded.
CI analyzes its own synthetic samples. This does not add runtime telemetry/upload.

### NuGet policy

The repository's successful publication demonstrates the configured OIDC path.
When recreating or reviewing its [NuGet policy](https://www.nuget.org/account/trustedpublishing),
use these workflow-aligned values:

| Field | Value |
| --- | --- |
| Policy owner / workflow user | `uozturk` |
| Repository owner | `ugurozturk` |
| Repository | `UO.DotGraphs` |
| Workflow file | `publish-nuget.yml` (filename only) |
| Environment | Empty; the workflow has no GitHub Environment |
| Package glob | `UO.Graphs.Cli` |
| Scopes | Create packages and publish new versions |

The workflow user is a NuGet profile name, not an email address. No long-lived
NUGET_API_KEY secret is required. Only the publish job has id-token: write; CI has
no publishing permission. Adding a GitHub Environment requires coordinated policy
and workflow changes. Policy ownership does not grant rights to another account's
package. See [NuGet's official setup](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

### Releasing another version

1. Keep both workflows on main and their branch filters consistent.
2. Manually update `<Version>` in Directory.Build.props, review, and push/merge
   to main. That push triggers publication after successful CI; no tag/GitHub
   Release is required by these workflows.
3. Inspect build-test-pack, then publish-nuget. A failed gate stops publication;
   tests/assertions are not bypassed.
4. After NuGet lists the version, use the README install/update commands.

Version is the exact stable major.minor.patch value in Directory.Build.props,
without CI run-number suffixes. The publish job reads the triggering commit's
props. Existing NuGet versions are skipped via --skip-duplicate, never overwritten;
a main push without a version change does not create another version.

If only publication failed because of policy, fix the policy and rerun that publish
run while its artifact remains available. Once the 14-day artifact retention expires,
a new main-push CI run is needed; manual CI does not publish.
References: [NuGet/login](https://github.com/NuGet/login),
[workflow_run](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_run).

## Prerequisites and offline preparation

Development SDK is pinned to **10.0.201**. The tool needs a .NET 10 runtime;
indexing also needs the target global.json-selected installed SDK, reference packs,
and restored project.assets.json. Recorded local runtime is **10.0.5**. Doctor
reports installed tooling without evaluating the target. Other SDKs may disable
fingerprint reuse; acceptance is limited to recorded configurations.

Acquire matching OS/architecture/RID SDK/runtime/reference packs and locked packages
in an approved preparation environment. Transfer caches or internal feeds by the
organization's process. Use NuGet.Config with clear and approved local feeds only.
Assets can contain absolute package roots, so restore explicitly at the final location:

```sh
dotnet restore UO.Graphs.slnx --locked-mode --configfile /approved/offline/NuGet.Config -m:1 -nr:false -p:NuGetAudit=false
dotnet restore samples/Phase2/Fixtures.slnx --locked-mode --configfile /approved/offline/NuGet.Config -m:1 -nr:false -p:NuGetAudit=false
dotnet restore samples/Phase3/Semantics.csproj --locked-mode --configfile /approved/offline/NuGet.Config -m:1 -nr:false -p:NuGetAudit=false
```

Disabling audit here is explicit disconnected dependency preparation, not license
or security verification. Runtime never initiates acquisition. Missing SDK/assets
return exit 4. Trusted custom build/generator code can perform I/O; real network
isolation needs infrastructure. Physical offline smoke remains **NOT RUN**.
A restricted terminal, empty feed, or ignore-failed-sources alone is not proof.

## Build and install a local package

After explicit preparation and relevant tests:

```sh
dotnet build UO.Graphs.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet pack src/UO.Graphs.Cli -c Release --no-build --no-restore -m:1 -nr:false -o /controlled/local-feed
# Configure only that local feed; use a new/empty tool-path.
dotnet tool install UO.Graphs.Cli --version 0.9.0 --tool-path /controlled/new-tool-path --configfile /controlled/NuGet.Config --no-cache
/controlled/new-tool-path/uographs --help
```

The same DotnetTool package supports tool-path, local manifest, and global install.
Isolated local-feed tool-path and temporary local-manifest installs were executed;
the user's global installation/manifest was not changed. Local-manifest historical
smoke checked help/query/context against a preexisting DB; its index was NOT RUN.
Later Release tool-path smoke indexed a fresh DB successfully. Global-scope install
remains NOT RUN. See STATUS for exact environments and results.

The entry point is uographs.dll and command name uographs. BuildHost-netcore,
native SQLite assets, and product DLLs are included, with [LICENSE](../LICENSE),
[third-party notices](../THIRD-PARTY-NOTICES.txt), and docs/licenses texts. ABP LGPL
fixture binaries are excluded. The 143-package/version [inventory](DEPENDENCIES.md)
includes development fixtures too; audit success is not a vulnerability scan or
execution on every native RID.

Initial restricted macOS package indexing timed out because both package and repo
BuildHost failed named-pipe bind with SocketException (13). Their 20 files matched
by SHA-256. Later permitted macOS Release and Linux installed-tool flows passed;
these observations are separate, not a claim that all IPC restrictions are resolved.
The original probe is `.uographs/phase11-followup/buildhost-probe-locale.json`.

## Typical workflow

Prepare the target SDK/assets in an approved environment first:

```sh
uographs doctor
uographs index /repo/Example.slnx --trust-project --db /controlled/graph.db
uographs query find Greet --limit 20 --db /controlled/graph.db
uographs context '<exact-symbol-id>' --max-chars 12000 --db /controlled/graph.db
uographs index /repo/Example.slnx --incremental --trust-project --db /controlled/graph.db
uographs status --check-freshness --db /controlled/graph.db
python3 scripts/safe_export.py /controlled/new-export.ndjson -- uographs export --format ndjson --db /controlled/graph.db
```

Trust permits MSBuild execution, not sandboxing. Incremental changes can rebuild
the whole selected closure. Preserve partial/unresolved/truncated/freshness states;
static exact binding is not runtime execution. The
[envelope schema](schemas/cli-envelope.schema.json) describes the outer JSON contract;
inner graph metadata is extensible, and NDJSON/help/stderr are outside that schema.

## Repository-host-independent verification

With prepared SDK/assets,
`python3 scripts/verify_release.py --evidence /controlled/new-evidence` runs build,
standard tests, audit, pack, isolated install, and index/query/context, retaining raw
local output/commands. Failures return nonzero; skipped dependent steps are not passes.
Timeout cleanup targets its own process group. --skip-tests explicitly reports NOT RUN.

On an authorized Linux agent with a prepared namespace,
`--offline-prefix-json '["unshare","-n","--"]'` runs installed-tool commands through
that launcher. The script neither elevates permissions nor proves the launcher's
network isolation by itself. This launcher was not used on macOS.
The [on-prem Azure example](../ci/azure-pipelines.onprem.yml) uses a self-hosted pool
and local evidence without hosted agents, secrets, publishing, or graph upload.
Azure pipeline execution is NOT RUN; GitHub Linux CI is separately verified.

## Remaining release gates

- Windows execution and the wider organizational/self-hosted agent matrix.
- Physical network isolation and physical disk exhaustion.
- 1000-project analysis, 100-project leaf/dependent scenarios, true cache-cold runs,
  and peak process-tree RSS.
- Real Copilot E2E, beyond file-format and CLI subprocess checks.

Package publication is verified, but full release acceptance remains incomplete.
See [PERFORMANCE](PERFORMANCE.md), [SECURITY](SECURITY.md), and [STATUS](STATUS.md).
