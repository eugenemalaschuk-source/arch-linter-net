# Tasks

## 1. Make audit and report dimensions truthful

- [x] 1.1 Add measured metric results to benchmark samples and the evidence schema; call `Measure()` for metric workloads, and verify exact count, complete/evaluable values, stable digest, and schema round-trip with focused Core tests.
- [x] 1.2 Separate ordinary and method-body contract materialization and hold SARIF at 32 findings across 1/8/32 rules; verify generated policy counts and every referenced rule in focused Core tests, then update the benchmarking docs.

## 2. Repair History ancestry inputs

- [x] 2.1 Generate a diverging/rejoining DAG, side-branch ancestry queries, and continuous rename events; verify parent structure, query answers, and rename continuity from input records in History catalog tests, then update the workload catalog documentation.

## 3. Verify reproducible materialized identity

- [x] 3.1 Independently materialize and build the same workload in two temporary roots; verify identical canonical input hashes and document which source/configuration and staged inputs participate in that identity.

## 4. Refresh and review retained evidence

- [x] 4.1 Regenerate affected 33-workload × 8-sample evidence at the implementation commit; verify metric result counts, ordinary/method-body counts, fixed SARIF volume, stable input identities, and evidence schema validation.
- [ ] 4.2 Inspect current Qodana trusted-gate and Sonar new-issue results; fix actionable regressions or add a precise evidence-based disposition, and verify the resulting PR checks or documented limitation.

## Workflow follow-up

- Synchronize the existing `large-solution-benchmarking` main spec by archiving this completed change.
- Run `openspec validate --all` and inspect the archived spec and evidence changes.
- Update and push the existing issue #1123 pull request; do not create a second PR or merge it.
