## 1. Canonical paired manifest

- [x] 1.1 Implement schema-v2 creation and strict parsing for deterministic paired `.nupkg` and `.snupkg` package subjects.
- [x] 1.2 Implement fail-closed digest, size, filename, version, source-commit, duplicate, and exact-directory inventory verification.
- [x] 1.3 Implement deterministic derived checksum rendering and manifest-selected path output without recursive manifest identity.
- [x] 1.4 Add focused manifest tests for valid pairs, deterministic output, missing/unexpected/duplicate subjects, tampering, identity mismatches, and bounded v1 compatibility.

## 2. Release-evidence and workflow handoffs

- [x] 2.1 Update Checkpoint B and release-evidence helpers/tests to retain and reject mismatched complete paired subject inventory.
- [x] 2.2 Update CI and release workflow candidate creation, validation, checksum generation, and downstream artifact handoffs for v2.
- [x] 2.3 Derive NuGet upload and GitHub Release attachment paths from the verified manifest and prove symbol packages are checked without double-push.

## 3. Documentation and validation

- [x] 3.1 Document the canonical pre-publication inventory, derived checksum evidence, and NuGet.org repository-signing boundary.
- [x] 3.2 Run focused release-tool tests, workflow lint, formatter, and strict OpenSpec validation; fix related failures.
- [x] 3.3 Synchronize the actual behavior into the OpenSpec change, archive it, and validate all resulting specs.
