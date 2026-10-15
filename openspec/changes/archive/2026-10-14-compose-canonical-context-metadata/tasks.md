## 1. Source-only context annotations

- [x] 1.1 Add the six manifest-defined metadata-only attributes to the source package with the reviewed constructors, targets, visibility, and C# 9 compatibility.
- [x] 1.2 Add or update source-contract tests for all six identities and confirm none is exposed as a role attribute.

## 2. Core extraction and composition

- [x] 2.1 Resolve exact canonical context FQNs to bounded metadata keys and validate catalog identity and compile-time string values.
- [x] 2.2 Compose winning role metadata, type context values, and assembly context defaults in the approved order; preserve custom YAML role metadata and source-enable behavior.
- [x] 2.3 Emit deterministic per-key diagnostics for same-scope context conflicts without changing role selection or discarding unrelated metadata.

## 3. Integration and validation

- [x] 3.1 Add focused tests for role properties, type/assembly composition, conflict behavior, custom YAML mappings, context-only evidence, and role-only compatibility.
- [x] 3.2 Verify composed metadata flows through existing selectors, contextual contracts, coverage/explain, and CLI/Testing projections.
- [x] 3.3 Synchronize relevant documentation if implementation reveals a mismatch, run focused tests and required formatting/lint/OpenSpec checks, then archive the change.
