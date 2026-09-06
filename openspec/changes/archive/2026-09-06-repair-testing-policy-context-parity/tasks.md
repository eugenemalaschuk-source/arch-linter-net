## 1. Shared context import boundary

- [x] 1.1 Extract the existing policy-context JSON deserialization and completeness validation into a focused Core-owned reader, preserving current schema/kind/evidence failures; verify existing Core policy-weakening tests still pass.
- [x] 1.2 Route the public policy-weakening formatter's context import through the shared reader and verify CLI and Testing callers compile against the same path.

## 2. Testing parity regression

- [x] 2.1 Add a Testing adapter regression that formats valid current-schema contexts as CLI-shaped JSON, supplies them through `WithPolicyWeakeningContexts`, and verifies `EvaluateDebtGate` returns an outcome.
- [x] 2.2 Cover strict-to-audit weakening, identical-context no-op, and malformed-context fail-closed behavior; verify the focused NUnit tests pass.

## 3. Documentation and specification synchronization

- [x] 3.1 Update the Testing usage documentation to state the CLI/Testing context-artifact parity and regeneration compatibility rule; verify the documentation diff is scoped to #788.
- [x] 3.2 Compare implementation and tests with the OpenSpec delta, update the synchronized specification, and run `openspec validate --all --strict`; the new `test-adapter` spec passes and the repository retains 33 unrelated pre-existing spec failures.

## 4. Risk-based validation and delivery

- [x] 4.1 Run focused Core and Testing suites, `make fmt`, and the directly implicated architecture/public-API/lint checks; all issue-scoped checks pass, with the repository's existing lint-code-size warnings and documentation notices retained.
- [x] 4.2 Inspect the final diff, commit the issue-specific branch, push it, and open exactly one PR targeting `main` with `Closes #788` and the required validation/risk sections; verified PR #789 on `fix/788-policy-context-testing`.
