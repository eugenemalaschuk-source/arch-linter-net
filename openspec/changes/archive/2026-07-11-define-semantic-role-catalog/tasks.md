## 1. Catalog and metadata reference

- [x] 1.1 Create the first-wave role catalog with family, definition, static evidence sources, typical metadata, use cases, and support tier for every role.
- [x] 1.2 Define metadata key semantics, value examples, policy-use boundaries, and discouraged/deferred keys.
- [x] 1.3 Document optional type-level and assembly-level annotation conventions alongside equivalent full-name YAML mappings.

## 2. Examples and authoring guidance

- [x] 2.1 Add a Sales/Inventory/SharedKernel modular-monolith role map using narrow role and metadata selectors.
- [x] 2.2 Add a Unity/client namespace-convention example compatible with the existing static-analysis-only boundary.
- [x] 2.3 Add custom-attribute and assembly-level metadata mapping examples.
- [x] 2.4 Add AI policy-authoring guidance covering ambiguity, precedence, conflicts, narrow selectors, and safe exclusions.

## 3. Documentation integration

- [x] 3.1 Link the catalog from policy-format documentation and the AI authoring guide/navigation.
- [x] 3.2 Cross-check all examples against the existing semantic-classification-model specification and issue #172 acceptance criteria.

## 4. Validation and lifecycle

- [x] 4.1 Run documentation formatting/linting and OpenSpec validation; fix all findings.
- [x] 4.2 Run `make fmt` and `make acceptance` to confirm no repository regressions. (`make fmt` is blocked by missing WSL `/bin/bash`; `make acceptance` passed.)
- [x] 4.3 Synchronize and archive the OpenSpec change after implementation and validation.
