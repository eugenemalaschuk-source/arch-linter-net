## Context

See `proposal.md` for the motivation and `specs/cycle-contracts/spec.md` for the output contract. `ArchitectureCycleDetector` is internal to Core and is called by both `CycleChecker` and `AcyclicSiblingChecker`. The existing private SCC implementation in `RepositoryMetricsCalculator` uses integer project indexes and recursive Tarjan traversal; extracting it would couple unrelated metrics code and retain recursion in the new classification pass.

### Baseline and expected effect

Baseline revision: `d74b4422c21707b1afea5e79fbdd5952597b671f`. Workload `D(V,E)` is a complete ordered DAG: vertices `N00..N(V-1)`, edge `i -> j` exactly when `i < j`, and `E=V(V-1)/2`. The detector returned zero cycles with the empty-result SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` at every measured size.

An uninstrumented direct call to the baseline detector source was timed and allocation-counted by `ArchitectureCycleDetectorBenchmarkHarness` using `GC.GetAllocatedBytesForCurrentThread`. A separate C# work-count reproduction followed the baseline recursion and adjacency loop and counted recursive visits and edge candidates separately. V=8/12 used five samples, V=16 used three, and V=20 used one; timings below are medians except V=20. Allocation is per detector call. Retained memory is the forced-GC managed-heap delta around the sample. Peak-working-set deltas are high-water changes inside the shared test process and are environment-sensitive.

| V | E | recursive visits | edge candidates | elapsed ms | allocated bytes | retained delta bytes | peak working-set delta bytes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 8 | 28 | 255 | 247 | 0.1119 | 93,504 | 600 | 0 |
| 12 | 66 | 4,095 | 4,083 | 1.7211 | 1,439,296 | 344 | 1,040,384 |
| 16 | 120 | 65,535 | 65,519 | 21.0161 | 22,906,688 | 352 | 1,159,168 |
| 20 | 190 | 1,048,575 | 1,048,555 | 381.7355 | 366,323,200 | 424 | 4,177,920 |

The output-heavy control is a complete directed SCC without self-edges: `E=V(V-1)`. The baseline detector reported every simple cycle rotation. V=4/6 used five/three samples, respectively; V=8 used one because output allocation reached 125 MB. Work counts are from the faithful recursion model: visits are `V × (1 + Σ P(V-1,k), k=1..V-1)`, edge candidates are visits times `V-1`, and every emitted simple path closes back to its start.

| V | E | cycle outputs | recursive visits | edge candidates | elapsed ms | allocated bytes | retained delta bytes | peak working-set delta bytes | output SHA-256 |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 4 | 12 | 60 | 64 | 192 | 0.0889 | 61,632 | 9,696 | 49,152 | `6c8a44983a59f64ad6b0867c95b78450bad793681993cf086228e13493cf2520` |
| 6 | 30 | 1,950 | 1,956 | 9,780 | 4.0834 | 2,083,536 | 369,816 | 6,791,168 | `4da77ca9eca9073369430a64cbf5416612e7edb0cc3755505929d0496c9fe247` |
| 8 | 56 | 109,592 | 109,600 | 767,200 | 334.2337 | 124,920,776 | 26,593,984 | 45,703,168 | `7da2392138e8a87fe9590cf7407aee8a6de3342aa64aacce4fa8919c312d6bca` |

Environment: .NET SDK 10.0.403, runtime .NET 10.0.14, Windows 10.0.26300, x64, AMD Ryzen 7 255 (8 cores / 16 logical processors). The repository pins SDK 10.0.402, which was unavailable locally; 10.0.403 was used. The DAG V=20 and SCC V=8 samples were each run once due their 366 MB and 125 MB allocation totals. Larger baseline DAG wall-time/allocation samples were not run. For the ordered DAG, `2^V - 1` recursive visits and `2^V - V - 1` edge candidates are model values beyond V=20; at V=30 and V=50 these are not executed measurements.

The existing AnalysisProfile harness has no cycle-enumeration phase boundary, and its graph workload does not invoke cycle contracts. Therefore the cycle phase's share of a full analysis (`p`) is **not measured**, and no Amdahl end-to-end speedup bound can be computed from current evidence. The direct detector timing/allocation values above are local measurements only.

Expected local effect: on a DAG, preparing ordered adjacency and the exact-size reverse arrays performs linear graph work plus one adjacency ordering cost of `Σ deg(v) log deg(v)`. The two SCC passes visit each vertex at most twice and scan each edge at most twice. Reverse-array degree counting and filling each scan the edges once. No cycle-path enumeration should run. The current model's exponential DAG traversal disappears. Temporary data should grow with `O(V+E)`; cycle output inside cyclic components remains potentially exponential because the contract requires all simple cycles.

**Success criterion:** preserve the exact complete cycle set and string order on cyclic controls; for dense/layered DAGs at V=10,20,30,50 return no cycles, perform no cycle-enumeration visits, stay within two SCC vertex visits and two SCC edge scans per input vertex/edge, count each reverse-array edge pass once, and order each adjacency once. Direct allocation and peak-memory evidence must scale with the prepared `V+E` graph rather than path count.

**Kill criterion:** reject or revise the design if any existing cycle output changes, the DAG counter bound is exceeded, zero-output DAG allocation/peak-memory growth is not attributable to `O(V+E)` preparation, or SCC classification introduces stack-depth failure. The phase share and absolute end-to-end speedup remain explicitly unmeasured unless the existing profile gains an attributable cycle phase.

## Goals / Non-Goals

**Goals:** discard inter-component edges before enumerating paths; preserve cycle rotations, self-edge behavior supported by the detector, final distinct/sorted output, and the current ordering comparer; keep graph state local to one call; expose only an internal optional work-count seam for structural tests and #1123-compatible evidence.

**Non-Goals:** optimize enumeration within cyclic components, cap or truncate cycle output, add cancellation behavior or policy fields, change public APIs, or reuse/refactor repository-metrics SCC code.

## Decisions

1. **Use an iterative two-pass SCC calculation local to the cycle graph.** Snapshot graph keys in the current default string order, build ordered adjacency once, allocate reverse adjacency arrays from counted in-degrees, and use iterative finishing-order/reverse traversals to assign component identifiers and sizes. Reverse degree counting and filling are each one edge pass. This avoids recursion in SCC classification and preserves the detector's existing key/neighbor ordering. The nested metrics Tarjan type is not a shared abstraction and remains untouched.
2. **Enumerate only inside cyclic components.** A component is cyclic when it has multiple vertices or its singleton has a self-edge. For each start vertex in the original ordered key list, follow only ordered edges with the same component identifier, retaining the current path-local visited rule. Keep final `Distinct()` and string sorting so all cycle rotations and diagnostic order remain unchanged.
3. **Make work counts optional and internal.** Tests and the focused benchmark may request counts for adjacency ordering, SCC vertex/edge scans, cycle-search visits/edge candidates, and emitted paths. Production callers use the no-counter overload, avoiding a public profiler or mandatory counter allocation.
4. **Keep evidence in the existing benchmark foundation.** Retain deterministic work assertions in Core tests and record environment-labelled detector-local timing/allocation samples separately. Do not infer a full-analysis gain while `p` is unmeasured.

## Risks / Trade-offs

- **[Risk]** Reverse adjacency and prepared ordered arrays use `O(V+E)` temporary memory. → Measure allocated and peak memory on the same generated shapes; reject superlinear zero-output DAG growth.
- **[Risk]** All-cycle output and path traversal can still be exponential in a dense cyclic component. → Preserve full output and report this bound honestly; no cap or witness substitution is introduced.
- **[Risk]** Culture-sensitive default string ordering could be accidentally replaced by ordinal ordering. → Use the same default comparer for key, neighbor, and final cycle ordering and retain exact-order regression tests.
- **[Risk]** A detector-only speedup may have little effect on whole-analysis runtime. → Keep phase share and end-to-end bound labeled not measured until the cycle phase is separately attributable.

## Migration Plan

No migration or public API transition is required. Update the existing cycle contract spec through the OpenSpec delta, implement and validate the detector, then archive the change before opening the issue PR.
