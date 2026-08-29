## 1. Authored waiver and fingerprint correctness

- [x] 1.1 Deduplicate validation and lifecycle records by authored declaration, aggregating source-set-expanded alias matches.
- [x] 1.2 Require lowercase canonical SHA-256 fingerprints in schema and validation, with regression coverage.

## 2. Invalid lifecycle and diagnostics

- [x] 2.1 Produce deterministic fail-closed `invalid` canonical lifecycle evidence for malformed manual waivers.
- [x] 2.2 Render target fingerprint and remediation reason in human waiver diagnostics.

## 3. Verification and OpenSpec completion

- [x] 3.1 Add focused source-set, invalid-state, canonicalization, and output regression tests; run risk-appropriate repository validation.
- [x] 3.2 Archive the completed review-fix change, inspect the updated specs, and validate all OpenSpec artifacts.
