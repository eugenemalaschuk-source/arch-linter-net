## 1. Publisher lifecycle

- [x] 1.1 Read release draft/immutable state and asset inventory from GitHub API metadata.
- [x] 1.2 Create releases as drafts, verify all candidate assets while draft, then publish and verify again.
- [x] 1.3 Resume matching drafts, preserve complete published retries, and fail before upload for incomplete immutable releases.
- [x] 1.4 Extend publisher tests for draft-first, resume, idempotency, and immutable failure cases.

## 2. Release contract and scope

- [x] 2.1 Update release-process documentation with draft-first behavior and immutable recovery guidance.
- [x] 2.2 Add the reviewed exact 0.9.1 release-scope declaration for issue #1042 / task #1043 and its regression assertion.
- [x] 2.3 Archive this OpenSpec change and validate all specs.

## 3. Validation and merge

- [x] 3.1 Run the release-tool test suite and applicable release-scope checks.
- [x] 3.2 Build and lint the updated release-process documentation and check the patch for whitespace errors.
- [x] 3.3 Review the implementation and validation results before pull request creation.
