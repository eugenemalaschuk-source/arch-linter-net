## 1. Candidate preparation and identity

- [x] 1.1 Add the explicit already-prepared-build mode to the canonical architecture Make target and verify default/local semantics remain unchanged with the target tests.
- [x] 1.2 Add candidate identity creation/verification for source SHA, tree SHA, policy digest, and CLI/Testing assembly hashes, with focused Python tests for valid, missing, and mismatched identities.
- [x] 1.3 Replace the producer's duplicate CLI/Testing build with one solution build, manifest creation, and fail-closed candidate verification; verify the workflow contract test.

## 2. Fan-out and evidence production

- [x] 2.1 Run strict, public-API, coverage, and Health/current/report projections concurrently with isolated logs/statuses and preserve strict-result aggregation; verify with workflow contract tests.
- [x] 2.2 Switch current report evidence to `health --change-snapshot`, keep the base snapshot separate, and ensure render/manifest steps consume existing artifacts only; verify report-producer assertions.
- [x] 2.3 Emit candidate-bound DAG/timing evidence and upload it with the producer artifacts, including the 204 s baseline and <=60 s PASS/GAP comparison fields; verify its schema/content in tooling tests.

## 3. Validation and lifecycle closure

- [x] 3.1 Update OpenSpec and workflow tests for the new candidate/fan-out/render-only contract and run focused Python tests.
- [x] 3.2 Run the required formatter/lints, focused tooling tests, relevant architecture checks, and OpenSpec validation; inspect the final diff for unrelated artifacts.
- [x] 3.3 After comparable hosted runs are available, record at least three successful standard-hosted samples, before/after median/range, overlap, command/process counts, and PASS/GAP attribution in the internal evidence record.
