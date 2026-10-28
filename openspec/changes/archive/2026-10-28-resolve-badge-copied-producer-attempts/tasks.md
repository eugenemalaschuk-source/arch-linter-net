# Tasks

## 1. Resolve producer execution lineage

- [x] 1.1 Extract the badge producer resolver into a focused internal helper and resolve the exact selected job against bounded attempt history; verify first-run, copied-job, multiple-consecutive-rerun, genuine-rerun, and malformed-lineage cases with producer-resolution tests.
- [x] 1.2 Add a resolver-to-artifact regression where the selected check points to a copied job but the original attempt-1 manifest is accepted using the original job ID and attempt; verify wrong attempt/job provenance remains rejected.

## 2. Verify integration boundaries

- [x] 2.1 Run the focused badge-promotion tests and confirm read-only preflight remains publication-free.
- [x] 2.2 Verify #1114's acquisition helper still serves all six release jobs with separate frozen-transport verification by running its focused acquisition/workflow tests and applicable workflow lint.

## Workflow follow-up

- Synchronize the archived `architecture-health-badge-promotion` spec with the implemented and tested behavior, archive this change before opening the PR, and run `openspec validate --all`.
