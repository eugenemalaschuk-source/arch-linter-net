## 1. Trusted no-App bootstrap handoff

- [x] 1.1 Remove GitHub App writer inputs, token minting, git push, and PR API use from the reusable bootstrap workflow.
- [x] 1.2 Materialize a closed private handoff payload and canonical manifest after trusted setup succeeds, then upload it through the pinned artifact action.
- [x] 1.3 Add focused workflow regression tests for no writer credentials, no remote write operations, and bounded private artifact output.

## 2. Verified owner apply path

- [x] 2.1 Implement strict handoff manifest parsing and validation for schema, identity, pins, base commit/tree, paths, UTF-8 bytes, lengths, and digests.
- [x] 2.2 Add the packaged `apply-handoff` CLI command with atomic managed-file application and actionable fail-closed diagnostics.
- [x] 2.3 Add focused NUnit coverage for valid apply and stale/tampered/wrong-identity/unsafe-path rejection with no partial writes.

## 3. Documentation and integration validation

- [x] 3.1 Update setup/adoption documentation and generated bootstrap guidance for the owner-operated normal PR handoff.
- [x] 3.2 Run focused CLI/workflow tests, formatter, workflow lint, documentation contract, and OpenSpec validation; package/transport evidence remains candidate-bound after immutable pin rotation.
- [x] 3.3 Synchronize and archive the OpenSpec change, inspect the final diff, push the issue branch, and open the focused #963 PR.
