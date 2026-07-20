## 1. Native path resolution

- [x] 1.1 Replace Darwin ABI-dependent policy-file identity handling with packed `getattrlist` file identity and vnode-type validation while retaining regular-file, case, boundary, link, and hard-link checks.
- [x] 1.2 Enrich selected-root path-resolution failures with root-role provenance while retaining fragment import-edge provenance and chains.
- [x] 1.3 Classify Win32 and POSIX native failures separately, preserving native error context in append-only `PlatformFailure` diagnostics.

## 2. Regression coverage

- [x] 2.1 Add x86_64 and arm64 macOS regressions for regular fragments, hard links, and special files.
- [x] 2.2 Add typed diagnostic assertions for root/fragment provenance and Win32/POSIX native error classification.

## 3. Verification

- [x] 3.1 Run focused policy-import tests and the required formatting and acceptance validation.
