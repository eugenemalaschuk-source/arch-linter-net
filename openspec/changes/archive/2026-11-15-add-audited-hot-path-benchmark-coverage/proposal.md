# Proposal

## Why

The existing #502 foundation has reusable workload and evidence contracts, but the #655 graph matrix does not execute the cycle or acyclic-sibling checkers and its source/fact/CEL measurements omit preparation work outside their narrower timers. The audit-driven implementation tasks need reproducible, attributable baselines now, while historical measurements and dispositions must remain intact.

## What Changes

- Extend the canonical synthetic workload catalog and materializer with executable layer-graph workloads for empty-cycle DAGs and cyclic output cases, including layered DAGs and SCCs with acyclic tails, plus independent ignore, metric-rule, and report-rule scales.
- Add bounded deterministic work observations and explicit timer boundaries for graph visits, membership checks, discovery/source parsing, method-body preparation, CEL context construction, AST semantic lookups, and History workloads without changing the `analysis-profile/v1` schema or adding a public profiler.
- Retain repeated baseline samples, exact runtime/source/input identities, profiler-overhead and resource availability, and canonical-result checksums in the existing benchmark evidence flow.
- Add a dated correction to the historical #655 graph-coverage interpretation and a reusable per-task baseline/effect/disposition record and command recipe for the #1122 child tasks.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `large-solution-benchmarking`: specify executable contract coverage, independent audit dimensions, bounded work attribution, and per-task baseline/effect evidence while preserving the existing versioned profile and result contracts.

## Impact

The change affects the Core test project's existing `Benchmarking` and `AnalysisProfile` harnesses, benchmark JSON schemas/catalog and internal performance evidence under `docs/internal/large-solution-benchmarking/`, plus the `large-solution-benchmarking` OpenSpec capability. It does not change production analysis semantics, public APIs, cache eligibility, release scope, or the `analysis-profile/v1` schema.
