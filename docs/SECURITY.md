# Local writes, trust, and failure handling

Queries/context/export do not start MSBuild. Product code does not edit analyzed
C#, project, solution, or configuration files. Indexing writes the selected SQLite
database and adjacent writer-lock/WAL/SHM/journal files, defaulting to `.uographs`
in the current directory. Progress/errors use stderr; JSON uses stdout; no automatic
log file is created.

Test/measurement scripts write explicitly selected evidence directories. The
synthetic generator refuses existing destinations. Benchmark source mutations are
limited to its controlled generated datasets, not user projects.

## MSBuild and custom-code boundary

Full and incremental indexing require `--trust-project` before evaluation. The
CLI does not invoke automatic NuGet restore/download/update. However SDK resolvers,
imports, property functions, targets, custom tasks, and permitted generators run
with user privileges and may write files, start processes, or use the network.
Workspace loading can generate standard obj files. Trust is not a sandbox.

`TrustGatePrecedesMsbuildTargetSideEffects` adds a real BeforeTargets=
ResolveAssemblyReferences target with WriteLinesToFile to a controlled fixture.
Without trust its marker is absent; with trust workspace loading writes the marker
outside the index root in a controlled test directory. Project source text stays
unchanged. This demonstrates why custom-code side effects cannot be ruled out.
Observed input changes during indexing reject publication and preserve the last commit.

If sources must be immutable, the organization must prepare a read-only copy and
separate writable obj/output locations. OS/container/agent policy provides network,
process, and filesystem isolation; the product does not elevate terminal permissions.
It cannot prevent malicious tasks/generators from editing sources.

## Tested failure paths and their limits

- Transaction cancellation and real SIGINT (exit 130) preserve the old snapshot.
  SIGKILL during preparation, WAL-publication process kill, concurrent readers, and
  competing writers (exit 10) have separate tests. Lease spans analysis through commit.
- A test-only internal seam sets PRAGMA max_page_count to provoke real
  **SQLITE_FULL (13)**, then verifies rollback, retained graph, and a successful
  retry. This is not physical disk exhaustion; disk-image/quota acceptance is
  **NOT RUN**. No production fault-injection option is exposed.
- Unknown schemas fail with exit 6. Supported old schemas require explicit re-index
  where necessary. Corrupt/non-SQLite files are not overwritten. Invalid global.json
  and props fail without publishing a replacement.
- Database paths are canonicalized to avoid alias-based lease bypass. Symlinked
  write sidecars are rejected. Snippet traversal/outside-root/symlink/secret/config/
  hash checks prevent disallowed text reads; fingerprints reject symlink inputs.
  MSBuild may evaluate linked external sources, so --root is not an OS access sandbox.
- SQL values are parameters; literal percent/underscore/apostrophe and SQL/shell
  payload regressions exist. Structural SQL fragments come from controlled enums
  or placeholders. Source-derived labels and evidence are never shell instructions.
- ProjectPreparation's dotnet/MSBuild subprocess stdout is capped at **32 Mi
  characters**, stderr at **1 Mi characters**. Overflow yields exit 4 without
  publication. A two-minute timeout/cancellation covers post-parent-exit pipe drain.
  Real parent/child, post-exit handshake, 250 ms delay, cancellation, and large-stderr
  tests distinguish actual blocking from scheduling delay. The product does not
  claim to kill a reparented child; tests separately clean up their own child.
- A real IncrementalIndexer test verifies a cooperative permitted generator receives
  and observes cancellation. Generators that ignore the token require external
  process policy; forced malicious-generator termination is unverified. The above
  stream caps/deadline do not apply universally to Roslyn MSBuildWorkspace BuildHost
  or in-process generators, which may write directly to Console.
- Context max-chars includes the complete JSON and LF. Whole records are pruned;
  truncation remains. An undersized mandatory envelope yields empty stdout/exit 2.
  Query/export have record bounds, not total byte bounds; large metadata or
  diagnostics can still produce large output.

The recorded restricted BuildHost and VSTest IPC failures and later successful
permitted runs are distinct evidence in [STATUS](STATUS.md). A timeout alone does
not prove a missing package payload or a specific permissions failure.

## Export and skill installation

CLI export writes stdout and cannot prevent shell `>` from truncating an existing
file. Use:

```sh
python3 scripts/safe_export.py NEW_FILE -- uographs export --format ndjson
```

The helper uses exclusive xb creation, refuses existing/symlinked targets, and runs
argv without a shell. On child failure it preserves the partial file for inspection
and the failed exit. POSIX noclobber (`set -C`) is a separate explicit shell policy.

The skill installer refuses existing/symlinked destinations, with no merge or
overwrite. Isolated tool-path tests do not change a user's global install. These
path checks are not a race-proof sandbox against another user concurrently mutating
the filesystem; destinations should be writable only by authorized users.

The product has no source/graph upload, telemetry, MCP/HTTP/LLM service, or API-key
requirement. Explicit CI package publication is separate from runtime. Manually
sharing graph/context with Copilot is governed by the organization's AI data policy.
