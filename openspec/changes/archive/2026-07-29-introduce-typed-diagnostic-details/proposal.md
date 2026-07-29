## Why

The existing diagnostic DTO hierarchy contains typed evidence, but public JSON and SARIF independently project selected fields into format-specific shapes. This makes the externally consumable contract lossy and forces consumers to infer meaning from adapter-specific fields and messages. The 0.5.1 compatibility contract requires one versioned normalized finding that preserves identity, provenance, baseline lifecycle, and family-specific evidence consistently.

## What Changes

- Introduce a versioned, discriminated normalized finding envelope with a stable kind, canonical identity, policy origin, baseline state, and typed details.
- Make human, JSON, SARIF, Testing API, and baseline workflows consume projections of that one normalized finding rather than reconstructing evidence from display text or unrelated DTO fields.
- Ship and document the JSON schema and compatibility/deprecation rules for existing JSON fields.
- Add exhaustive serialization, parity, ordering, identity, and forward-compatibility coverage for the supported finding families.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `diagnostics-model`: define the normalized, versioned finding envelope and typed detail records.
- `violation-reporting`: expose versioned normalized findings in the machine-readable diagnostic output while retaining documented compatibility fields.
- `sarif-diagnostics-output`: preserve the normalized typed details and physical locations in SARIF properties.
- `test-adapter`: expose normalized findings and baseline lifecycle without display-text parsing.
- `packaged-schema-registry`: package and publish the matching normalized JSON diagnostic schema.

## Impact

Affected areas include Core diagnostic model and mapper code, JSON and SARIF formatters, CLI validation output, Testing API and baseline integrations, packaged schemas, documentation, and their NUnit contract tests. The change does not alter architecture-check semantics or baseline identity computation.
