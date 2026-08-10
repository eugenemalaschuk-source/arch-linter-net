## 1. Policy model and assembly expansion

- [x] 1.1 Make directional assembly dependency and allow-only contracts implement the existing
  source-expandable interface with explicit clone semantics.
- [x] 1.2 Register all strict/audit directional assembly groups in the source-set expander and
  preserve existing subtraction, identity, and provenance behavior.
- [x] 1.3 Extend schema validation and schema artifacts for the new directional assembly selector
  fields.

## 2. Discovered project-set resolution

- [x] 2.1 Separate project-kind source-set validation/resolution from eager assembly/layer
  expansion while preserving explicit-project behavior.
- [x] 2.2 Bind project-kind members and constrained path globs to the final filtered discovery
  inventory before project-metadata execution, with deterministic provenance and fail-closed
  optional-empty handling.
- [x] 2.3 Ensure deferred union is idempotent across runner setup paths and validates resolved
  project-metadata contracts before execution.

## 3. Tests and public surfaces

- [x] 3.1 Add focused assembly dependency/allow-only expansion, direct-finding, authored-ID
  coverage, subtraction, and large-inventory regression tests.
- [x] 3.2 Add solution-discovered project-set selector, include/exclude, zero-match, imported
  provenance, and repeated-setup regression tests.
- [x] 3.3 Update coverage/explain/JSON/SARIF projections and tests for the new source-set
  provenance where required.
- [x] 3.4 Update reference documentation, capability metadata, schema docs, and AI authoring
  guidance with the distinct project path-glob grammar.

## 4. Verification and spec completion

- [x] 4.1 Run focused tests, format the repository, inspect the diff, and run `make acceptance`.
- [x] 4.2 Synchronize implementation and specs, validate OpenSpec strictly, archive the change,
  and validate all specs.
