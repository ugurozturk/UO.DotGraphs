# Performance methodology and recorded results

Measurements below are from **2026-10-09**, macOS **27.0.1 ARM64**, **24 logical
CPUs**, **64 GiB RAM**, SDK **10.0.201**, runtime **10.0.5**, Python **3.14**.
CPU-model sysctl and process-list ps access were denied. Assets were prepared by
explicit restore. Peak native/child-process RSS was **NOT RUN**; installed RAM is
not process consumption. These are local observations, not Windows/Linux benchmarks.

## Datasets and conditions

`scripts/synthetic_solution.py DEST --projects 10|100|1000 --classes 4 --methods 8`
refuses an existing destination. Consumers form a star around P0000; the final
project is a leaf. Generated code is analyzed by the real compiler. Dataset
creation alone is not successful analysis. Local Directory.Build/Packages settings
isolate it from ancestor settings; no package dependencies, net10.0 reference pack.

| Projects | Source files | Methods | Source bytes | Recorded execution |
| --- | ---: | ---: | ---: | --- |
| 10 | 40 | 320 | 16,648 | Full/no-op/leaf/dependent plus reads completed |
| 100 | 400 | 3,200 | 171,088 | Full and no-op passed; 4001/13635/13935 graph records |
| 1000 | 4000 | 32,000 | 1,715,488 | Generated; restore/index/latency NOT RUN |

The 1000-project run was not started after lengthy 100-project/IPC attempts. This
does not prove unsupported scale or insufficient RAM.

Pre-recorded investigative budgets in `.uographs/phase11-evidence/budgets.json`:
full 10/100/1000 = 120/600/3600 s; warm = 60/300/1800 s; reads = 2 s;
process tree = 8 GiB. These are not measured SLAs; unmeasured criteria did not pass.

- **Full:** fresh CLI process and empty database with --full, prepared SDK/assets.
  OS caches were not flushed: true cache-cold acceptance is NOT RUN.
- **Warm/no-op:** same arguments/environment/scope and existing snapshot in a new
  process; verify noOp and zero analyzed projects. Evaluation and hashing still run.
- **Leaf/dependency edits:** change one expression literal only in the generated
  dataset, then restore it. Both changes rebuild all ten projects; no selective
  project-incremental claim.
- Index timings are CLI timers: initial preparation/hash and analysis including
  second preparation/hash. Startup and SQLite publication are excluded.
- Read wall times use fresh direct-DLL subprocesses and 100 ms harness polling.
  Treat small values as approximate upper bounds, not in-process SQL microbenchmarks.
  Cache/concurrent load was uncontrolled; no statistical percentage improvement claim.

## Ten-project measurements

One successful repetition per index scenario; three repetitions per query/context.
The script supports three warm/read repeats by default; actual evidence counts govern.

| Scenario | Fingerprint ms | Analysis + validation ms | Outcome / projects analyzed |
| --- | ---: | ---: | --- |
| Full before workspace sharing | 12667 | 27338 | rebuiltAllProjects / 10 |
| Full after sharing | 13360 | 16129 | rebuiltAllProjects / 10 |
| Warm incremental | 14356 | 0 | noOp / 0 |
| Leaf edit | 13903 | 17035 | rebuiltAllProjects / 10 |
| P0000 dependency edit | 13933 | 16334 | rebuiltAllProjects / 10 |

Each successful graph: **401 entities / 1305 relationships / 1335 occurrences**.
SQLite: **6,631,424 bytes**, WAL 0, SHM 32,768 separately. Even no-op hashed
**6978 files / 940,927,624 bytes**. SDK/packs/tool content and per-project evaluation
remain measured costs; timestamp-only reuse was not substituted.

| Operation | Repeat 1 s | Repeat 2 s | Repeat 3 s |
| --- | ---: | ---: | ---: |
| query find M0 --limit 20 | 0.117727 | 0.222842 | 0.217710 |
| context P0000.C0.M0() --max-chars 12000 | 0.219144 | 0.223178 | 0.225076 |

## Optimization scope

Previously every project in a homogeneous solution created a workspace and reopened
the solution. The measured path now shares one analyzer-free immutable Solution,
reducing ten solution loads to one. Sharing requires same TFM, no preparation
diagnostics, and generators disabled. Mixed inner-build, project-input, and generator
paths remain separate. SQLite entity/relationship/occurrence payloads matched after
normalizing analysis IDs (`workspace-regression.json`). Reference equality also
checks properties, module MVIDs, and recursive compilation inputs.

This comparison is one repetition with different obj/cache/load conditions, so no
percentage performance or token-saving claim follows.

## Reproduction and raw evidence

```sh
python3 scripts/synthetic_solution.py /controlled/new-p10 --projects 10
# Explicit dependency preparation, separate from product runtime:
dotnet restore /controlled/new-p10/Synthetic.slnx --configfile /controlled/new-p10/NuGet.Config -p:NuGetAudit=false --disable-parallel -m:1 -nr:false
python3 scripts/benchmark.py /controlled/new-p10 /controlled/new-evidence --timeout 600 --repeats 3 --cli /controlled/tool/uographs
```

Local `.uographs/phase11-evidence/` retains environment/budgets/manifests,
direct-baseline-10.json, *-direct-10.raw, performance-summary.json,
read-latency/raw.jsonl, stdout/stderr, and SQLite sizes. These raw graphs were not
uploaded as hosted artifacts.

Python/redirected index attempts hit workspace IPC timeouts; the 180 s terminated
attempt remains in perf-baseline-10c/raw.jsonl and is not a passing measurement.
An initial 100-project attempt overlapped a product rebuild; input hashing correctly
rejected publication with exit 5. Stable-binary repetition is separate. Restricted
process inventory prevented independent residual-child/memory inspection.

## One hundred projects

Stable-binary full index passed: fingerprint **122481 ms**, analysis/validation
**136951 ms**, one repetition. Graph: **4001 / 13635 / 13935**. SQLite:
**53,084,160 bytes**; hashing: **16116 files / 1,894,229,534 bytes**.

full-100-summary.json retains numeric stdout values. The tool transport truncated
full-direct-100-final.raw in the middle; it is not complete JSON. Full analysis
metadata was separately read from SQLite into full-100-analysis.json. These evidence
forms are not interchangeable. Leaf/dependent 100-project runs remain NOT RUN.

The first warm attempt rebuilt all 100 projects because pack changed publish/
under the tool directory: **115269 ms** fingerprint, **133246 ms** analysis. It is
not a no-op pass. A later stable-input attempt passed **noOp / 0 projects**:
**111733 ms** fingerprint, **0 ms** analysis, recorded in noop-100-summary.json and
the separately transport-truncated noop-direct-100.raw.

Query repeats: **0.213082 / 0.216708 / 0.216431 s**.
Context at 50,000 characters: **0.218130 / 0.214898 / 0.214983 s**.
The preceding 12,000-character negative returned exit 2 because the mandatory
envelope required 27,146 characters. The benchmark default is therefore 50,000;
the ten-project evidence explicitly used 12,000. These are character, not token,
measurements. Current release gates remain in [STATUS](STATUS.md).
