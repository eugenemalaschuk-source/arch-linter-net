# Fix semantic consumer coverage

## Why

Semantic coverage can currently treat a type as governed when a contextual selector only shares its role and metadata key, even if the selector's metadata value or operator does not match. It can also silently broaden a semantic exclusion when an exclusion field is misspelled, and it reports externally owned selector layers as stale.

## What Changes

- Preserve and evaluate the complete contextual selector when computing semantic coverage and stale contextual-consumer evidence.
- Reject unknown fields in semantic coverage exclusion mappings before permissive YAML deserialization drops them.
- Exclude `external: true` selector-backed layers from semantic stale-selector evidence.

## Impact

- Affected capabilities: `architecture-coverage-reporting`, `yaml-contract-loading`
- Affected code: semantic coverage session logic, contextual consumer registry, YAML policy loader, Core tests
