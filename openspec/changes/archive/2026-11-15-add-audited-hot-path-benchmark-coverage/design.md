# Design

## Context

See `proposal.md` for the gap this change closes. The #502 catalog already owns project/source/policy workloads, an evidence envelope, and explicit staged versus real-MSBuild boundaries. The current #655 harness is issue-specific and measures project topology, selectors, and broad CLI profiles; its graph rows do not invoke cycle or acyclic-sibling checks. The `analysis-profile/v1` contract is stable and is explicitly not extended by this work.

## Goals / Non-Goals

**Goals:**

- Reuse the #502 workload generator, fixture materializer, evidence model, and explicit-run convention.
- Make audit-specific dimensions and graph shapes deterministic, independently scalable, and visible in manifests and generated policy/source inputs.
- Capture actual checker execution, bounded work counts, timer boundaries, repeated environment-labelled samples, and canonical results without changing analysis semantics.
- Give downstream algorithm tasks a shared History workload and per-task evidence recipe.
- Preserve #655 historical measurements and its original disposition while correcting the scope of its graph-coverage claim.

**Non-Goals:**

- Optimizing production algorithms in this issue.
- Changing public APIs, YAML/CLI semantics, cache eligibility, `analysis-profile/v1`, or release scope.
- Adding an always-on runtime profiler, a second product profiling command, timing gates, or a new benchmark framework.
- Rewriting historical benchmark samples as if they were captured on the current implementation base.

## Decisions

### Keep project topology separate from contract graph topology

Keep `CyclicScc` as a structural-only project-reference workload because cyclic MSBuild project references cannot be compiled. Add a separate optional contract-layer graph shape to the existing manifest. Materialize its type references inside buildable synthetic assemblies, so dense and layered DAGs plus SCCs with acyclic tails execute the real cycle and acyclic-sibling contract families without a project-reference cycle. Bound SCC size by declared dimensions and retain required output cardinality in the workload identity/evidence.

### Extend v1 manifests additively

Add optional, zero-default ignore, metric-rule, and report-rule dimensions and inventory counts, plus optional contract-layer graph details. Omit unset optional fields during canonical serialization so existing #502 workload identities and committed evidence remain unchanged. Keep the manifest/evidence schema IDs at v1 because the new fields are additive and optional; reject overflow using the existing checked-counter convention.

### Put new counters in benchmark evidence and internal test seams

Retain the exact raw `analysis-profile/v1` payload. Reuse its existing project, source-scan, CEL-selector, cycle-recorder, and allocation observations where they already measure the requested boundary. For uncovered operation counts, use fixed low-cardinality benchmark counters or internal test seams passed explicitly by the benchmark tests; do not add profile fields, global mutable observers, file/type labels, or a public profiler surface. Keep the counter vocabulary bounded by the evidence schema.

### Separate setup, process, analyzer, and observer measurements

Record fixture restore/build separately from analysis. Keep process wall-clock (CLI process start through exit) separate from `analysis-profile/v1` phase time, and document which phases contain setup, compilation/build-context, semantic evaluation, and report publication. Use paired profiled/unprofiled samples to describe observer overhead where the observer changes measured work. Preserve raw per-sample values, source and materialized-input identities, runtime/OS/architecture/configuration, a privacy-safe runner label, resource availability, and canonical-result checksum.

### Keep ordinary tests deterministic and timing matrices explicit

Normal NUnit tests validate generated policy/source topology, actual checker invocation where feasible, deterministic counters, schema validity, and canonical output identity. Hardware-sensitive repeated timing and memory matrices remain `[Explicit]`; they write only issue-scoped evidence paths and never replace the historical #655 artifact. For every downstream task, the shared evidence recipe captures baseline, expected local/end-to-end effect bound, memory trade-off, success/kill criteria, post-change result, and explicit `not measured` values.

### Reuse History workload inputs without taking ownership of sibling algorithms

Add synthetic workload constructors for sparse/dense line edits, task-to-commit occurrences, co-change components, and ancestry-query shapes to the same catalog. Keep each algorithm's focused regression fixture and exact algorithm counter owned by its child issue; the shared foundation supplies scalable reproducible inputs, evidence fields, and commands.

## Risks / Trade-offs

- **Observer work changes the measured path** → keep instrumentation opt-in and internal, use fixed counters, and report paired observer-enabled/disabled samples and overhead.
- **Generated graph inputs could accidentally rely on an unbuildable project cycle** → keep contract-layer edges separate from project references and assert fixture compilation plus the declared contract output.
- **Cycle output can grow exponentially** → constrain generated SCC sizes, scale tails/edges independently, and include output cardinality in deterministic work and canonical identity.
- **Optional schema fields can invalidate old workload identities** → omit zero/default fields from canonical serialization and retain regression assertions for existing manifests.
- **Local memory counters vary by platform** → preserve unavailable values with reasons and keep timing/memory out of deterministic correctness gates.

## Migration Plan

No product migration is required. New optional manifest/evidence properties remain backward-compatible with existing v1 artifacts. The issue-scoped dated note is additive; historical #655 JSON/Markdown measurements and disposition remain untouched.
