# Issue #1125: Indexed cycle baseline candidate evidence

## Scope and pre-change decision contract

Issue #1125 is post-release 0.10 performance stabilization. The implementation reuses one
strongly connected component (SCC) index over `fullGraph` and performs indexed candidate checks.
The pre-change recorder at base revision
[`dcc3b300f6ab673860e84ac3bb94ee5a9832f6b8`](https://github.com/eugenemalaschuk-source/arch-linter-net/blob/dcc3b300f6ab673860e84ac3bb94ee5a9832f6b8/src/ArchLinterNet.Core/Execution/ArchitectureCycleBaselineCandidateRecorder.cs)
ran reachability once per evidence item. The explicit benchmark keeps a test-only copy of that
loop as a legacy control so both paths can be rerun against exactly the same graph and evidence.

The decision contract follows the issue's expected effect: change the candidate work from
`O(B(V+E))` to `O(V+E+B+K)`, where `K` is the required output size; preserve exact candidates;
and retain only `O(V)` component metadata for membership checks. The graph/index construction has
`O(V+E)` temporary storage, and emitted candidates still require `O(K)` storage. The candidate
passes the local effect gate if all outputs match, one index is built per enabled non-empty run,
reachability searches fall to zero, and the 10,000-evidence median is at least twice as fast with
at least 50% lower per-call allocation. A mismatch, repeated reachability, or failure to meet the
local threshold would reject the optimization for further investigation.

The cycle recorder phase share `p` is not available in `AnalysisProfile`, and the profile harness
does not run cycle baseline recording. Therefore the Amdahl bound `1 / ((1-p) + p/s)` and its
infinite-local-speedup limit `1 / (1-p)` are not numerically measurable here. No end-to-end
analysis speedup is claimed.

## Workload and method

The fixed graph has 8 vertices and 8 edges:

```text
A -> B -> C -> A
          C -> D -> E -> F
G <-> H
```

Its nontrivial SCCs are `{A,B,C}` and `{G,H}`, plus the acyclic tail `C -> D -> E -> F`. Its
canonical graph SHA-256 is
`50b3740ea349839b68b2ab1404ae465a8d6a095d92e3d390b3d4cfd07aab6fd6`.

The evidence sequence repeats `A->B`, `G->H`, `C->D`, `E->F` in that order. The first two edges
participate in cycles and the tail edges do not, so the emitted candidate count is `B/2`. Evidence
counts are 100, 1,000, and 10,000. Graph, evidence, and output recorder construction are outside
the timed boundary; each timed sample measures a direct `Record` call. Both implementations are
warmed before five Release samples. Allocations use
`GC.GetAllocatedBytesForCurrentThread`. Structural counters run separately from timing. Retained
managed deltas are forced-GC observations around one retained output recorder; peak working set is
the process high-water delta.

Environment: .NET SDK 10.0.403, runtime .NET 10.0.14, Windows 10.0.26300 x64, AMD Ryzen 7 255
(8 cores / 16 logical processors). The repository pins SDK 10.0.402, which was unavailable for
this run; only `rollForward` was temporarily changed to `latestPatch` for commands and the exact
checked-in `global.json` bytes were restored afterward. Benchmark source identity:
`fb965ecd69980b560377c13850f78967dad30846`.

## Results

| Evidence count | Legacy median ms | Indexed median ms | Local speedup | Legacy allocated bytes | Indexed allocated bytes | Allocation reduction | Legacy retained delta bytes | Indexed retained delta bytes | Peak WS delta bytes (legacy / indexed) |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | 0.0379 | 0.0186 | 2.04x | 32,504 | 3,768 | 88.4% | 178,736 | 2,648 | 0 / 0 |
| 1,000 | 0.3715 | 0.0809 | 4.59x | 320,544 | 11,008 | 96.6% | 468,848 | 9,816 | 0 / 0 |
| 10,000 | 3.1349 | 0.3866 | 8.11x | 3,251,520 | 133,984 | 95.9% | 7,996,984 | 132,696 | 2,146,304 / 0 |

Five samples were used for each implementation and size; medians are shown. The allocation count
includes required output storage. Retained managed deltas and process peak-working-set deltas are
noisy GC/process observations, not stable memory guarantees. The 10,000-item retained deltas in
particular should not be interpreted as a 7.9 MB product-retention requirement; the SCC index is
local to `Record`, and its candidate list is the required output.

Deterministic work counts:

| Evidence count | Legacy reachability searches | Legacy visited vertices | Legacy edge visits | Indexed count | Indexed candidate edge checks | Indexed component checks | Indexed emitted candidates |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | 100 | 300 | 200 | 1 index | 100 | 100 | 50 |
| 1,000 | 1,000 | 3,000 | 2,000 | 1 index | 1,000 | 1,000 | 500 |
| 10,000 | 10,000 | 30,000 | 20,000 | 1 index | 10,000 | 10,000 | 5,000 |

At every size the index prepared exactly 8 vertices and 8 edges. It built 8 adjacency lists and
visited each of the 8 graph edges once during adjacency preparation, reverse-degree construction,
reverse fill, the first SCC pass, and the second SCC pass. It performed no per-candidate graph
search. Thus preprocessing stays linear in the graph and the repeated work is linear in `B`.

Candidate order, multiplicity, and full diagnostic identity matched between the legacy and indexed
paths. The canonical candidate-array SHA-256 values were:

| Evidence count | Candidate count | Candidate-array SHA-256 |
|---:|---:|---|
| 100 | 50 | `94c14c807c0fb86e62152e0d8b350617bf6fc9ab84caa238f691f1aeab310f54` |
| 1,000 | 500 | `a3781b230bf11888b558b2e00a753f798d2067c69d30a65b0b1c7f9e79b519a0` |
| 10,000 | 5,000 | `85e0d4d4ab56bbc325adc32e02228e18040a47c445ab3ca2fb6e6e35ac1ba5b5` |

The indexed path passes the local gate: outputs match, exactly one index is built, reachability
searches are zero, the 10,000-item median is 8.11x faster, and per-call allocation is 95.9% lower.
An earlier before-only capture measured 4.0956 ms and 3,251,400 allocated bytes at 10,000 items;
the checked-in warmed legacy control measured 3.1349 ms and 3,251,520 bytes. This is a 23% timing
difference and 120-byte allocation difference across two measurement harness revisions. The
paired run above is the canonical comparison; the earlier run should not be compared directly to
the indexed values. Local timing is sensitive to warm-up and runtime scheduling. Deterministic
work counts and matching output hashes are the stronger evidence.

## Reproduction

From the repository root with restore assets present, set the source identity and run the explicit
matrix:

```powershell
$env:ARCHLINTER_CYCLE_RECORDER_SOURCE_IDENTITY = 'fb965ecd69980b560377c13850f78967dad30846'
dotnet test tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~RunBaselineCandidateRecorderMatrix' --logger 'console;verbosity=detailed'
```

The test is `[Explicit]`; selecting its name runs the manual benchmark and it remains outside normal
test and acceptance runs. The JSON output includes runtime, OS, graph/evidence digests, all five
samples, allocation and retained-memory observations, structural counters, and candidate digests.

## Scope limits

This benchmark isolates cycle baseline candidate recording. It does not measure a complete
architecture analysis, other cycle detection, or the share of total run time occupied by this
recorder. Correctness coverage for suppressed edges, including cycles whose edges are both
suppressed, remains in `ArchitectureCycleBaselineCandidateRecorderTests` and
`ArchitectureBaselineIntegrationTests`.
