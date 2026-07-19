## 1. Portable path resolution

- [x] 1.1 Replace Darwin ABI-dependent policy-file identity handling with managed canonical-path handling while retaining regular-file, case, boundary, and link checks.
- [x] 1.2 Enrich selected-root path-resolution failures with root-role provenance while retaining fragment import-edge provenance and chains.

## 2. Regression coverage

- [x] 2.1 Add a root-plus-fragment regression that exercises the portable resolver without native layout assumptions.
- [x] 2.2 Add typed diagnostic assertions that distinguish selected-root failures from fragment failures.

## 3. Verification

- [x] 3.1 Run focused policy-import tests and the required formatting and acceptance validation.
