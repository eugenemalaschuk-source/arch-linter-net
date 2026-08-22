## 1. Frozen-subject inventory

- [x] 1.1 Extend the canonical manifest tool to verify the exact manifest and derived-checksum evidence set and render exact attestation subject inventories.
- [x] 1.2 Add focused release-tool tests for evidence tampering and missing/unexpected subject rejection.

## 2. Independent provenance verification

- [x] 2.1 Implement a consumer-style verifier that validates every package and evidence attestation with repository, signer-workflow, and source-commit constraints.
- [x] 2.2 Add focused tests covering complete verification, missing attestation, and package/manifest/checksum tamper negatives.

## 3. Release workflow handoffs

- [x] 3.1 Add the least-privilege, SHA-pinned attestation job after Checkpoint B and before publication.
- [x] 3.2 Add the separate provenance verification job and make NuGet upload and GitHub Release attachment depend on its success while reusing frozen subjects.
- [x] 3.3 Extend workflow tests to prove exact inventories, permissions, immutable pins, job ordering, and no rebuild/regeneration handoff.

## 4. Documentation and synchronization

- [x] 4.1 Document GitHub Release asset verification, the distinct trusted-publishing/provenance/repository-signing boundaries, and post-publication limitations.
- [x] 4.2 Synchronize the OpenSpec change with the implemented behavior and mark completed tasks.

## 5. Validation and archive

- [x] 5.1 Run focused release-tool and workflow tests, format changed files, and run workflow lint.
- [x] 5.2 Run strict OpenSpec validation, archive the synchronized change, and validate all main specs.
