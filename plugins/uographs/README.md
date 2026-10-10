# UO.Graphs for Codex

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

Existing standalone skills are not removed; same-named copies may both appear.

Each repository needs its own prepared graph index. Missing CLI/SDK/index or a
stale/partial graph is reported, never repaired through automatic installation,
restore or indexing. Project evaluation requires explicit `--trust-project`.
Local graph storage does not make Codex offline: content passed to Codex remains
subject to your organization's AI data policy.

The plugin's version starts at 0.1.0 and is maintained independently of the
CLI's NuGet version. The skill and CLI reference are copied byte-for-byte from
the standalone Codex template, with parity checked by repository tests.
