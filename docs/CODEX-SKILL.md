# Codex skill

The optional `uographs` skill uses the local CLI through an authorized terminal.
It adds no MCP server, service, API key, runtime dependency, or automatic download.
The CLI contract and evidence rules match the Copilot skill.

## Installation and use

The recommended installation is the [Codex plugin](../plugins/uographs/README.md).
First prepare the CLI separately if it is missing (.NET 10 runtime required):

```sh
dotnet tool install --global UO.Graphs.Cli
uographs --help
uographs doctor
```

If it is already installed, reuse it; plugin setup does not reinstall or update it.
NuGet installs the analysis CLI, while Codex installs the plugin. Neither step
creates a graph index. Each repository needs its own separately prepared index.

After the marketplace files are published to the GitHub repository:

```sh
codex plugin marketplace add ugurozturk/UO.DotGraphs
codex plugin add uographs@uographs
codex plugin list --marketplace uographs --json
```

The marketplace name and plugin name are both `uographs`. Install once for your
Codex user account and use it across repositories, subject to repository and
organization policy. The plugin bundles the existing skill and CLI reference;
it adds no MCP server, hook, setup skill, authentication integration, or automatic
NuGet download. Plugin version 0.2.0 is independent of the CLI NuGet version.
Copilot uses the same plugin through its own marketplace catalog; see
[COPILOT-SKILL](COPILOT-SKILL.md).
GitHub installation is not verified until those files are available remotely.

For local package development, replace the first command with:

```sh
codex plugin marketplace add '/absolute/path/to/UO.DotGraphs'
```

Use a disposable test environment for installation acceptance. Marketplace add
and plugin add change Codex configuration/cache; listing help alone is not an
installation test. Check installed/enabled state with `plugin list --json`, then
verify discovery in a new Codex session separately from file validation.

### Existing standalone installations

The repository skill remains at
[.agents/skills/uographs](../.agents/skills/uographs/SKILL.md). The Python 3.10+
installer and NuGet-packaged templates remain available for a repository-only
installation from reviewed source/package files:

```sh
python3 scripts/install_skill.py '/path/to/authorized/repository' --agent codex
```

The destination must exist. This installer refuses existing and symlinked skill
destinations, preserves AGENTS.md and Copilot content, and uses the
[standalone template](../templates/codex/uographs/SKILL.md). Its default without
`--agent` remains Copilot. Installing into this checkout correctly refuses an
overwrite because the skill already exists here.

The plugin does not remove or overwrite standalone skills. Same-named skills in
multiple scopes may both appear; select the plugin copy when testing discovery,
and review any customized standalone copy before manually removing duplication.
There is no `uographs skill add codex` command in the CLI.

Open the target repository in Codex and select `uographs:uographs` in the skill
picker (the name verified with Codex CLI 0.162.1), for example:

```text
$uographs:uographs Use the prepared CLI and database /absolute/path/graph.db to explain
who calls MyNamespace.Service.Execute. Read only; report source evidence,
freshness, partial coverage, and truncation.
```

Discovery and `$skill` invocation follow the
[official OpenAI skill documentation](https://learn.chatgpt.com/docs/build-skills)
checked on 2026-10-10. Standalone repository copies retain the `$uographs` name.
Codex discovers repository `.agents/skills` folders;
CLI/IDE users can inspect `/skills`. If a new skill does not appear, restart Codex.
Check for same-named skills in other scopes because Codex does not merge them.
No optional UI metadata is required; automatic selection remains enabled by default.

Provide a prepared CLI 0.10.0 and explicit database path. The skill selects the
shortest useful route: definition lookup and source, the needed semantic query,
or bounded impact with source/tests. Help is conditional on unknown compatibility;
doctor diagnoses runtime trouble. Query metadata usually replaces a separate
status call. Freshness checks and context expansion depend on the task. Query and
status default to a 12,000 Unicode-scalar summary, including the final LF; full
diagnostics are only for a specific investigation. Unchecked reads stay
`notChecked`; source hash matches do not prove global freshness. The skill does
not fall back to unbounded output on an incompatible older CLI. Index creation
or update requires authorization covering MSBuild trust and analysis scope. It
never silently installs/restores, grants trust, or enables generators. Sharing
source/graph output with Codex remains subject to AI data policy; a local database
does not make Codex offline.

## Verification

```sh
python3 -B scripts/test_skill.py --evidence .uographs/codex-skill/artifacts.json
python3 -B scripts/test_skill.py --db /absolute/path/HelloGraph.db --evidence .uographs/codex-skill/reads.json
```

Artifact tests cover the plugin marketplace/manifest, packaged skill parity,
and both standalone layouts, relative references, template parity,
identical documented CLI commands, safe Codex CLI installation, coexistence with
Copilot, overwrite refusal, and symlinks at every Codex destination level. The
optional database run uses a real prepared HelloGraph snapshot and the existing
Debug CLI subprocess harness. Build/package validation and actual results are in
[STATUS](STATUS.md). CI runs artifact tests and requires both templates in the package.

Local marketplace add, plugin installation, and user-scope discovery in two
unrelated repositories passed with Codex CLI 0.162.1. These checks used a separate
test profile and the local stdio protocol; no model request was sent. Evidence
and exact acceptance limits are recorded in [STATUS](STATUS.md).

**Model-driven Codex E2E: NOT RUN.** Installation and discovery do not establish
model selection or behavior. In an authorized Codex session, verify explicit
invocation, an exact-symbol question with file/line evidence, ambiguous selection,
missing CLI/index, stale/partial coverage, denied terminal access, and literal
handling of instruction-like graph text. Confirm that no automatic restore/index
or permission change occurs. Keep these results separate from automated tests.
