# Deferred hot-path measurement evidence (#655)

## Decision

This is an evidence-first measurement. No selector, graph, cache, prepared-analysis, or public-API optimization is implemented by this issue.

The checked-in machine-readable artifact is deferred-hot-path-analysis-results.json. It retains the raw analysis-profile/v1 payload for every measured CLI run. The source identity is synthetic-current-tree; values are observations of .NET 10.0.10 on macOS 15.8.0 (X64), configuration Debug.

## Methodology

- Reuse the #502 large-solution-benchmark/v1 synthetic workload generator and materializer.
- Vary independent dimensions at small, medium, and large sizes where the hypothesis is measurable.
- Use deterministic workload counters before interpreting wall-clock phase values.
- Selector evidence uses the runtime selector_predicate_evaluation phase count; P, T, L, and S are varied one at a time.
- Sequential/bounded evidence runs identical staged inputs in paired processes and retains allocation, peak-working-set availability, concurrency, and canonical-result data.
- Graph evidence covers linear, wide fan-out/fan-in, diamond, dense, and structural-only SCC shapes from #502.
- Preserve canonical-result SHA-256 identity and completion/exit status for each profile.
- Use staged assemblies to keep fixture compilation outside analyzer preparation; use real MSBuild only for the cache-eligibility lane.
- The explicit harness is DeferredHotPathBenchmarkHarness; it is excluded from normal test and acceptance gates.

## Environment

- Runtime: .NET 10.0.10
- Operating system: macOS 15.8.0
- Architecture: X64
- Configuration: Debug
- Tool boundary: ArchLinterNet.Cli analysis-profile/v1

## Outcomes

| Finding | Outcome | Scale variable | Measurements | Routing |
|---|---|---|---:|---|
| type-layer-membership-amplification | **B** | P/T/L/S independently | 12 | No child issue; close the hypothesis for the current v0.9 lane. |
| repeated-selector-classification | **C** | C=contracts_per_workload | 3 | Outcome C; retain the result as adoption guidance only. |
| graph-reachability-witness | **B** | shape∈{linear,wide,diamond,dense,SCC}; P=projects | 12 | No graph optimization issue; close as not material on the measured current tree. |
| cross-process-preparation | **D** | R=independent_processes | 7 | Route to #492/#493; do not duplicate persisted prepared-analysis work here. |
| exact-request-cache-eligibility | **D** | P=projects | 6 | Route to #675; preserve fail-closed cache semantics. |
| public-api-cross-process-reuse | **D** | R=public_api_processes | 0 | Route to #498, subject to the #493 materiality gate. |
| consumer-forced-sequential-vs-bounded-parallelism | **B** | mode∈{sequential,bounded}; shape∈{linear,wide,diamond,dense} | 24 | No parallelism change; retain the paired evidence and continue attribution in the owning performance/adoption lanes. |

### 1. Type/layer membership amplification

Layer and selector membership may rescan the same immutable type universe for each declared layer. **Outcome B.** Current model: The generated policy executes one compiled selector predicate per matching type/layer pair (P×T×L); S independently increases the CEL terms inside that predicate. The analysis-profile selector_predicate_evaluation phase records the predicates actually evaluated. P, T, and L are varied independently and increase the runtime counter; S is varied independently while the invocation count remains attributable to the same predicate boundary. The runtime counter and independent matrix make selector work attributable, but the selector phase is not material enough in the measured end-to-end profiles to justify a precomputed Type→layers implementation. Routing: No child issue; close the hypothesis for the current v0.9 lane.
### 2. Repeated selector/classification work across contract families

Multiple contract families may repeatedly scan the same type/reference/source universe despite existing indexes. **Outcome C.** Current model: Contract-family execution grows with requested contract count while one source fact-index materialization remains available to the snapshot. Across one, four, and eight synthetic contracts, FactIndexMaterializations remains bounded at one; source scanning is not applicable to the staged-assembly mode; ContractFamilyCounts identify the requested family work. The measured shape is already served by the existing immutable snapshot/fact-index boundary when used correctly. No shared projection or Core change is justified. Routing: Outcome C; retain the result as adoption guidance only.
### 3. Graph/reachability/witness work

Repeated traversal, alternate-path closure, or witness reconstruction may amplify with graph density. **Outcome B.** Current model: The reusable corpus reports graph edges and alternate paths; this validation path does not expose a graph-traversal counter independent of the selected contract families. Linear, wide fan-out/fan-in, diamond, and dense synthetic graphs are measured at three project sizes; CyclicScc is retained as structural-only because the #502 materializer rejects cyclic project compilation. All executable shapes preserve canonical results. The required topology space is covered. The evidence remains insufficient for an independently attributable material graph-specific phase or witness counter, so no graph optimization issue is justified. Routing: No graph optimization issue; close as not material on the measured current tree.

Topology coverage:

| Shape | Projects | Edges | SCCs | Cycle | Status |
|---|---:|---:|---:|---|---|
| Linear | 4 | 3 | 4 | no | materialized-and-measured |
| Linear | 8 | 7 | 8 | no | materialized-and-measured |
| Linear | 12 | 11 | 12 | no | materialized-and-measured |
| WideFanOutFanIn | 4 | 4 | 4 | no | materialized-and-measured |
| WideFanOutFanIn | 8 | 12 | 8 | no | materialized-and-measured |
| WideFanOutFanIn | 12 | 20 | 12 | no | materialized-and-measured |
| Diamond | 4 | 4 | 4 | no | materialized-and-measured |
| Diamond | 8 | 11 | 8 | no | materialized-and-measured |
| Diamond | 12 | 19 | 12 | no | materialized-and-measured |
| Dense | 4 | 6 | 4 | no | materialized-and-measured |
| Dense | 8 | 28 | 8 | no | materialized-and-measured |
| Dense | 12 | 66 | 12 | no | materialized-and-measured |
| CyclicScc | 4 | 4 | 1 | yes | structural-only |
| CyclicScc | 8 | 8 | 1 | yes | structural-only |
| CyclicScc | 12 | 12 | 1 | yes | structural-only |
### 4. Cross-process build/preflight/fact repetition

Independent read-only processes may repeat project, build-state, assembly, and fact preparation over one unchanged build. **Outcome D.** Current model: Preparation and fact counters repeat once per process; bounded parallel dispatch can hide elapsed duplication without removing it. Repeated strict processes retain equivalent canonical results while ProjectGraphEvaluations and FactIndexMaterializations repeat per process; source scanning is not applicable to the staged-assembly mode. This is an existing prepared-analysis decision boundary, not a new #655 implementation lane. Routing: Route to #492/#493; do not duplicate persisted prepared-analysis work here.
### 5. Exact-request cache eligibility and recomputation

Real MSBuild projects may remain cache-ineligible or recompute despite an opt-in exact-request cache. **Outcome D.** Current model: Cache eligibility, misses, hits, and rejected units are governed by analysis-cache/v1 evaluated-input authorization. The cache-enabled real-MSBuild matrix records the actual Cache counters and never treats an ineligible unit as a successful hit. Cache trust and eligibility are explicitly owned by the existing real-MSBuild lane; #655 must not relax authorization or duplicate its measurements. Routing: Route to #675; preserve fail-closed cache semantics.
### 6. Public-API cross-process reuse

Independent public-api diff processes may repeat exported-surface preparation. **Outcome D.** Current model: Earlier in-process memoization does not prove that persisted exported-surface facts are safe or material. No public-api-specific prepared fact boundary is exercised by the reusable validation fixture in this lane. The public-API consumer and materiality gate already have a dedicated owner; no duplicate persistence evidence is created here. Routing: Route to #498, subject to the #493 materiality gate.
### 7. Consumer-forced sequential execution versus bounded parallelism

Consumers may force --max-parallelism 1 across assembly-consuming commands even though deterministic bounded parallel scanning is already available. **Outcome B.** Current model: The same immutable staged-assembly inputs are executed once sequentially and once with the resolved default bounded degree; type-loading work is partitioned by target assembly. Each executable #502 graph shape is paired at three project sizes. The bounded profile reports active concurrency, while the sequential profile reports NotApplicable concurrency; allocations, peak working-set availability, and canonical-result digests are retained for both variants. The existing bounded capability is exercised and semantically equivalent, but it does not materially improve the measured hot phase and generally increases managed allocation; peak working-set data is unavailable on this host. No Core change is justified by this matrix, and concurrency must not mask the remaining bottleneck. Routing: No parallelism change; retain the paired evidence and continue attribution in the owning performance/adoption lanes.

Topology coverage:

| Shape | Projects | Edges | SCCs | Cycle | Status |
|---|---:|---:|---:|---|---|
| Linear | 4 | 3 | 4 | no | materialized-and-measured |
| Linear | 8 | 7 | 8 | no | materialized-and-measured |
| Linear | 12 | 11 | 12 | no | materialized-and-measured |
| WideFanOutFanIn | 4 | 4 | 4 | no | materialized-and-measured |
| WideFanOutFanIn | 8 | 12 | 8 | no | materialized-and-measured |
| WideFanOutFanIn | 12 | 20 | 12 | no | materialized-and-measured |
| Diamond | 4 | 4 | 4 | no | materialized-and-measured |
| Diamond | 8 | 11 | 8 | no | materialized-and-measured |
| Diamond | 12 | 19 | 12 | no | materialized-and-measured |
| Dense | 4 | 6 | 4 | no | materialized-and-measured |
| Dense | 8 | 28 | 8 | no | materialized-and-measured |
| Dense | 12 | 66 | 12 | no | materialized-and-measured |
| CyclicScc | 4 | 4 | 1 | yes | structural-only |
| CyclicScc | 8 | 8 | 1 | yes | structural-only |
| CyclicScc | 12 | 12 | 1 | yes | structural-only |

## Measurement rows

| Finding | Workload | Variant | Dimension | Size | Work | Observed counter | Dominant phase | Allocated bytes | Peak working set | Canonical result |
|---|---|---|---|---|---:|---|---|---:|---:|---|
| type-layer-membership-amplification | synthetic-selector-p-projects-small | sequential | P=projects | projects-small | 137 | phase.selector_predicate_evaluation.count=112 | total 448.0 ms | 4885856 | unavailable | 59bf176490f26ed54d7936686b1c4893886e2dc85a7c11679cf7f920f738f77f |
| type-layer-membership-amplification | synthetic-selector-p-projects-medium | sequential | P=projects | projects-medium | 277 | phase.selector_predicate_evaluation.count=224 | total 418.0 ms | 6267608 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| type-layer-membership-amplification | synthetic-selector-p-projects-large | sequential | P=projects | projects-large | 557 | phase.selector_predicate_evaluation.count=448 | total 432.0 ms | 9039832 | unavailable | edc48d888fb1dba2edc6da1351ab7e1279e70b657821e0fdb7537c3f94236c20 |
| type-layer-membership-amplification | synthetic-selector-t-types-small | sequential | T=types_per_project | types-small | 141 | phase.selector_predicate_evaluation.count=112 | total 407.0 ms | 5029224 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| type-layer-membership-amplification | synthetic-selector-t-types-medium | sequential | T=types_per_project | types-medium | 277 | phase.selector_predicate_evaluation.count=224 | total 413.0 ms | 6266832 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| type-layer-membership-amplification | synthetic-selector-t-types-large | sequential | T=types_per_project | types-large | 549 | phase.selector_predicate_evaluation.count=448 | total 415.0 ms | 8804136 | unavailable | f0ab5f64006c274d44bdb070f79f36d6f9c1bd702ee9c3852ea5bf50e8a345a5 |
| type-layer-membership-amplification | synthetic-selector-l-layers-small | sequential | L=layers | layers-small | 149 | phase.selector_predicate_evaluation.count=128 | total 422.0 ms | 4997944 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| type-layer-membership-amplification | synthetic-selector-l-layers-medium | sequential | L=layers | layers-medium | 277 | phase.selector_predicate_evaluation.count=224 | total 432.0 ms | 6262536 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| type-layer-membership-amplification | synthetic-selector-l-layers-large | sequential | L=layers | layers-large | 533 | phase.selector_predicate_evaluation.count=416 | total 424.0 ms | 8922808 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| type-layer-membership-amplification | synthetic-selector-s-terms-small | sequential | S=selector_terms_per_layer | terms-small | 149 | phase.selector_predicate_evaluation.count=224 | total 411.0 ms | 6059568 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| type-layer-membership-amplification | synthetic-selector-s-terms-medium | sequential | S=selector_terms_per_layer | terms-medium | 277 | phase.selector_predicate_evaluation.count=224 | total 412.0 ms | 6270408 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| type-layer-membership-amplification | synthetic-selector-s-terms-large | sequential | S=selector_terms_per_layer | terms-large | 533 | phase.selector_predicate_evaluation.count=224 | total 388.0 ms | 6695936 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| repeated-selector-classification | synthetic-classification-small | sequential | C=contracts | small | 85 | Counters.FactIndexMaterializations=1 | total 380.0 ms | 4689048 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| repeated-selector-classification | synthetic-classification-medium | sequential | C=contracts | medium | 85 | Counters.FactIndexMaterializations=1 | total 383.0 ms | 5286496 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| repeated-selector-classification | synthetic-classification-large | sequential | C=contracts | large | 85 | Counters.FactIndexMaterializations=1 | total 394.0 ms | 6091344 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| graph-reachability-witness | synthetic-graph-linear-small | sequential | P=projects within topology shape | small | 43 | workload.reference_edge_count=3 | total 379.0 ms | 4185992 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| graph-reachability-witness | synthetic-graph-linear-medium | sequential | P=projects within topology shape | medium | 87 | workload.reference_edge_count=7 | total 382.0 ms | 5215848 | unavailable | ab50df6c42c541a8710f8eca7fecdee77716ffba9b89deaffb1536281bd3a455 |
| graph-reachability-witness | synthetic-graph-linear-large | sequential | P=projects within topology shape | large | 131 | workload.reference_edge_count=11 | total 387.0 ms | 6136680 | unavailable | dd7997c1b554851f6e086ba148f66453baab87af7cced5e97cb0ad9fdc740b5b |
| graph-reachability-witness | synthetic-graph-widefanoutfanin-small | sequential | P=projects within topology shape | small | 44 | workload.reference_edge_count=4 | total 379.0 ms | 4194192 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| graph-reachability-witness | synthetic-graph-widefanoutfanin-medium | sequential | P=projects within topology shape | medium | 92 | workload.reference_edge_count=12 | total 384.0 ms | 5172888 | unavailable | ab50df6c42c541a8710f8eca7fecdee77716ffba9b89deaffb1536281bd3a455 |
| graph-reachability-witness | synthetic-graph-widefanoutfanin-large | sequential | P=projects within topology shape | large | 140 | workload.reference_edge_count=20 | total 385.0 ms | 6143816 | unavailable | dd7997c1b554851f6e086ba148f66453baab87af7cced5e97cb0ad9fdc740b5b |
| graph-reachability-witness | synthetic-graph-diamond-small | sequential | P=projects within topology shape | small | 44 | workload.reference_edge_count=4 | total 381.0 ms | 4194192 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| graph-reachability-witness | synthetic-graph-diamond-medium | sequential | P=projects within topology shape | medium | 91 | workload.reference_edge_count=11 | total 380.0 ms | 5169912 | unavailable | ab50df6c42c541a8710f8eca7fecdee77716ffba9b89deaffb1536281bd3a455 |
| graph-reachability-witness | synthetic-graph-diamond-large | sequential | P=projects within topology shape | large | 139 | workload.reference_edge_count=19 | total 388.0 ms | 6162944 | unavailable | dd7997c1b554851f6e086ba148f66453baab87af7cced5e97cb0ad9fdc740b5b |
| graph-reachability-witness | synthetic-graph-dense-small | sequential | P=projects within topology shape | small | 46 | workload.reference_edge_count=6 | total 380.0 ms | 4185992 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| graph-reachability-witness | synthetic-graph-dense-medium | sequential | P=projects within topology shape | medium | 108 | workload.reference_edge_count=28 | total 380.0 ms | 5168456 | unavailable | ab50df6c42c541a8710f8eca7fecdee77716ffba9b89deaffb1536281bd3a455 |
| graph-reachability-witness | synthetic-graph-dense-large | sequential | P=projects within topology shape | large | 186 | workload.reference_edge_count=66 | total 390.0 ms | 6140400 | unavailable | dd7997c1b554851f6e086ba148f66453baab87af7cced5e97cb0ad9fdc740b5b |
| cross-process-preparation | synthetic-cross-process-medium | sequential | R=processes | 1-process | 86 | Counters.ProjectGraphEvaluations=1 | total 385.0 ms | 4869464 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| cross-process-preparation | synthetic-cross-process-medium | sequential | R=processes | 2-process | 86 | Counters.ProjectGraphEvaluations=1 | total 380.0 ms | 4885808 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| cross-process-preparation | synthetic-cross-process-medium | sequential | R=processes | 2-process | 86 | Counters.ProjectGraphEvaluations=1 | total 384.0 ms | 4894048 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| cross-process-preparation | synthetic-cross-process-medium | sequential | R=processes | 4-process | 86 | Counters.ProjectGraphEvaluations=1 | total 379.0 ms | 4869376 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| cross-process-preparation | synthetic-cross-process-medium | sequential | R=processes | 4-process | 86 | Counters.ProjectGraphEvaluations=1 | total 421.0 ms | 4885864 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| cross-process-preparation | synthetic-cross-process-medium | sequential | R=processes | 4-process | 86 | Counters.ProjectGraphEvaluations=1 | total 381.0 ms | 4943216 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| cross-process-preparation | synthetic-cross-process-medium | sequential | R=processes | 4-process | 86 | Counters.ProjectGraphEvaluations=1 | total 378.0 ms | 4869360 | unavailable | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| exact-request-cache-eligibility | synthetic-cache-small | sequential | P=projects | small-population | 21 | Counters.Cache.IneligibleUnitCount=0 | total 2258.0 ms | 124889480 | unavailable | 0f0f13c903defc168a7b944bde950e61294c5eeffd4fbef4c4100f4fd0f9da46 |
| exact-request-cache-eligibility | synthetic-cache-small | sequential | P=projects | small-repeat | 21 | Counters.Cache.Hits=1 | total 2114.0 ms | 60196328 | unavailable | 0f0f13c903defc168a7b944bde950e61294c5eeffd4fbef4c4100f4fd0f9da46 |
| exact-request-cache-eligibility | synthetic-cache-medium | sequential | P=projects | medium-population | 45 | Counters.Cache.IneligibleUnitCount=0 | total 2776.0 ms | 127756992 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| exact-request-cache-eligibility | synthetic-cache-medium | sequential | P=projects | medium-repeat | 45 | Counters.Cache.Hits=1 | total 2574.0 ms | 61891192 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| exact-request-cache-eligibility | synthetic-cache-large | sequential | P=projects | large-population | 69 | Counters.Cache.IneligibleUnitCount=0 | total 3898.0 ms | 130493280 | unavailable | dc8beec847ebb8a2d099bacd821fd94aff9d81186304f6548f97fdc07ddc8dfd |
| exact-request-cache-eligibility | synthetic-cache-large | sequential | P=projects | large-repeat | 69 | Counters.Cache.Hits=1 | total 3211.0 ms | 63627544 | unavailable | dc8beec847ebb8a2d099bacd821fd94aff9d81186304f6548f97fdc07ddc8dfd |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-linear-small | sequential | P=projects | small | 43 | Counters.Concurrency.MaxParallelism=0 | total 378.0 ms | 4194192 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-linear-small | bounded-default | P=projects | small | 43 | Counters.Concurrency.MaxParallelism=4 | total 384.0 ms | 4259432 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-linear-medium | sequential | P=projects | medium | 87 | Counters.Concurrency.MaxParallelism=0 | total 388.0 ms | 5161312 | unavailable | ab50df6c42c541a8710f8eca7fecdee77716ffba9b89deaffb1536281bd3a455 |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-linear-medium | bounded-default | P=projects | medium | 87 | Counters.Concurrency.MaxParallelism=4 | total 389.0 ms | 5185080 | unavailable | ab50df6c42c541a8710f8eca7fecdee77716ffba9b89deaffb1536281bd3a455 |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-linear-large | sequential | P=projects | large | 131 | Counters.Concurrency.MaxParallelism=0 | total 390.0 ms | 6150712 | unavailable | dd7997c1b554851f6e086ba148f66453baab87af7cced5e97cb0ad9fdc740b5b |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-linear-large | bounded-default | P=projects | large | 131 | Counters.Concurrency.MaxParallelism=4 | total 396.0 ms | 6179976 | unavailable | dd7997c1b554851f6e086ba148f66453baab87af7cced5e97cb0ad9fdc740b5b |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-widefanoutfanin-small | sequential | P=projects | small | 44 | Counters.Concurrency.MaxParallelism=0 | total 381.0 ms | 4185992 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-widefanoutfanin-small | bounded-default | P=projects | small | 44 | Counters.Concurrency.MaxParallelism=4 | total 383.0 ms | 4251296 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-widefanoutfanin-medium | sequential | P=projects | medium | 92 | Counters.Concurrency.MaxParallelism=0 | total 382.0 ms | 5223392 | unavailable | ab50df6c42c541a8710f8eca7fecdee77716ffba9b89deaffb1536281bd3a455 |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-widefanoutfanin-medium | bounded-default | P=projects | medium | 92 | Counters.Concurrency.MaxParallelism=4 | total 387.0 ms | 5193512 | unavailable | ab50df6c42c541a8710f8eca7fecdee77716ffba9b89deaffb1536281bd3a455 |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-widefanoutfanin-large | sequential | P=projects | large | 140 | Counters.Concurrency.MaxParallelism=0 | total 388.0 ms | 6155752 | unavailable | dd7997c1b554851f6e086ba148f66453baab87af7cced5e97cb0ad9fdc740b5b |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-widefanoutfanin-large | bounded-default | P=projects | large | 140 | Counters.Concurrency.MaxParallelism=4 | total 392.0 ms | 6176448 | unavailable | dd7997c1b554851f6e086ba148f66453baab87af7cced5e97cb0ad9fdc740b5b |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-diamond-small | sequential | P=projects | small | 44 | Counters.Concurrency.MaxParallelism=0 | total 380.0 ms | 4185992 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-diamond-small | bounded-default | P=projects | small | 44 | Counters.Concurrency.MaxParallelism=4 | total 384.0 ms | 4251248 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-diamond-medium | sequential | P=projects | medium | 91 | Counters.Concurrency.MaxParallelism=0 | total 380.0 ms | 5162784 | unavailable | ab50df6c42c541a8710f8eca7fecdee77716ffba9b89deaffb1536281bd3a455 |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-diamond-medium | bounded-default | P=projects | medium | 91 | Counters.Concurrency.MaxParallelism=4 | total 387.0 ms | 5186536 | unavailable | ab50df6c42c541a8710f8eca7fecdee77716ffba9b89deaffb1536281bd3a455 |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-diamond-large | sequential | P=projects | large | 139 | Counters.Concurrency.MaxParallelism=0 | total 388.0 ms | 6158928 | unavailable | dd7997c1b554851f6e086ba148f66453baab87af7cced5e97cb0ad9fdc740b5b |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-diamond-large | bounded-default | P=projects | large | 139 | Counters.Concurrency.MaxParallelism=4 | total 396.0 ms | 6181144 | unavailable | dd7997c1b554851f6e086ba148f66453baab87af7cced5e97cb0ad9fdc740b5b |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-dense-small | sequential | P=projects | small | 46 | Counters.Concurrency.MaxParallelism=0 | total 379.0 ms | 4194216 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-dense-small | bounded-default | P=projects | small | 46 | Counters.Concurrency.MaxParallelism=4 | total 386.0 ms | 4251280 | unavailable | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-dense-medium | sequential | P=projects | medium | 108 | Counters.Concurrency.MaxParallelism=0 | total 381.0 ms | 5169880 | unavailable | ab50df6c42c541a8710f8eca7fecdee77716ffba9b89deaffb1536281bd3a455 |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-dense-medium | bounded-default | P=projects | medium | 108 | Counters.Concurrency.MaxParallelism=4 | total 386.0 ms | 5189968 | unavailable | ab50df6c42c541a8710f8eca7fecdee77716ffba9b89deaffb1536281bd3a455 |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-dense-large | sequential | P=projects | large | 186 | Counters.Concurrency.MaxParallelism=0 | total 392.0 ms | 6155872 | unavailable | dd7997c1b554851f6e086ba148f66453baab87af7cced5e97cb0ad9fdc740b5b |
| consumer-forced-sequential-vs-bounded-parallelism | synthetic-parallel-dense-large | bounded-default | P=projects | large | 186 | Counters.Concurrency.MaxParallelism=4 | total 395.0 ms | 6167768 | unavailable | dd7997c1b554851f6e086ba148f66453baab87af7cced5e97cb0ad9fdc740b5b |

## Routing and non-goals

- Cross-process preparation evidence belongs to #492/#493.
- Real-MSBuild cache eligibility belongs to #675.
- Public-API prepared reuse belongs to #498 and remains conditional on the #493 materiality gate.
- No private adopter identity, repository URL, namespace, proprietary topology, or raw private CI log is committed.
- No universal timing SLA is claimed.

OpenSpec: not applicable. This change adds an explicitly invoked internal measurement harness and evidence only; it changes no product behavior, public API, policy semantics, cache trust boundary, or documented user guarantee.
