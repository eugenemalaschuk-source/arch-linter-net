## 1. Prepared-input provenance

- [x] 1.1 Retain metadata preparation immediately after each successful preparation call and use
  it as the fallback for cancellation/error provenance and collision inputs.
- [x] 1.2 Add focused cancellation and evaluation-error tests that verify prepared projects,
  artifacts, receipts, and counters are preserved before materialization.

## 2. Effective build-output context

- [x] 2.1 Merge CLI overrides with policy output defaults and thread the effective context through
  graph build, output resolution, manifests, receipts, verification, and cache identity.
- [x] 2.2 Cover Platform-constrained prepared-path selection and policy-selected Release rebuild
  with receipt/digest assertions.

## 3. Required Windows packed-artifact execution

- [x] 3.1 Add a method-specific Make target for the installed-tool replacement oracle.
- [x] 3.2 Add the Windows packed-artifact matrix entry and preserve its shard evidence/fan-in
  requirements with workflow routing tests where applicable.

## 4. Verification and lifecycle

- [x] 4.1 Run focused tests, formatting, architecture lint, code-size lint, and OpenSpec
  validation.
- [x] 4.2 Archive the completed change, push the review fix, and update PR #648 without merging.
