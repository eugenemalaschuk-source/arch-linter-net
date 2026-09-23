## 1. Evidence contract

- [x] 1.1 Add typed Phase 1 evidence records for real-MSBuild/cache-control observations, eligibility reasons, cache counters, avoided-work fields, resource/timing availability, reference/base disposition, gate metadata, and A/B/C outcome; verify model validation rejects missing scale points, non-canonical reasons, incomplete effect estimates, and unauthorized outcome A claims.
- [x] 1.2 Implement deterministic effect-model calculations for targeted-phase share, Amdahl upper bound, cold/miss overhead, warm-hit avoided work, amortized reuse/break-even, S/M/L expected end-to-end effect, resource trade-offs, success threshold, and kill criterion; verify formulas with focused NUnit tests.

## 2. Measurement harness

- [x] 2.1 Add an explicit #675 benchmark harness that reuses #502 workload generation/materialization and records disabled, real-MSBuild cache-enabled population/miss/repeat, and separately labelled eligibility-control runs at small, medium, and large sizes; verify the harness preserves canonical result identity and never labels an ineligible real-MSBuild run as a hit.
- [x] 2.2 Capture and normalize typed `CacheIneligible` reasons, lookup/hit/miss/reject/write/byte/avoided-work counters, phase timings, deterministic work counters, and available allocation/working-set/I/O observations from each run; verify changed project/source/package/configuration/artifact cases reject stale reuse where the existing cache contract exposes the scenario.
- [x] 2.3 Assess repeated immutable reference/base-side exact-request snapshots independently from candidate prepared-state reuse, record the current cache-boundary disposition as routed to the owning lane, and verify the report excludes #492/#493 prepared-state savings from the exact-cache estimate.
- [x] 2.4 Gate the recorded A/B/C outcome on complete evidence, canonical equivalence, realistic reuse assumptions, the materiality threshold, and the open #991 normalization prerequisite; verify the harness fails closed for a claimed outcome A that lacks post-normalization authority or required evidence.

## 3. Checked-in evidence

- [x] 3.1 Render a synthetic/anonymized machine-readable evidence artifact and human-readable internal report containing the S/M/L matrix, pre-implementation estimate, actual-vs-expected comparison, A/B/C decision, reference/base disposition, and Phase 2 routing; verify no private adopter identity, topology, URL, namespace, or raw CI log is emitted.
- [x] 3.2 Add focused contract tests for report serialization/rendering and the explicit non-overlap/routing statements; verify the tests pass without running the hardware-sensitive explicit matrix.

## 4. Validation and closure

- [x] 4.1 Run the explicit benchmark matrix on the prepared CLI/build and refresh the checked-in evidence only after its assertions pass; verify the generated result is reproducible in schema/identity and records the actual environment and incomplete measurements explicitly.
- [x] 4.2 Run focused Core tests, formatter/lint checks, relevant OpenSpec validation, inspect the diff for unrelated artifacts, and archive `measure-real-msbuild-cache-eligibility`; verify `openspec validate --all` passes after archive and the branch contains no production eligibility change.
