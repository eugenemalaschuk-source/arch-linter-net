## 1. Core temporal receipt and CLI boundary

- [x] 1.1 Add the internal Core temporal-publication receipt model, schema constants, strict serialized Health report-evidence reader, and binding validation; verify malformed, incomplete, and identity-mismatch inputs produce deterministic unassessable reasons.
- [x] 1.2 Implement date-aware temporal revalidation using the canonical waiver lifecycle semantics and finite-horizon rules without evaluating policy, assemblies, findings, Gate, Health, or payload; verify cross-midnight, expired, stale, invalid, and required-external scenarios with Core tests.
- [x] 1.3 Add `health revalidate-publication` with explicit UTC-date and identity options, stable JSON output, output-file support, and fail-closed exit behavior; verify CLI tests prove no policy/build input is required and the input artifact is unchanged.

## 2. Trusted promotion and transport integration

- [x] 2.1 Extend provider evidence identity and bounded artifact reading to retain the exact Health member digest, badge payload digest, producer identity digest, and merged-tree binding; verify resolver tests reject missing or mismatched evidence.
- [x] 2.2 Invoke the trusted Core/CLI revalidator only after the stored horizon elapses, validate its receipt, and feed only its finite horizon into the existing decision path; verify raw promotion tests cover the #978 cross-midnight reproducer and genuine unavailable cases.
- [x] 2.3 Preserve the bound temporal receipt in the publication metadata and exercise the same refreshed context through Relay prepare/publish/renew; verify Relay contract tests cover ready, stale/invalid, expiry, and mismatch outcomes without a second evaluator.
- [x] 2.4 Wire the pinned promotion action and reusable workflow to the reviewed action-root .NET CLI command without consumer checkout or new credentials; verify workflow/release contract tests cover the command path, fail-closed tooling absence, and immutable pin boundaries.

## 3. Integration, specification sync, and delivery

- [x] 3.1 Run targeted C#/Python/workflow tests, correct diagnostics, and verify the OpenSpec delta remains aligned with the implementation; repository-wide formatting and acceptance remain deferred to CI.
- [ ] 3.2 Run repository validation appropriate to the cross-cutting change (`make restore`, `make acceptance`, `openspec validate --all`, public API check, and architecture/policy gates); record any environment-only limitation explicitly.
- [x] 3.3 Archive the completed OpenSpec change, re-run `openspec validate --all`, review the final diff, then commit, push, open PR #980, and start bounded CI/review monitoring; the full public API snapshot/repository acceptance gate remains deferred to PR CI.
