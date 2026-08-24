## 1. Preparation sequencing

- [x] 1.1 Route uncached `EnsureBuilt` validation through the existing metadata-only preparation and post-build materialization path.
- [x] 1.2 Preserve ordinary uncached validation behavior and the existing build-state receipt, strict/audit, and `--no-restore` semantics.
- [x] 1.3 Preserve the metadata-selected output identity while refreshing an unconstrained post-build receipt.

## 2. Regression coverage

- [x] 2.1 Add focused Windows-relevant CLI integration coverage for an `ArchLinterNet.Testing` target graph rebuilt by `--ensure-built`.
- [x] 2.2 Add isolated installed-package smoke coverage for the same self-analysis build-state sequence.
- [x] 2.3 Cover a policy-selected Debug output when an alternate Release output is newer.

## 3. Verification and specification lifecycle

- [x] 3.1 Run focused build-state/preflight and CLI integration tests, formatter, architecture lint, and OpenSpec validation.
- [x] 3.2 Synchronize implementation and specs, archive the completed OpenSpec change, and inspect the archived artifacts before opening the pull request.
