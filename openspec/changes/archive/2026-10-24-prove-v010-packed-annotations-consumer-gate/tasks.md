## 1. Candidate package inventory

- [x] 1.1 Extend Checkpoint B manifest loading and package provenance checks for v3 stable 0.10 candidates, including the package-only annotations subject, while preserving v2 behavior.
- [x] 1.2 Add focused manifest and evidence tests for v2 compatibility, v3 package inventory, optional symbols, and exact candidate digest binding.

## 2. Packed annotation consumer proof

- [x] 2.1 Add an external synthetic consumer matrix that restores the candidate annotations, CLI, Core, and Testing packages from the immutable feed and covers the required project shapes.
- [x] 2.2 Verify package/runtime/public API boundaries, canonical and custom mapping parity, selectors and contextual behavior, role conflicts, metadata, reviewed API membership, policy authority, compatibility diagnostics, and CLI/Testing/output parity.
- [x] 2.3 Emit the annotation scenario through the existing Checkpoint B package-and-entrypoints evidence path.

## 3. Release evidence contract

- [x] 3.1 Make platform merge and final aggregation require annotation evidence when the v3 candidate inventory includes `ArchLinterNet.Annotations`, while preserving the existing scenario set for v2 candidates.
- [x] 3.2 Update release-evidence aggregation tests and the Checkpoint B spec; archive the OpenSpec change and validate all specs.

## 4. Validation

- [x] 4.1 Run focused release-manifest/evidence tests and the packed annotation scenario against a freshly packed stable 0.10 candidate.
- [x] 4.2 Run repository acceptance, package validation, documentation validation, and strict OpenSpec validation; inspect the final diff and record exact PASS/FAIL evidence.
