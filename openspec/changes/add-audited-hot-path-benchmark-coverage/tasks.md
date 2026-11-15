# Tasks

## 1. Extend the canonical workload catalog

- [ ] 1.1 Add optional independent ignore, metric-rule, report-rule, and audit workload dimensions to the #502 manifest/generator/schema/catalog; verify default workloads keep their identities and the new dimensions are materialized with checked counts using focused `LargeSolutionBenchmarking` tests.
- [ ] 1.2 Add buildable contract-layer graph fixtures for dense DAG, layered DAG, and SCC-with-acyclic-tail cases; verify generated policies execute cycle and acyclic-sibling contracts, assert empty/non-empty cycle outputs and canonical results, and retain the project-cycle fail-closed test.
- [ ] 1.3 Add shared synthetic History input families and three-size scale cases for sparse/dense line edits, task-to-commit occurrences, co-change components, and ancestry queries; verify deterministic inputs, independent dimensions, and output checksums in ordinary tests, and document the catalog selection rules in the #502 README.

## 2. Extend benchmark evidence and bounded observations

- [ ] 2.1 Add optional, schema-bounded work counters and source/input/runner/timer provenance to `benchmark-evidence/v1`; reuse existing profile counters where applicable and internal test seams elsewhere, then verify schema validation and backward-compatible serialization without changing `analysis-profile/v1`.
- [ ] 2.2 Add repeatable profiler-overhead and analyzer/process/setup boundary observations, plus canonical result and resource availability checks; verify profiled/unprofiled samples share inputs/results and all unavailable values retain explicit reasons.

## 3. Exercise the audited execution paths

- [ ] 3.1 Add a separate explicit benchmark harness for executable cycle/acyclic-sibling contracts, parser and method-body preparation, CEL context and AST lookup work, metric/report/ignore scales, and the shared History workloads; verify deterministic work counters and actual checker invocation in focused tests while keeping hardware-sensitive matrices outside normal `make test`.
- [ ] 3.2 Run the focused repeated sample matrix at three useful sizes per adopted family and retain issue-scoped `benchmark-evidence/v1` results with exact source/runtime/runner/input identities, timer boundaries, profiler overhead, allocations/peak-memory availability, and canonical checksums; verify the evidence validates against the existing #502 schema.

## 4. Correct and document the historical evidence boundary

- [ ] 4.1 Add a dated #655 graph-coverage correction and a reusable per-task baseline/effect/disposition record with exact commands; verify earlier #655 measurements/dispositions remain byte-for-byte unchanged and mark unmeasured values explicitly.
- [ ] 4.2 Run focused Core benchmark/evidence tests, formatter and relevant documentation/spec checks; inspect the final diff for unrelated files and confirm the `analysis-profile/v1` schema and public API snapshots are unchanged.

## Workflow follow-up

- Archive this OpenSpec change after implementation and evidence synchronization, then run `openspec validate --all` before opening the pull request.
