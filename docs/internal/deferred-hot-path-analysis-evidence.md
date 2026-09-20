# Deferred hot-path measurement evidence (#655)

## Decision

This is an evidence-first measurement. No selector, graph, cache, prepared-analysis, or public-API optimization is implemented by this issue.

The checked-in machine-readable artifact is deferred-hot-path-analysis-results.json. It retains the raw analysis-profile/v1 payload for every measured CLI run. The source identity is synthetic-current-tree; values are observations of .NET 10.0.10 on macOS 15.8.0 (X64), configuration Debug.

## Methodology

- Reuse the #502 large-solution-benchmark/v1 synthetic workload generator and materializer.
- Vary independent dimensions at small, medium, and large sizes where the hypothesis is measurable.
- Use deterministic workload counters before interpreting wall-clock phase values.
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
| type-layer-membership-amplification | **B** | P×T×L×S | 3 | No child issue; close the hypothesis for the current v0.9 lane. |
| repeated-selector-classification | **C** | C=contracts_per_workload | 3 | Outcome C; retain the result as adoption guidance only. |
| graph-reachability-witness | **B** | E=reference_edges | 3 | No graph optimization issue; close as not material on the measured current tree. |
| cross-process-preparation | **D** | R=independent_processes | 7 | Route to #492/#493; do not duplicate persisted prepared-analysis work here. |
| exact-request-cache-eligibility | **D** | P=projects | 6 | Route to #675; preserve fail-closed cache semantics. |
| public-api-cross-process-reuse | **D** | R=public_api_processes | 0 | Route to #498, subject to the #493 materiality gate. |

### 1. Type/layer membership amplification

Layer and selector membership may rescan the same immutable type universe for each declared layer. **Outcome B.** Current model: Generated selector evaluation work is P×T×L×S; no selector-specific runtime counter is exposed by analysis-profile/v1. The deterministic workload counter grows with the declared product while the profiled validation boundary remains dominated by existing indexed preparation/contract phases. Measurable synthetic selector work was not sufficient to justify a precomputed Type→layers implementation. The current evidence cannot separate selector predicate cost from the surrounding contract phase without new instrumentation. Routing: No child issue; close the hypothesis for the current v0.9 lane.

### 2. Repeated selector/classification work

Multiple contract families may repeatedly scan the same type/reference/source universe despite existing indexes. **Outcome C.** Current model: Contract-family execution grows with requested contract count while one source fact-index materialization remains available to the snapshot. Across one, four, and eight synthetic contracts, FactIndexMaterializations remains bounded at one; source scanning is not applicable to the staged-assembly mode; ContractFamilyCounts identify the requested family work. The measured shape is already served by the existing immutable snapshot/fact-index boundary when used correctly. No shared projection or Core change is justified. Routing: Outcome C; retain the result as adoption guidance only.

### 3. Graph/reachability/witness work

Repeated traversal, alternate-path closure, or witness reconstruction may amplify with graph density. **Outcome B.** Current model: The reusable corpus reports graph edges and alternate paths; this validation path does not expose a graph-traversal counter independent of the selected contract families. Dense synthetic graphs at three sizes preserve canonical results and expose deterministic edge/alternate-path growth, but no material graph-specific phase or witness counter is reproduced. The graph hypothesis is measurable as workload structure but not material as an independently attributable product hot path in this lane. Routing: No graph optimization issue; close as not material on the measured current tree.

### 4. Cross-process preparation/fact repetition

Independent read-only processes may repeat project, build-state, assembly, and fact preparation over one unchanged build. **Outcome D.** Current model: Preparation and fact counters repeat once per process; bounded parallel dispatch can hide elapsed duplication without removing it. Repeated strict processes retain equivalent canonical results while ProjectGraphEvaluations and FactIndexMaterializations repeat per process; source scanning is not applicable to the staged-assembly mode. This is an existing prepared-analysis decision boundary, not a new #655 implementation lane. Routing: Route to #492/#493; do not duplicate persisted prepared-analysis work here.

### 5. Exact-request cache eligibility

Real MSBuild projects may remain cache-ineligible or recompute despite an opt-in exact-request cache. **Outcome D.** Current model: Cache eligibility, misses, hits, and rejected units are governed by analysis-cache/v1 evaluated-input authorization. The cache-enabled real-MSBuild matrix records the actual Cache counters and never treats an ineligible unit as a successful hit. Cache trust and eligibility are explicitly owned by the existing real-MSBuild lane; #655 must not relax authorization or duplicate its measurements. Routing: Route to #675; preserve fail-closed cache semantics.

### 6. Public-API cross-process reuse

Independent public-api diff processes may repeat exported-surface preparation. **Outcome D.** Current model: Earlier in-process memoization does not prove that persisted exported-surface facts are safe or material. No public-api-specific prepared fact boundary is exercised by the reusable validation fixture in this lane. The public-API consumer and materiality gate already have a dedicated owner; no duplicate persistence evidence is created here. Routing: Route to #498, subject to the #493 materiality gate.

## Measurement rows

| Finding | Workload | Size | Work | Observed counter | Dominant phase | Canonical result |
|---|---|---|---:|---|---|---|
| type-layer-membership-amplification | synthetic-selector-small | small | 21 | workload.selector_predicate_evaluation_count=16 | total 399.0 ms | 0f0f13c903defc168a7b944bde950e61294c5eeffd4fbef4c4100f4fd0f9da46 |
| type-layer-membership-amplification | synthetic-selector-medium | medium | 278 | workload.selector_predicate_evaluation_count=256 | total 388.0 ms | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| type-layer-membership-amplification | synthetic-selector-large | large | 4188 | workload.selector_predicate_evaluation_count=4096 | total 417.0 ms | 63b7b74b5329ad60de76b8c7fd6db2bb3e9866c95ddec4f659ae5c184a22c375 |
| repeated-selector-classification | synthetic-classification-small | small | 85 | Counters.FactIndexMaterializations=1 | total 425.0 ms | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| repeated-selector-classification | synthetic-classification-medium | medium | 85 | Counters.FactIndexMaterializations=1 | total 422.0 ms | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| repeated-selector-classification | synthetic-classification-large | large | 85 | Counters.FactIndexMaterializations=1 | total 419.0 ms | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| graph-reachability-witness | synthetic-graph-small | small | 46 | workload.reference_edge_count=6 | total 432.0 ms | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| graph-reachability-witness | synthetic-graph-medium | medium | 75 | workload.reference_edge_count=15 | total 383.0 ms | dc8beec847ebb8a2d099bacd821fd94aff9d81186304f6548f97fdc07ddc8dfd |
| graph-reachability-witness | synthetic-graph-large | large | 108 | workload.reference_edge_count=28 | total 383.0 ms | ab50df6c42c541a8710f8eca7fecdee77716ffba9b89deaffb1536281bd3a455 |
| cross-process-preparation | synthetic-cross-process-medium | 1-process | 86 | Counters.ProjectGraphEvaluations=1 | total 381.0 ms | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| cross-process-preparation | synthetic-cross-process-medium | 2-process | 86 | Counters.ProjectGraphEvaluations=1 | total 387.0 ms | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| cross-process-preparation | synthetic-cross-process-medium | 2-process | 86 | Counters.ProjectGraphEvaluations=1 | total 381.0 ms | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| cross-process-preparation | synthetic-cross-process-medium | 4-process | 86 | Counters.ProjectGraphEvaluations=1 | total 381.0 ms | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| cross-process-preparation | synthetic-cross-process-medium | 4-process | 86 | Counters.ProjectGraphEvaluations=1 | total 380.0 ms | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| cross-process-preparation | synthetic-cross-process-medium | 4-process | 86 | Counters.ProjectGraphEvaluations=1 | total 387.0 ms | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| cross-process-preparation | synthetic-cross-process-medium | 4-process | 86 | Counters.ProjectGraphEvaluations=1 | total 461.0 ms | 3f4d30020a731053f972f028eb5e465645c7ebd3f71b5cbbb6efc85769fac906 |
| exact-request-cache-eligibility | synthetic-cache-small | small-population | 21 | Counters.Cache.IneligibleUnitCount=0 | total 2270.0 ms | 0f0f13c903defc168a7b944bde950e61294c5eeffd4fbef4c4100f4fd0f9da46 |
| exact-request-cache-eligibility | synthetic-cache-small | small-repeat | 21 | Counters.Cache.Hits=1 | total 2168.0 ms | 0f0f13c903defc168a7b944bde950e61294c5eeffd4fbef4c4100f4fd0f9da46 |
| exact-request-cache-eligibility | synthetic-cache-medium | medium-population | 45 | Counters.Cache.IneligibleUnitCount=0 | total 2769.0 ms | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| exact-request-cache-eligibility | synthetic-cache-medium | medium-repeat | 45 | Counters.Cache.Hits=1 | total 2655.0 ms | e0cd89f9f290d44be32224c19ae518bfdccbfcacef1f39d9771b0d5bd5d7ae3e |
| exact-request-cache-eligibility | synthetic-cache-large | large-population | 69 | Counters.Cache.IneligibleUnitCount=0 | total 3376.0 ms | dc8beec847ebb8a2d099bacd821fd94aff9d81186304f6548f97fdc07ddc8dfd |
| exact-request-cache-eligibility | synthetic-cache-large | large-repeat | 69 | Counters.Cache.Hits=1 | total 3141.0 ms | dc8beec847ebb8a2d099bacd821fd94aff9d81186304f6548f97fdc07ddc8dfd |

## Routing and non-goals

- Cross-process preparation evidence belongs to #492/#493.
- Real-MSBuild cache eligibility belongs to #675.
- Public-API prepared reuse belongs to #498 and remains conditional on the #493 materiality gate.
- No private adopter identity, repository URL, namespace, proprietary topology, or raw private CI log is committed.
- No universal timing SLA is claimed.

OpenSpec: not applicable. This change adds an explicitly invoked internal measurement harness and evidence only; it changes no product behavior, public API, policy semantics, cache trust boundary, or documented user guarantee.
