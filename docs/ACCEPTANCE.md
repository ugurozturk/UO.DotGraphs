# Phase acceptance criteria

This document defines verification requirements. Listing a feature here does not
mean it is implemented or accepted, nor does it authorize a later phase.
[STATUS](STATUS.md) contains actual results; `history/` contains dated phase records.

## Common gate

Every implementation phase after Phase 0 requires a real build, relevant automated
tests, and real CLI smoke. Record command, exit code, environment, and result.
Unexecuted checks remain `NOT RUN` with a reason. Source inspection or compilation
alone is not end-to-end acceptance.

Tests exercise actual analysis/storage/query behavior, including negative and
partial-analysis cases. Hardcoded fixture output is not an implementation.
Controlled fixture success does not imply support for every real solution.

When SDKs/packages change, verify stable versions, compatibility, exact pins/locks,
and licenses. Separate development restore from runtime. Verify no automatic
runtime restore/download/update and refusal before target MSBuild evaluation when
`--trust-project` is absent. Real offline acceptance is a separate gate.

Track Windows, Linux, and macOS separately. A passing platform does not validate
another. Do not close a phase while mandatory criteria remain unverified; Phase 11
includes the final platform matrix.

## Phase 0 — Preparation

- Inspect the directory, user files, Git state, and installed SDK; preserve files.
- Establish AGENTS, product, architecture, acceptance, and status documents with
  consistent scope, sequencing, and accuracy rules.
- Do not invent implementation/test results. Build/test/smoke are not required.

## Phase 1 — Working end-to-end skeleton

- Build four .NET 10 product projects with pinned stable SDK/packages, MIT text,
  and dependency license evidence.
- Extract a real entity from a small C# fixture through trust-gated Roslyn loading,
  persist it in SQLite, and read it through a separate CLI process.
- Test help, invalid input, persistence, missing trust/prerequisites; document
  commands, output, and exits without claiming broad symbol/relationship coverage.

## Phase 2 — Workspace and symbols

- Load real multi-project solutions and document/query supported symbol kinds.
- Keep overloads, partial declarations, project identity, TFM, and configuration
  contexts distinct; show selected and skipped contexts.
- Missing SDK/assets, references, and projects produce diagnostics and partial or
  failed analysis. Empty output must not masquerade as complete analysis.

## Phase 3 — Semantic relationships

- Record supported calls, type dependencies, inheritance, and implementation with
  source evidence.
- Test overload resolution, interface/virtual calls, dynamic/unresolved candidates,
  and exact/inferred/unresolved distinctions. Static targets are not runtime proof.
- Preserve ranges, analysis context, and reasoning through storage and CLI output.

## Phase 4 — Graph queries

- Verify find, callers/callees, dependencies, impact, and paths on real indexes.
- Test cycles, direction, depth/results/work bounds, ambiguity, unreachable targets,
  and visible truncation.
- Distinguish partial indexes from empty results; scope absence claims precisely.

## Phase 5 — AI context and export

- Produce deterministic, bounded context with source references; document and test
  the budget unit and truncation behavior.
- Preserve identity, evidence, confidence, context, and limitations in versioned
  exports.
- Require no LLM/API connection. Document the separate AI data-transfer boundary.

## Phase 6 — Incremental indexing

- Refresh affected records and remove obsolete relationships after add/edit/delete/
  rename and project/reference/context changes.
- Compare semantic results of incremental and clean full indexing for the same
  final source snapshot.
- Verify no-op, interruption, failure, invalidation, consistency, and freshness.
  State actual invalidation granularity; no watcher is required.

## Phase 7 — DI/HTTP analysis

- Extract bounded DI and HTTP declarations from the analyzed application, with a
  support matrix and explicit dynamic limits.
- Test positive/negative fixtures, multiple registrations, and ambiguous endpoints.
  Do not label heuristic matching exact.
- Add no product HTTP server or network endpoint discovery.

## Phase 8 — Mediator/messaging analysis

- Extract supported request/handler and publish/consumer contracts with evidence.
- Preserve uncertainty for missing handlers, multiple consumers, and dynamic
  registration; give no delivery guarantee.
- Add no mediator, event bus, or broker requirement to the product architecture.

## Phase 9 — ABP/EF Core/Refit analysis

- Define separate version/API matrices, fixtures, and source evidence per module.
- Explain convention-based inference and avoid exact claims for unsupported or
  dynamic behavior.
- Run no migrations, database/HTTP requests, or application host for analysis.

## Phase 10 — VS Code GitHub Copilot Agent Skill

- Use documented CLI commands only; document installation, discovery, and bounded
  context use without MCP or a service.
- Exercise instructions for missing CLI/SDK/assets/trust, partial/stale indexes,
  and command errors; do not silently grant trust or bypass permissions/policy.
- Verify actual skill use in VS Code/Copilot separately. File checks and CLI tests
  alone leave real integration `NOT RUN`.
- Explain the external data boundary and keep the CLI independently usable.

## Phase 11 — Performance, packaging, and final audit

- Record budgets before measuring specified representative solutions: full and
  incremental indexing, memory, SQLite size, and query latency.
- Test clean tool installation, help/version, indexing, and queries from the
  package. With SDK/assets prepared, verify operation under real network isolation.
- Audit package payload, notices, licenses, versions/locks, and native platform assets.
- Record installation/build/tests/smoke separately on Windows, Linux, and macOS.
  Cross-compilation is not execution; inaccessible platforms remain `NOT RUN`.
- Recheck locality, trust, partial-analysis reporting, scope, and Copilot privacy.
  Packaging or publication alone does not close remaining release gates.
