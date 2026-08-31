## 1. Canonical native bindings

- [x] 1.1 Remove namespace-text fallback binding from metric external facts and verify a same-namespace/different-owner fixture attributes an external group only to its canonical owner.
- [x] 1.2 Preserve all simple-name assembly subject candidates, fail closed on non-unique assembly endpoints, and verify a focused topology/metric test reports `ambiguous_subject` without a value.

## 2. Unassessable report evidence

- [x] 2.1 Serialize unassessable contributor count, list, and truncation as null and verify the exact JSON contract in formatter and CLI integration tests.

## 3. Approval and regression validation

- [x] 3.1 Regenerate the complete Core public API approval snapshot and verify `make public-api-check` passes on the approved baseline.
- [x] 3.2 Run focused and full Core/CLI suites, lint, policy/docs, strict OpenSpec validation, and the project architecture gate; fix issue-related failures.
