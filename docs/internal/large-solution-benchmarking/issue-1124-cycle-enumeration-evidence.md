# Issue #1124: SCC pruning for cycle enumeration

## Workload and method

The direct detector workload uses complete ordered DAGs `D(V,E)`, with vertices
`N00..N(V-1)`, edge `i -> j` when `i < j`, and `E=V(V-1)/2`. The output-heavy
control is a complete directed strongly connected component without self-edges,
with `E=V(V-1)` and every simple cycle rotation required in the output.

Baseline source is `d74b4422c21707b1afea5e79fbdd5952597b671f`. The baseline detector
was measured directly from that source. Its recursive visits and edge candidates
were counted with a faithful separate recursion model. Candidate measurements
use the explicit `ArchitectureCycleDetectorBenchmarkHarness`, which times normal
detector calls before invoking the internal work counter. Allocation is per call
from `GC.GetAllocatedBytesForCurrentThread`. Retained memory is the forced-GC
managed-heap delta around the workload. Peak working set is a process high-water
delta and is sensitive to unrelated activity in the test process.

Environment: .NET SDK 10.0.403, runtime .NET 10.0.14, Windows 10.0.26300 x64,
AMD Ryzen 7 255 (8 cores / 16 logical processors). The benchmark runs used SDK
10.0.403 because the repository-pinned 10.0.402 was initially unavailable. SDK
10.0.402 was installed afterward and used for final validation.

## Complete ordered DAG

The baseline follows every path from every start, despite returning no cycles.
For these graphs, its work model is `2^V - 1` recursive visits and
`2^V - V - 1` edge candidates. Candidate work records one ordered adjacency
build, one reverse-degree and reverse-fill visit per edge, and one visit per
vertex and edge in each SCC pass. All cycle-adjacency filtering and cycle search
counts are zero because no component is cyclic.

| V | E | baseline visits | baseline edge candidates | baseline ms | baseline allocated bytes | candidate ms | candidate allocated bytes | candidate retained delta bytes | candidate peak WS delta bytes |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 8 | 28 | 255 | 247 | 0.1119 | 93,504 | — | — | — | — |
| 10 | 45 | 1,023 | 1,013 | — | — | 0.0179 | 7,952 | 1,480 | 0 |
| 12 | 66 | 4,095 | 4,083 | 1.7211 | 1,439,296 | — | — | — | — |
| 16 | 120 | 65,535 | 65,519 | 21.0161 | 22,906,688 | — | — | — | — |
| 20 | 190 | 1,048,575 | 1,048,555 | 381.7355 | 366,323,200 | 0.1694 | 17,728 | 576 | 0 |
| 30 | 435 | 1,073,741,823 | 1,073,741,793 | model only | model only | 0.1632 | 29,040 | 816 | 0 |
| 50 | 1,225 | 1,125,899,906,842,623 | 1,125,899,906,842,573 | model only | model only | 0.3145 | 62,920 | 1,392 | 0 |

Candidate samples used five calls at V=10, one at V=20, V=30, and V=50. Baseline
used five at V=8/12, three at V=16, and one at V=20. Table timings are medians
where multiple samples exist and single observations otherwise. Candidate empty
output had SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`
at every size. At each candidate size, the work counter observed exactly `V`
prepared vertices, `E` prepared edges, `V` sorted adjacency lists, `E` visits in
each reverse-array pass, `V` and `E` visits in each SCC pass, and zero cycle
filter, search, or emitted-path work. At V=30 and V=50, baseline work and memory
are formula projections only; the exponential detector was not run at those sizes.

## Complete strongly connected component

This control confirms that SCC pruning retains the existing output-heavy case.
Baseline recursive work is `V × (1 + ΣP(V-1,k), k=1..V-1)` visits and
`(V-1)` edge candidates per visit. Candidate cycle-search work matches those
counts exactly; the SCC classification adds linear preparation and traversal.
For each candidate SCC, adjacency was sorted once per vertex; reverse degree and
fill each visited every edge once; each forward/reverse SCC pass visited every
vertex and edge once; cycle-adjacency filtering visited each edge once with no
copy pass because all edges remain inside the component.

| V | E | baseline path visits | baseline edge candidates | candidate cycle-search visits | candidate cycle-edge candidates | emitted paths |
|---:|---:|---:|---:|---:|---:|---:|
| 4 | 12 | 64 | 192 | 64 | 192 | 60 |
| 6 | 30 | 1,956 | 9,780 | 1,956 | 9,780 | 1,950 |
| 8 | 56 | 109,600 | 767,200 | 109,600 | 767,200 | 109,592 |

| V | E | cycle outputs | baseline ms | baseline allocated bytes | baseline retained delta bytes | baseline peak WS delta bytes | candidate ms | candidate allocated bytes | candidate retained delta bytes | candidate peak WS delta bytes | output SHA-256 |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 4 | 12 | 60 | 0.0889 | 61,632 | 9,696 | 49,152 | 0.0412 | 16,480 | -21,400 | 0 | `6c8a44983a59f64ad6b0867c95b78450bad793681993cf086228e13493cf2520` |
| 6 | 30 | 1,950 | 4.0834 | 2,083,536 | 369,816 | 6,791,168 | 1.8375 | 478,264 | 416,680 | 0 | `4da77ca9eca9073369430a64cbf5416612e7edb0cc3755505929d0496c9fe247` |
| 8 | 56 | 109,592 | 334.2337 | 124,920,776 | 26,593,984 | 45,703,168 | 168.091 | 31,524,936 | 29,525,656 | 17,379,328 | `7da2392138e8a87fe9590cf7407aee8a6de3342aa64aacce4fa8919c312d6bca` |

Baseline and candidate output counts and digests match at every size. Baseline
used five/three/one timing samples at V=4/6/8; candidate used five/five/one.
Retained-heap deltas vary with process lifetime and GC state, so they are
reported as observed rather than treated as a stable comparison. Peak working
set is also a process high-water value. Candidate DAG retained deltas remained
between 576 and 1,480 bytes at the larger measured sizes, while per-call
allocation grew with `V+E` preparation.

## Interpretation and limits

The measured acyclic path explosion is removed: the detector returns zero
cycles without entering cycle enumeration, and its counted work follows the
prepared graph size. The V=20 local DAG sample changed from 381.7355 ms and
366,323,200 allocated bytes at baseline to 0.1694 ms and 17,728 bytes in the
candidate. These timings are machine-local, with one sample at V=20; work counts
and allocations provide the stronger evidence for the structural bound.

The detector still enumerates every required simple cycle within cyclic SCCs,
so its time and output memory can remain exponential there. The existing
AnalysisProfile harness does not invoke cycle contracts or expose a cycle-phase
boundary. Therefore the cycle phase's share of whole-analysis time (`p`) and an
end-to-end speedup bound are **not measured**. No whole-analysis speedup claim is
made.

Reproduction command from the repository directory (with restore assets present):

```powershell
$env:ARCHLINTER_CYCLE_BENCHMARK_SIZES = '10,20,30,50'
$env:ARCHLINTER_CYCLE_SOURCE_IDENTITY = 'd74b4422 plus issue-1124 candidate'
dotnet test tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~ArchitectureCycleDetectorBenchmarkHarness' --logger 'console;verbosity=normal'
```

The complete-SCC matrix defaults to V=4/6/8. The harness caps this control at
eight vertices because output cardinality grows factorially.
