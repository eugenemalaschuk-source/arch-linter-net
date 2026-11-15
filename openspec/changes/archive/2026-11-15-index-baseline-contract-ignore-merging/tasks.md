# Tasks

## 1. Establish the pre-change effect contract

- [x] 1.1 Add a focused explicit Core merge benchmark and test-only linear reference that vary contract count, baseline-entry count, and ignore count independently, and verify identical ordered outputs and canonical digests.
- [x] 1.2 Before changing production merge logic, run the benchmark at the current base revision and record deterministic legacy scan counts, direct merge timing, allocations, retained-memory observations, environment, revision, scale dimensions, expected effect, success/kill criteria, and any unavailable phase-share or memory values in the issue-specific evidence file.

## 2. Index baseline merging and preserve semantics

- [x] 2.1 Build one first-match, case-insensitive contract lookup per non-empty group and lazy per-contract membership indexes for v1 display pairs or the exact v2/v3 structured merge key; verify the existing focused merger tests pass.
- [x] 2.2 Add an opt-in internal work counter and generated differential/scale tests for v1/v2/v3, independently scaled C/B/I, inline ordering, duplicate contract IDs, case sensitivity, first reason/provenance, ignored identity fields, and existing filtering/diagnostics; verify linear membership counts and the Core baseline validation test family.

## 3. Measure the indexed result and validate integration

- [x] 3.1 Rerun the same explicit workload and environment, update the evidence report with paired timing/allocation/retention observations and canonical output digests, and verify the measured work model and issue-specific success/kill criteria.
- [x] 3.2 Run `make fmt`, focused baseline tests, the Core test project, `make public-api-check`, relevant architecture/spec checks, and inspect the final diff for unrelated changes; record each exact command and result in the PR evidence.

## Workflow follow-up

- Archive this change after implementation and tests with `openspec archive index-baseline-contract-ignore-merging`.
- Run `openspec validate --all` after archive and inspect the synchronized `baseline-generation` spec and archive files before opening the PR.
