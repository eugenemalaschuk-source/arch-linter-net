## 1. Public examples and acceptance coverage

- [x] 1.1 Add executable modular-monolith and Unity/client monolithic and imported sample policy trees.
- [x] 1.2 Add compact recommended-name, arbitrary-name, root-versus-fragment conflict, and fragment-versus-fragment conflict fixtures.
- [x] 1.3 Add NUnit acceptance tests that load committed fixtures and assert equivalent models/outcomes or typed conflicts.

## 2. Public authoring documentation

- [x] 2.1 Add the policy-import guide covering syntax, roles, composition, ordering, conflicts, boundaries, limits, unsupported behavior, migration, troubleshooting, and examples.
- [x] 2.2 Expand schema-reference and troubleshooting guidance for root/fragment editor association and import failures.
- [x] 2.3 Update AI authoring guidance and review checklist for focused fragments, minimal edits, global IDs, and root-based validation.
- [x] 2.4 Update policy-format, README, and MkDocs navigation links so the public guide and examples are discoverable.

## 3. Synchronization and validation

- [x] 3.1 Review the OpenSpec deltas against the implemented documentation and acceptance behavior, updating them if needed.
- [x] 3.2 Run `make fmt` through RTK and address formatting changes.
- [x] 3.3 Run `make acceptance` through RTK and resolve all failures.
