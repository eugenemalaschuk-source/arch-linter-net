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

The explicit matrix also measures a sparse 10,000-vertex directed cycle (10,000 edges) and a dense
500-vertex graph with all forward edges plus one closing edge (124,751 edges). Both are strongly
connected, and the first 1 or 10 edges in the fixed evidence sequence are cycle participants. These
cases isolate index construction cost when the recorder receives only 1 or 10 evidence items while
still emitting ordinary baseline candidates.

For the sparse cycle, a legacy search from the first edge visits the full graph. In the dense graph,
the legacy traversal can find a return path after visiting only a few nodes for the first evidence
item, depending on `HashSet` iteration order. That makes the dense one-item case a useful example of
where unconditional index construction costs more than one favorable reachability search.

Environment: .NET SDK 10.0.403, runtime .NET 10.0.14, Windows 10.0.26300 x64, AMD Ryzen 7 255
(8 cores / 16 logical processors). The repository pins SDK 10.0.402, which was unavailable for
this run; only `rollForward` was temporarily changed to `latestPatch` for commands and the exact
checked-in `global.json` bytes were restored afterward. Benchmark source identity:
`b59086a772c93566093e3872700f4c58371c5b49`.

## Results

| Evidence count | Legacy median ms | Indexed median ms | Local speedup | Legacy allocated bytes | Indexed allocated bytes | Allocation reduction | Legacy retained delta bytes | Indexed retained delta bytes | Peak WS delta bytes (legacy / indexed) |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | 0.0394 | 0.0199 | 1.98x | 32,504 | 3,768 | 88.4% | 131,296 | 2,312 | 0 / 0 |
| 1,000 | 0.3430 | 0.0831 | 4.13x | 320,544 | 11,008 | 96.6% | 464,960 | 5,872 | 0 / 0 |
| 10,000 | 2.9243 | 0.3868 | 7.56x | 3,251,520 | 133,984 | 95.9% | 7,931,656 | 67,336 | 0 / 0 |

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
searches are zero, the 10,000-item median is 7.56x faster, and per-call allocation is 95.9% lower.
The table is the paired run from the cited source identity. Local timing is sensitive to warm-up and
runtime scheduling. Deterministic work counts and matching output hashes are the stronger evidence.

### Large graphs with 1 or 10 evidence items

Each value is the median of five warmed direct `Record` calls. Graph, evidence, and recorder
construction are outside the timed boundary. All four cases emitted the same candidate sequence in
the legacy and indexed implementations.

| Graph | Evidence / candidates | Legacy median ms / allocated bytes | Indexed median ms / allocated bytes | Recorder-only comparison |
|---|---:|---:|---:|---|
| Sparse cycle, 10,000 vertices / 10,000 edges | 1 / 1 | 0.3692 / 673,384 | 1.8384 / 2,204,104 | Indexed 4.98x slower; 227% more allocation |
| Sparse cycle, 10,000 vertices / 10,000 edges | 10 / 10 | 4.1556 / 6,731,848 | 1.4523 / 2,204,344 | Indexed 2.86x faster; 67.3% less allocation |
| Dense cycle, 500 vertices / 124,751 edges | 1 / 1 | 0.0072 / 8,808 | 3.8972 / 1,105,848 | Indexed about 541x slower; about 126x the allocation |
| Dense cycle, 500 vertices / 124,751 edges | 10 / 10 | 16.0409 / 468,768 | 2.2866 / 1,106,088 | Indexed 7.02x faster; 136% more allocation |

The work counters explain the shape of these results. The sparse legacy runs performed 1 and 10
reachability searches, with 10,000 and 100,000 node pops respectively; the dense runs performed 1
and 10 searches, with 3 and 1,096,017 node pops. In both graphs, every indexed call built exactly
one SCC index, prepared all vertices and edges, and performed zero reachability searches. Each
graph edge was visited once in each index-preparation pass (adjacency, reverse degree, reverse fill,
first SCC pass, and second SCC pass).

The dense one-item case shows that SCC construction can dominate when one favorable legacy search
finds a short return path. At 10 items, indexing was faster on both graphs, although it allocated
more in the dense case. These are synthetic recorder-only measurements: they do not estimate the
frequency of these shapes in consumer projects or imply whole-analysis performance. The production
algorithm still builds the index for every enabled non-empty evidence set; the measurements document
that small-input tradeoff without changing the issue's indexed implementation.

Deterministic identities for the added cases:

| Graph | Graph SHA-256 | Evidence count | Evidence SHA-256 | Candidate-array SHA-256 (legacy = indexed) |
|---|---|---:|---|---|
| Sparse cycle, 10,000 vertices / 10,000 edges | `34e29e2eff778aed93c9ee3e7a61e4861a65bf703becd903b67669393b2e59e8` | 1 | `3cddb4f7db53ae820a6f8d8f885cfe7874f93aee95367e456a3fe2471fc680a6` | `bea70851e5645c5100849981f4adc7b9cb7c197c662424202acf1220b42dca3e` |
| Sparse cycle, 10,000 vertices / 10,000 edges | `34e29e2eff778aed93c9ee3e7a61e4861a65bf703becd903b67669393b2e59e8` | 10 | `b1ed5ec9c017e16b79739d2c61fdbd1410fc3b43e6f09e6c042f4eb78b3ad274` | `e7a0713e458b1592397cad6d8ae77669d2b33ef3bdbda0df0dd0aa52b227d6da` |
| Dense cycle, 500 vertices / 124,751 edges | `ee7bacf65a74b0bc16bfb78c45353f3d6566fd286956cad24404d3549750ea7d` | 1 | `835a35d418b084dac1c2a1f54c3a59cf9cfbca97e0929335d45c62b66e30f9a5` | `6b3289ce6e40eeaa608f73aa8627d8668c74f0240be59b921d44f0c94aa39fdb` |
| Dense cycle, 500 vertices / 124,751 edges | `ee7bacf65a74b0bc16bfb78c45353f3d6566fd286956cad24404d3549750ea7d` | 10 | `bdc333fdc4e2634ba02f3132b67ec30598698624810be499aaeb49a1aa5b7741` | `c56b37b7cf820ffbbcd647bc07234915037cd92bcec6cd291193fee29930cdc3` |

## Reproduction

From the repository root with restore assets present, set the source identity and run the explicit
matrix:

```powershell
$env:ARCHLINTER_CYCLE_RECORDER_SOURCE_IDENTITY = 'b59086a772c93566093e3872700f4c58371c5b49'
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
