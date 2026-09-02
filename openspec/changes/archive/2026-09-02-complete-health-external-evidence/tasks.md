## 1. Health external-evidence producer chain

- [x] 1.1 Expose the canonical external-evidence bindings and producer context on `health`, reuse the binding authority before Health projection, and verify Health JSON contains one trust receipt per declared requirement.
- [x] 1.2 Add CLI producer-chain tests for matching zero-result SARIF and wrong-revision SARIF, and verify `report pr` projects complete and unassessable evidence respectively without a second evaluator.

## 2. Report safety and approvals

- [x] 2.1 Extend plain-text Markdown escaping to neutralize GitHub autolinks and mentions, and verify hostile email, mention, issue, and repository-reference inputs remain inert.
- [x] 2.2 Regenerate and review the detailed Core public-API approval fixture, and verify `PublicApiSurface_MatchesApprovedBaseline` passes.

## 3. Documentation and validation

- [x] 3.1 Align CLI and output-format documentation with the legacy Health report-availability contract, and verify documentation lint passes.
- [x] 3.2 Run focused Core and CLI tests, public API checks, architecture lint, OpenSpec validation, and the relevant coverage shard command; local `test-coverage-core-2` and `test-unit-core-2` stalled before test output and were stopped, while their focused direct test selections passed.
