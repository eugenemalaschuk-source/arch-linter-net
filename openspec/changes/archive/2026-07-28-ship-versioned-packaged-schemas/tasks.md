## 1. Release schema resources

- [x] 1.1 Add the versioned 0.5.1 manifest and eight release-qualified JSON Schema resources with verified identities and digests.
- [x] 1.2 Embed and pack the complete registry resource tree from Core, while retaining repository aliases for source authoring.

## 2. Core and CLI discovery

- [x] 2.1 Implement the typed Core packaged-schema registry with integrity and version validation.
- [x] 2.2 Compose `schema list` and `schema print` CLI handlers that consume the Core registry offline.

## 3. Contract evidence and documentation

- [x] 3.1 Add Core, CLI, and package artifact tests for offline discovery, resource integrity, version skew, and package contents.
- [x] 3.2 Update capability metadata, public schema documentation, CLI help, and release notes with all supported format versions and immutable editor examples.

## 4. Validation and synchronization

- [x] 4.1 Run focused tests, format, repository acceptance, and package validation; fix any issue-related failures.
- [x] 4.2 Synchronize the final OpenSpec artifacts, validate them, archive the change, and inspect the generated main specifications.
