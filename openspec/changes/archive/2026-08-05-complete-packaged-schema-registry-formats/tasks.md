## 1. Registry and package resources

- [x] 1.1 Add immutable manifest descriptors and SHA-256 digests for the deferred analysis-cache and analysis-profile source schemas.
- [x] 1.2 Embed the exact cache/profile source schemas in Core and publish them under the release-matched NuGet `contentFiles` schema path.

## 2. Executable release-contract tests

- [x] 2.1 Extend registry tests to retain normalized-finding coverage and verify cache/profile resources, identity, digests, and real public-producer output.
- [x] 2.2 Extend package/offline smoke coverage to validate local-package discovery and `schema print` byte equivalence for the newly registered cache/profile formats.

## 3. Documentation and specification synchronization

- [x] 3.1 Update public schema reference and capability inventory to identify the complete implemented 0.5.1 registry and format compatibility behavior.
- [x] 3.2 Run focused tests, formatting, package checks, acceptance, and strict OpenSpec validation; resolve issue-scope failures.
- [x] 3.3 Synchronize the actual behavior with the change artifacts and archive the OpenSpec change.
