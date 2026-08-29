## 1. Policy requirement contract

- [x] 1.1 Add the `external_evidence` policy model, import-composition support, raw shape checks,
  and semantic validation for unique SARIF requirements; verify focused policy-loader NUnit tests
  cover valid, duplicate, unsupported, and incomplete declarations.
- [x] 1.2 Add focused policy fixtures/tests that demonstrate the effective typed requirement is
  available to a caller without changing policies that omit `external_evidence`.

## 2. Bounded SARIF trust reader

- [x] 2.1 Add immutable evidence context, limits, provenance, and closed trust-result models,
  including stable reason/status mappings usable by the applicability boundary; verify model
  construction and canonical ordering tests pass.
- [x] 2.2 Extend the file-system seam for bounded stream reads and implement repository containment,
  byte-limit/hash, SARIF 2.1 shape, matching-run, invocation-success, and result-count validation;
  verify focused reader NUnit tests pass.
- [x] 2.3 Validate repository/revision/scope/logical-key context merging and mismatch behavior,
  including valid zero results and optional absence; verify focused reader NUnit tests pass.

## 3. Documentation and reviewed contracts

- [x] 3.1 Document the vendor-neutral `external_evidence` declaration and explicit producer/CI
  context contract, including non-goals and fail-closed examples; verify documentation navigation
  and Markdown linting are valid.
- [x] 3.2 Update the reviewed Core public-API snapshot only through the explicit lifecycle and
  verify `make public-api-check` passes.

## 4. Integration validation and synchronization

- [x] 4.1 Run focused Core test families, formatter, affected policy/schema checks, and OpenSpec
  validation; fix issue-related failures and record exact results.
- [x] 4.2 Synchronize the implementation with the proposal/specs, mark every completed task, and
  archive the OpenSpec change; verify `openspec validate --all` passes before the pull request.
