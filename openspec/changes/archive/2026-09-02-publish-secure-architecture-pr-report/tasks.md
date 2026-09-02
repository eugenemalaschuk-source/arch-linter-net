## 1. Read-only report production

- [x] 1.1 Replace the write-capable legacy coverage-comment path with a read-only architecture
  report producer that retains strict/audit/coverage artifacts and creates the compatible Health,
  change, and CLI-rendered report inputs.
- [x] 1.2 Emit the fixed report artifact and manifest with bounded, deterministic repository/PR/
  head/run/attempt, schema/kind/marker, path, size, and SHA-256 metadata while preserving
  fail-closed producer results.

## 2. Privileged publication

- [x] 2.1 Add the no-checkout completed-CI publisher with the sole pull-request write permission,
  fixed artifact-ID discovery, current-head/run validation, bounded inert file handling, and
  manifest/hash verification.
- [x] 2.2 Implement one-comment update/create, legacy coverage-marker migration, stale-run
  protection, and bounded non-semantic integration-failure handling.

## 3. Regression evidence and documentation

- [x] 3.1 Add focused workflow-contract tests covering producer/publisher permissions,
  canonical CLI ownership, artifact validation, stale/malformed handling, fork safety, and
  single-writer migration invariants.
- [x] 3.2 Update CI integration and output documentation to describe the unified report,
  artifact trust boundary, retained standalone coverage, and safe-degradation behavior.

## 4. Validation and specification completion

- [x] 4.1 Run focused workflow tests, workflow formatting/lint checks, OpenSpec validation, and
  the risk-appropriate repository validation; fix issue-related failures.
- [x] 4.2 Synchronize the actual behavior into main OpenSpec specs and archive the completed
  change before opening the pull request.
