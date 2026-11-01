## 1. Add opt-in recorder attribution

- [x] 1.1 Capture per-invocation cycle recorder workload, split elapsed time, allocations, and deterministic SCC/evidence work only when the explicit analysis-profile timing observer is present; verify ordinary and timing-only calls create no observations or work-counter allocations.
- [x] 1.2 Add the optional `Measurements.CycleRecorder` profile model and populate it from the current validation timing session; verify enabled, disabled, empty, nonempty, strict, and audit invocation denominators and the complete `(V,E,B,U,K)` tuples.
- [x] 1.3 Add focused semantic/profile tests proving zero index work for disabled/empty calls, exact candidate/digest preservation, no topology or diagnostic identity fields, and profile/unprofiled output parity; run the affected Core and Testing profile test projects.

## 2. Publish and document the opt-in schema

- [x] 2.1 Add the extended strict profile schema to the 0.10.0 compatibility manifest and package resources while keeping the historical 0.5.1 schema unchanged; verify the generated profile validates through `PackagedSchemaRegistry` and the current schema checksum/resource identity is correct.
- [x] 2.2 Update the analysis-profile phase/counter dictionary and reviewed Core public API snapshot for the additive profile model; verify `make public-api-check` and the relevant profile schema/documentation tests.
- [ ] 2.3 Complete the OpenSpec delta synchronization and run `openspec validate --all`; verify the archived main spec contains `## Purpose` and `## Requirements` with no delta header.

## 3. Measure and make the evidence-gated disposition

- [x] 3.1 Extend the existing #1125/#502 paired evidence path without a new harness; verify legacy/indexed direct-recorder candidates have identical full ordered output and digests and retain joint workload/work counters.
- [x] 3.2 Run repeated profiled/unprofiled ArchLinterNet self-analysis samples on identical inputs; retain strict/audit profiles, recorder invocation denominators, full-analysis timing, allocations, existing process peak-memory evidence, and profiler-overhead measurements with exact source/runtime/input identities.
- [ ] 3.3 Publish a public-safe internal report linked from #1147/#1122/#1144, labeling self-analysis as internal dogfood and synthetic stress data separately; record a current materiality/success/kill criterion and explicitly choose retain-indexed, authorize a separate fallback change, or insufficient evidence.
- [x] 3.4 If the authorized evidence-gated decision demonstrates a simple fallback's material end-to-end benefit, implement it only in a separate focused PR and rerun the same comparison including high-evidence cases; otherwise verify that no adaptive production path was added and record the concrete revisit trigger or missing evidence.
