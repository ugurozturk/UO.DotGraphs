# UO.Graphs for Codex and GitHub Copilot

This plugin contains the `uographs` skill and its CLI reference. It uses the
separately installed local UO.Graphs CLI; it does not install .NET or the CLI,
create an index, or add an MCP server, hook, service, or account connection.

## Install

If the CLI is missing, install it explicitly (.NET 10 runtime required):

```sh
dotnet tool install --global UO.Graphs.Cli
uographs --help
uographs doctor
```

### Codex

Once the marketplace files are published to the GitHub repository:

```sh
codex plugin marketplace add ugurozturk/UO.DotGraphs
codex plugin add uographs@uographs
codex plugin list --marketplace uographs --json
```

Install once for your Codex user account; repository skill copies are not needed.
Restart Codex if the plugin does not appear. Codex CLI 0.162.1 discovers the
plugin skill as `uographs:uographs`. Select it in the skill picker, or invoke:

```text
$uographs:uographs Use /absolute/path/graph.db to explain who calls MyNamespace.Service.Execute. Read only; include source evidence and coverage limits.
```

### GitHub Copilot and VS Code

After the Copilot marketplace catalog is published to GitHub, use Copilot CLI:

```sh
copilot plugin marketplace add ugurozturk/UO.DotGraphs
copilot plugin install uographs@uographs
copilot plugin list --json
```

VS Code supports discovering plugins installed by Copilot CLI. In VS Code's
Agent Host Copilot chat, the corresponding commands are:

```text
/plugin marketplace add ugurozturk/UO.DotGraphs
/plugin install uographs@uographs
/plugin list
```

Use the same interface to manage the installation; slash commands and the Agent
Customizations editor can have separate inventories. Plugin support must be
available under your VS Code version and organization policy.

For a local checkout, add the plugin path to VS Code **user** settings, preserving
any existing entries, to use it across repositories:

```json
{
  "chat.pluginLocations": {
    "/absolute/path/to/UO.DotGraphs/plugins/uographs": true
  }
}
```

Invoke the plugin skill in Copilot Chat as:

```text
/uographs:uographs Use /absolute/path/graph.db to explain who calls MyNamespace.Service.Execute. Read only; include source evidence and coverage limits.
```

The Copilot catalog at `.github/plugin/marketplace.json` and Codex catalog at
`.agents/plugins/marketplace.json` both select this same portable plugin. The
Copilot plugin commands follow the
[GitHub reference](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-plugin-reference);
VS Code loading and installation follow its
[agent plugin documentation](https://code.visualstudio.com/docs/agent-customization/agent-plugins).
Actual acceptance is recorded in the repository's `docs/STATUS.md`; documentation
of an installation route does not establish live VS Code/Copilot acceptance.

Existing standalone skills are not removed; same-named copies may both appear.

The skill targets CLI **0.10.0** and query/status **v5**. Query/status default to
a 12,000 Unicode-scalar summary including the final LF. An exact ID can go
directly to the needed semantic query; a simple definition lookup can use `rg`
or a small find when uographs is requested. Help, doctor, status, freshness and
context are conditional on the task. Full diagnostics are reserved for a specific
investigation; `--details full` still uses v5 and cannot combine with
`--max-chars`. Older incompatible CLIs do not trigger automatic upgrades or
unbounded-output fallbacks. Existing schema 3 indexes remain readable.

Each repository needs its own prepared graph index. Missing CLI/SDK/index or a
stale/partial graph is reported, never repaired through automatic installation,
restore or indexing. Project evaluation requires explicit `--trust-project`.
Local graph storage does not make the agent offline: content passed to it remains
subject to your organization's AI data policy.

The plugin's version is 0.2.0 and is maintained independently of the
CLI's NuGet version. The skill and CLI reference are copied byte-for-byte from
both standalone agent templates, with parity checked by repository tests.
