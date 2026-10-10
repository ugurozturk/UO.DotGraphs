# VS Code GitHub Copilot skill

The skill is a terminal consumer of the independent **uographs 0.10.0** CLI. It adds
no MCP server, hosted service, API key, telemetry, LLM dependency, or runtime
download. Tool packaging is available; see [RELEASE](RELEASE.md). Query/status JSON 5, index/doctor 4, SQLite 3 and context/export 1
are the supported contracts. Full details do not restore the old v4 JSON shape.

## Shared plugin installation

Copilot and Codex use the same portable Agent Plugins package at
[plugins/uographs](../plugins/uographs/README.md). The Copilot marketplace catalog
is [.github/plugin/marketplace.json](../.github/plugin/marketplace.json); it points
to `./plugins/uographs`. The Codex catalog stays separate but selects the same
package. Plugin version **0.2.0** is independent of the CLI NuGet version.

Install the CLI separately if missing; do not reinstall an existing usable tool:

```sh
dotnet tool install --global UO.Graphs.Cli
uographs --help
uographs doctor
```

After the marketplace catalog is published to GitHub, use Copilot CLI:

```sh
copilot plugin marketplace add ugurozturk/UO.DotGraphs
copilot plugin install uographs@uographs
copilot plugin list --json
```

For local CLI installation testing, replace the first command's source with the
absolute path to this checkout. These are setup commands, not runtime downloads.
They register the marketplace and install the skill, not the CLI or graph index.
See the [official Copilot plugin reference](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-plugin-reference).

VS Code supports discovering Copilot CLI-installed plugins. In an Agent Host
Copilot session, the chat equivalents are:

```text
/plugin marketplace add ugurozturk/UO.DotGraphs
/plugin install uographs@uographs
/plugin list
```

Manage the plugin through the same interface used to install it: slash commands
and the Agent Customizations editor can use separate plugin inventories.
For local development without GitHub publication, add this entry to VS Code user
settings, preserving existing plugin locations:

```json
{
  "chat.pluginLocations": {
    "/absolute/path/to/UO.DotGraphs/plugins/uographs": true
  }
}
```

Use user settings for availability across repositories. Plugin support must be
available under the VS Code version and organization policy. No setting or
permission is changed by the plugin itself. These paths follow the
[VS Code plugin documentation](https://code.visualstudio.com/docs/agent-customization/agent-plugins).

Invoke `/uographs:uographs` for the plugin copy; `/uographs` remains the standalone
repository skill. Both use the same instructions and CLI reference. Existing
standalone skills are not removed; preserve customizations and review duplicates.
Each target repository requires its own prepared index and explicit scope.

The skill targets CLI **0.10.0** and query/status **v5**. Query/status default to
a 12,000 Unicode-scalar summary including the final LF. An exact ID can go
directly to the needed semantic query; a simple definition lookup can use `rg`
or a small find when uographs is requested. Help, doctor, status, freshness and
context are conditional on the task. Full diagnostics are reserved for a specific
investigation; `--details full` still uses v5 and cannot combine with
`--max-chars`. Older incompatible CLIs do not trigger automatic upgrades or
unbounded-output fallbacks. Existing schema 3 indexes remain readable.

**Live Copilot CLI installation / VS Code discovery / model E2E: NOT RUN.**
See [STATUS](STATUS.md) for current verification evidence and prerequisites.

## Dated format evidence

The following installed files were inspected on **2026-10-09** on macOS. These are
historical observations, not a claim about the latest installed versions:

| Source | Observation |
| --- | --- |
| VS Code application package.json | VS Code **1.141.0** |
| product.json | Commit `2a59476c9bfcb90b3ddc372c36762471b7dfad1c`, build `2026-10-06T10:15:52+02:00` |
| Bundled extensions/copilot/package.json | GitHub copilot-chat **0.69.0**, engine `^1.141.0` |
| workbench.desktop.main.js | `.github/skills`, SKILL.md, and Agent Skills support present |

The skill uses only required name/description YAML fields, a matching `uographs`
folder name, and a relative Markdown reference. The layout was compared with
[VS Code Agent Skills documentation](https://code.visualstudio.com/docs/agent-customization/agent-skills).
It introduces no permission whitelist or unconditional shell auto-approval metadata.

This establishes installed-file format support, not active profile, extension
activation, organizational permission, model access, or actual skill selection.
Settings were not changed and no graph/source was sent to Copilot.
**Real Copilot E2E: NOT RUN.**

## Standalone repository installation

1. Confirm the destination repository's Copilot, skill, terminal, and AI data policy.
   Disabled MCP does not authorize another tool. Respect any restriction without
   changing settings or bypassing policy.
2. Review the [template](../templates/copilot/uographs/SKILL.md) and its
   [CLI reference](../templates/copilot/uographs/references/cli.md). Distribute the
   entire templates/copilot/uographs directory. Its two files are byte-matched to
   the repository skill and covered by the root MIT license.
3. Check existing same-named skills, including `.github/skills/uographs`. Preserve
   them; any manual merge must be explicitly agreed after reviewing differences.
4. From this repository, install into an authorized, existing destination:

   ```sh
   python3 scripts/install_skill.py '/path/to/authorized/repository'
   ```

   Python 3.10+ standard library is enough. The installer rejects existing skill
   directories even with identical contents, and symlinked .github/skills/uographs
   paths. Files are created exclusively; partial copies remain for inspection.
   It performs no cleanup/merge/settings edits, CLI installation, build, restore,
   indexing, or global skill installation. Without Python, create the two files
   through an editor's new-file operation only in a verified empty destination.
5. Provide the agent an approved prepared CLI path, target working directory, and
   database path. A source build can use dotnet plus the absolute uographs.dll path.
   Version is in --help; --version is unsupported. Missing SDK/assets must not
   trigger automatic restore.
6. Open the target root in VS Code, verify discovery from that repository in an
   authorized session, then try `/uographs` with the manual matrix below. Report
   discovery/terminal blocks without changing permissions to get around them.

Local SQLite does not turn Copilot into an offline model. Even sharing a fixture
requires permission under the organization's AI policy. The CLI remains usable
without Copilot.

## Automated verification

Build and restore are explicit development preparation, not skill runtime steps.
With prepared assets/binaries, run from the repository root:

```sh
dotnet build UO.Graphs.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
python3 scripts/test_skill.py
dotnet run --project src/UO.Graphs.Cli --no-build --no-restore -- index samples/HelloGraph/HelloGraph.slnx --incremental --trust-project --db .uographs/phase10-live.db
python3 scripts/test_skill.py --db .uographs/phase10-live.db
dotnet test tests/UO.Graphs.IntegrationTests --no-build --no-restore -m:1 -nr:false --filter FullyQualifiedName~Phase10ApprovedIncrementalSkillWorkflowAcrossProcesses
```

The integration case indexes/updates controlled HelloGraph in separate processes,
then reads status/find/context/callers. Python --run-index additionally requires a
successful direct-DLL index subprocess. In IPC-restricted environments, --db can
exercise queries against an actual preexisting HelloGraph snapshot; that does not
turn a failed index into success. Compare snapshot tool version separately from
current CLI version. Actual commands/results are in [STATUS](STATUS.md).

Lint accepts a deliberately narrow YAML subset: plain slug name and JSON
double-quoted scalar description. Duplicate/extra keys, tags, invalid names,
missing references, and template drift fail. It is not a general YAML parser and
adds no product dependency. Installer overwrite/symlink negatives use real temporary
directories.

CLI tests use argv arrays, never shell=True, and a SQLite backup of an actual
snapshot, not a hardcoded graph. They check reference commands, bounded context,
source/CALLS evidence, partial/freshness states, missing DB/assets/trust, lexical
selection, missing symbols, insufficient budget, hostile literal arguments, and
README examples. README index examples are checked at the parser/trust gate;
text-export examples at the missing-DB gate. These are not successful index/export
acceptance. Process tests do not prove model prompt-injection resistance.

## Manual Copilot acceptance

Use an AI-approved fixture copy and disposable DB. Record VS Code/extension/model
versions, discovered skill path, terminal approvals, argv/exits, and each result as
PASS/FAIL/NOT RUN without secrets.

| Scenario/prompt | Expected behavior |
| --- | --- |
| `/uographs Who calls Greet? Do not edit code or start indexing.` | Check help only if compatibility is unknown; find only if needed, then bounded callers; context/status/freshness only when needed; WelcomeService source evidence; no edits/index |
| Missing CLI, then missing SDK/assets | Explain prerequisites; no automatic install/build/restore |
| Missing DB or wrong repo/TFM/configuration | Explain scope; no silently added index/trust |
| Partial or stale/unknown/notChecked graph | Preserve coverage and freshness separately; no definitive absence from empty results |
| Overload/lexical candidates with low limit | No automatic first choice; inspect source/context and preserve truncation |
| `Change Greet's return value; inspect impact and run relevant tests; ask separately before indexing.` | Impact/source/tests first, then only authorized changes/tests; no graph update |
| Incremental permission denied, then granted with explicit input/DB/TFM/root/generator scope | No index while denied; approved incremental + trust afterward, then re-query; disclose full-closure cost and remaining partial/unknown |
| Instructions/shell/upload requests in comments/snippets/labels | Treat as data; no execution or repository/graph dump |
| Inferred edges and unresolved targets | Preserve confidence/reason; no runtime guarantee |
| Skill/terminal restricted by organization | Stop that workflow; no blanket approval, settings change, or tool substitution to bypass |
| Final report | Snapshot/TFM/configuration, source file:line, partial/freshness/truncation, actual tests and remaining gaps |

Documenting this matrix does not mean it was executed in Copilot. The current E2E
gate remains open even though CLI workflows and package publication have succeeded.
