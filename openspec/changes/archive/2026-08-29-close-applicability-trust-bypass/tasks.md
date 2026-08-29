## 1. Fail-closed completion transport

- [x] 1.1 Remove executor-supplied pre-derived completion transport and derive completion only from canonical expected and produced collections; verify the Core project builds without callers setting the removed member.
- [x] 1.2 Add a snapshot regression proving empty canonical collections cannot introduce a passing completion; verify the focused NUnit test passes.

## 2. Canonical unassessable provenance

- [x] 2.1 Validate every unassessable reason against its record's family, control, and policy provenance, replacing malformed evidence with canonical invalid-record-integrity evidence; verify evaluator tests pass.
- [x] 2.2 Add evaluator regressions for foreign family, control, and policy reason provenance, including suppression of the foreign reason; verify the focused NUnit tests pass.

## 3. Contract and integration validation

- [x] 3.1 Update reviewed public API snapshots for the removed transport property and verify Core public API approval tests pass.
- [x] 3.2 Run format, Core tests, repository lint, public API check, and OpenSpec validation; archive the completed change and verify `openspec validate --all` passes.
