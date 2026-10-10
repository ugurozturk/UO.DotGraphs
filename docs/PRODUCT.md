# UO.Graphs product scope

UO.Graphs analyzes local C#/.NET projects with Roslyn and stores code entities,
relationships, and source evidence in SQLite. Its CLI is `uographs`. The primary
product is a queryable code knowledge graph; diagram export is one output format.

## Capabilities

The CLI finds symbols, follows callers/callees and dependencies, explains potential
change impact, and finds bounded paths. It produces bounded AI context and JSON,
NDJSON, Mermaid, and DOT exports. Incremental indexing skips semantic analysis
when a complete dependency fingerprint is unchanged; otherwise it rebuilds the
entire selected project closure.

Static extractors cover Microsoft DI, ASP.NET Core, UO.Mediator, MediatR,
MassTransit, ABP, EF Core, and Refit within documented API/version boundaries.
The optional VS Code GitHub Copilot and Codex skills consume the independent CLI.
Phase 11 added performance and failure handling improvements, tool packaging,
and release verification. [STATUS](STATUS.md) separates implementation from
actual acceptance and remaining gates.

## Intended environment

- Local checkouts, including repositories obtained from on-premises Azure DevOps.
  Git metadata is not required for analysis.
- Windows, Linux, and macOS are target platforms. A target platform is not
  automatically an accepted platform; see STATUS for actual results.
- .NET 10; the development SDK is pinned in `global.json`.
- An MIT product with separately documented third-party licenses in
  [DEPENDENCIES](DEPENDENCIES.md).

## Locality, trust, and privacy

Analysis and queries do not upload source or graphs. The product contains no MCP
or HTTP server, hosted database, embedding/LLM service, API key requirement, or
telemetry. Development and installation dependency acquisition is separate from
runtime operation.

Runtime never automatically restores, downloads, or updates SDKs or packages.
Missing SDKs/assets produce explicit prerequisite errors. Users prepare target
dependencies beforehand; UO.Graphs does not repair the target project.

Indexing requires `--trust-project` before any target MSBuild evaluation. Imports,
tasks, and other project-defined code can execute. Trust is neither a sandbox nor
a network block; custom build code and permitted generators may perform their own
I/O. See [SECURITY](SECURITY.md).

The Copilot and Codex skills follow terminal and organizational policy. Passing
output to either agent crosses a separate data boundary: a local graph does not
make the model offline. Skill format checks and CLI tests are distinct from real
agent E2E acceptance; see [COPILOT-SKILL](COPILOT-SKILL.md) and
[CODEX-SKILL](CODEX-SKILL.md).

## Accuracy contract

- `exact` proves the stated static relationship in the analysis context, not
  runtime invocation, activation, registration, or delivery.
- `inferred` preserves the rule, explanation, and evidence behind a candidate.
- `unresolved` preserves the observation and reason without inventing a target.
- Results include source evidence, analysis context, coverage, and limitations.
  An empty result from an incomplete workspace does not prove absence.
- Even complete analysis only supports absence claims within the indexed scope,
  supported relationship kinds, and declared static context.

## Outside the initial release scope

No frontend or web graph viewer, multilingual parser, repository federation,
watcher, plugin marketplace, or mandatory NativeAOT. No general runtime tracing
or guarantee of resolving every dynamic call. Analyzing DI, HTTP, messaging, ABP,
EF Core, or Refit does not introduce those application services into UO.Graphs.

## Delivery sequence

0. Preparation documents.
1. Working end-to-end skeleton.
2. Workspace loading and symbols.
3. Semantic relationships.
4. Graph queries.
5. AI context and export.
6. Incremental indexing.
7. DI/HTTP analysis.
8. Mediator/messaging analysis.
9. ABP/EF Core/Refit analysis.
10. VS Code GitHub Copilot Agent Skill.
11. Performance, packaging, and final audit.

Each phase requires the user's instruction. The roadmap is not authorization to
start later phases. [ACCEPTANCE](ACCEPTANCE.md) defines the gates; records under
`docs/history/` preserve phase-specific evidence rather than current limitations.
