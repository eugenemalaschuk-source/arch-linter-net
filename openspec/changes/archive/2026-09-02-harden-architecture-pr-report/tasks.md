## 1. Artifact context and authority integrity

- [x] 1.1 Add versioned execution-context persistence to Health report evidence and the change-report JSON/CLI producers; verify focused Health and change-report serialization tests cover mode, condition set, and identifier.
- [x] 1.2 Make the PR-report reader validate exact execution context, receipt mode, closed availability keys/tokens, and availability-to-payload conformance; verify mismatch, missing payload, and unknown-wire fixtures fail closed.
- [x] 1.3 Replace the resolved-finding nested scan with ordinal identity-set membership; verify the change-report comparison tests preserve resolved/new/existing classifications.

## 2. Reporting boundary and Markdown semantics

- [x] 2.1 Move PR-report composition and the report-owned change view into `Core.Reporting`, and replace production partial report reader/projector/renderer types with focused collaborators; verify `make architecture-coverage-ci` reports no new self-policy diagnostics.
- [x] 2.2 Select blockers from canonical strict/lifecycle/debt-gate receipts, preserving audit and unrelated diagnostics as non-blocking evidence; verify a mixed strict/audit fixture.
- [x] 2.3 Add context-specific inline-code and plain-text Markdown escaping and omit empty clean-report drill-downs; verify hostile input and exact clean-output fixtures.

## 3. Integration and lifecycle

- [x] 3.1 Update reviewed public API approval and command/help tests for the intentional contract boundary; verify focused Core and CLI test projects pass.
- [x] 3.2 Run formatting, OpenSpec strict validation, relevant architecture/public-API gates, and the risk-appropriate test suites; record exact successful commands.
- [x] 3.3 Synchronize implementation with the change artifacts, archive the OpenSpec change, validate all specs, push the corrective commit, and update/resolve the verified PR review threads without merging.
