## 1. Policy contract and loading

- [x] 1.1 Add strict/audit contract-surface exposure policy models, group/binding/catalog registration, and bounded source/forbidden selector validation; verify valid and malformed policies in focused Core schema/loading tests.
- [x] 1.2 Extend the 0.8 policy schema and raw-YAML validation for the new groups and selector objects; verify unknown, empty, unbounded, and invalid referenced-surface configurations fail closed.

## 2. Exposure evaluation and lifecycle integration

- [x] 2.1 Resolve direct and reviewed-public-API source roots through existing type/role/public-surface evidence and evaluate #512 exposure facts against forbidden selectors; verify direct, nested generic, metadata, and same-named cross-assembly paths.
- [x] 2.2 Project one required applicability record per exposure control and fail closed for zero-match or incomplete facts; verify strict/audit, Human/JSON/SARIF/Testing parity and canonical reason/provenance.
- [x] 2.3 Register typed exposure payload, ignore/baseline identity, baseline group handling, and standard handler-result propagation; verify distinct paths retain distinct baseline identities.

## 3. Documentation and integration proof

- [x] 3.1 Document authoring syntax, selector composition, API-membership/semantic-role separation, recursive diagnostics, applicability behavior, and non-goals; verify documentation links and policy examples are valid.
- [x] 3.2 Synchronize the change artifacts with delivered behavior and run focused Core tests, `make fmt`, relevant lint/schema checks, and strict OpenSpec validation; record exact passing commands.
