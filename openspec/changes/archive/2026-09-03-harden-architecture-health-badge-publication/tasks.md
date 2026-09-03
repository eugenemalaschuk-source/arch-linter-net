## 1. Reviewed unavailable receipt

- [x] 1.1 Commit the deterministic unavailable Shields payload at a fixed architecture path.
- [x] 1.2 Make unprivileged PR CI generate the CLI fallback and byte-compare it with the committed receipt.

## 2. Trusted publication boundary

- [x] 2.1 Resolve the Architecture Coverage requirement from effective `main` branch rules.
- [x] 2.2 Read the reviewed unavailable receipt through the GitHub API without checkout, restore, build, or CLI execution.
- [x] 2.3 Check the event SHA against current `main` at the write boundary and skip stale runs.
- [x] 2.4 Publish endpoint and metadata through one tree/commit and a non-force ref update.
- [x] 2.5 Cover fallback, stale event, effective-rule, atomic publication, and failure-before-ref behavior with workflow fixtures.

## 3. Canonical evidence parsing

- [x] 3.1 Require report-evidence schema version, kind, matching gate/health, and complete inventory-bearing receipts.
- [x] 3.2 Use a production-shaped Core serializer result for positive badge inputs and test malformed envelope regressions.

## 4. Validation

- [x] 4.1 Run CLI unit tests and release/workflow fixture tests.
- [x] 4.2 Run coverage and repository lint validation.
