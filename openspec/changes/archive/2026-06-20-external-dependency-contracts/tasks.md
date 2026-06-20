## 1. Policy Model And Loading

- [x] 1.1 Add `ArchitectureExternalDependencyGroup` with `namespace_prefixes` and `type_prefixes` YAML fields.
- [x] 1.2 Add top-level `ExternalDependencies` to `ArchitectureContractDocument` mapped from `external_dependencies`.
- [x] 1.3 Add `ArchitectureExternalDependencyContract` with `id`, `name`, `source`, `forbidden`, `ignored_violations`, and `reason`.
- [x] 1.4 Add `StrictExternal` and `AuditExternal` groups to `ArchitectureContractGroups` and include them in strict/audit contract enumeration.
- [x] 1.5 Update fallback ID assignment and duplicate ID validation for the new external contract groups.
- [x] 1.6 Add configuration validation for external contracts that reference missing source layers or unknown external dependency groups.

## 2. Matching And Execution

- [x] 2.1 Add a focused external dependency matcher for exact-or-child namespace prefixes and full type-name prefixes.
- [x] 2.2 Add external violation finding over direct referenced types returned by the current reference scanner.
- [x] 2.3 Ensure matching uses only referenced type metadata visible from first-party project types and does not traverse third-party package internals.
- [x] 2.4 Add `CheckExternalContract` execution that resolves the source layer, scans source types, evaluates forbidden groups, applies ignored violations, and returns deterministic violations.
- [x] 2.5 Keep external contracts direct-only for this change and do not add transitive external dependency traversal.

## 3. Validation Surfaces

- [x] 3.1 Wire strict and audit external contract execution into the CLI validation flow.
- [x] 3.2 Wire strict and audit external contract execution into the testing adapter validation flow.
- [x] 3.3 Wire strict external contract execution into `ArchitectureValidator`.
- [x] 3.4 Include external contract IDs in `--contract` selection and unknown-ID reporting.
- [x] 3.5 Preserve existing audit-mode CLI behavior while ensuring strict validation ignores `audit_external` violations.

## 4. Diagnostics

- [x] 4.1 Extend violation data or formatter output to identify the forbidden external dependency group.
- [x] 4.2 Preserve deterministic human-readable output for external dependency violations.
- [x] 4.3 Preserve existing JSON output shape where practical and add external group context without renaming existing fields unnecessarily.
- [x] 4.4 Add diagnostic coverage for source type, contract ID, forbidden external group, and matched forbidden references.

## 5. Schema, Docs, And Samples

- [x] 5.1 Update `schema/dependencies.arch.schema.json` with `external_dependencies`, `strict_external`, and `audit_external`.
- [x] 5.2 Update policy format and contracts documentation with the new YAML shape and scanner boundaries.
- [x] 5.3 Update AI-facing policy authoring guidance, capabilities, and review checklist to prefer `external_dependencies` for vendor/framework leakage rules.
- [x] 5.4 Update at least one sample policy to show Unity runtime/editor or infrastructure SDK external dependency rules.
- [x] 5.5 Document that `external: true` layers remain supported but are not the preferred model for new vendor/framework controls.

## 6. Tests And Validation

- [x] 6.1 Add loader/model tests for external dependency declarations and strict/audit external contracts.
- [x] 6.2 Add matcher tests for namespace exact match, namespace child match, namespace sibling non-match, type exact match, and type prefix match.
- [x] 6.3 Add core execution tests for strict external violations and passing external contracts.
- [x] 6.4 Add tests for audit external reporting without strict validation failure.
- [x] 6.5 Add tests for ignored violations in external contracts.
- [x] 6.6 Add diagnostics/JSON tests for external group and forbidden reference reporting.
- [x] 6.7 Add CLI or testing-adapter coverage for external contracts if existing integration test infrastructure supports it cleanly.
- [x] 6.8 Run `rtk make restore` if needed, then `rtk make acceptance` before marking implementation complete.
