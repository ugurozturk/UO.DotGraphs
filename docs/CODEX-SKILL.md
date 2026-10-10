# Codex skill

The optional `uographs` skill uses the local CLI through an authorized terminal.
It adds no MCP server, service, API key, runtime dependency, or automatic download.
The CLI contract and evidence rules match the Copilot skill.

## Installation and use

The repository skill is at [.agents/skills/uographs](../.agents/skills/uographs/SKILL.md).
For another repository, review the [standalone template](../templates/codex/uographs/SKILL.md)
and its [CLI reference](../templates/codex/uographs/references/cli.md), then run from
this checkout (Python 3.10+ standard library):

```sh
python3 scripts/install_skill.py '/path/to/authorized/repository' --agent codex
```

The destination must exist. The installer creates `.agents/skills/uographs` and
copies both files exclusively. Existing destinations, including dangling symlinks
at any skill-directory level, are rejected; partial copies remain for inspection.
It preserves `AGENTS.md`, Copilot skills, and personal Codex configuration. The
original command without `--agent` still installs the Copilot template. The .NET
tool package also carries both templates and this installer for extraction and
review; installing the tool does not itself install an agent skill.

This checkout already contains the Codex skill; running the installer against it
will correctly refuse an overwrite. No global/user skill installation is performed.

Open the target repository in Codex and mention the skill, for example:

```text
$uographs Use the prepared CLI and database /absolute/path/graph.db to explain
who calls MyNamespace.Service.Execute. Read only; report source evidence,
freshness, partial coverage, and truncation.
```

Discovery and `$skill` invocation follow the
[official OpenAI skill documentation](https://learn.chatgpt.com/docs/build-skills)
checked on 2026-10-10. Codex discovers repository `.agents/skills` folders;
CLI/IDE users can inspect `/skills`. If a new skill does not appear, restart Codex.
Check for same-named skills in other scopes because Codex does not merge them.
No optional UI metadata is required; automatic selection remains enabled by default.

Provide a prepared CLI and explicit database path. The skill checks help, doctor,
status, scope and freshness before bounded symbol/context queries. Index creation
or update requires authorization covering MSBuild trust and analysis scope. It
never silently installs/restores, grants trust, or enables generators. Sharing
source/graph output with Codex remains subject to AI data policy; a local database
does not make Codex offline.

## Verification

```sh
python3 -B scripts/test_skill.py --evidence .uographs/codex-skill/artifacts.json
python3 -B scripts/test_skill.py --db /absolute/path/HelloGraph.db --evidence .uographs/codex-skill/reads.json
```

Artifact tests cover both standalone layouts, relative references, template parity,
identical documented CLI commands, safe Codex CLI installation, coexistence with
Copilot, overwrite refusal, and symlinks at every Codex destination level. The
optional database run uses a real prepared HelloGraph snapshot and the existing
Debug CLI subprocess harness. Build/package validation and actual results are in
[STATUS](STATUS.md). CI runs artifact tests and requires both templates in the package.

**Real Codex E2E: NOT RUN.** File validation and CLI tests do not establish model
selection or behavior. In an authorized Codex session, verify discovery, explicit
invocation, an exact-symbol question with file/line evidence, ambiguous selection,
missing CLI/index, stale/partial coverage, denied terminal access, and literal
handling of instruction-like graph text. Confirm that no automatic restore/index
or permission change occurs. Keep these results separate from automated tests.
