## Why

The v0.10 annotation contract defines six bounded context keys, but the implementation currently extracts metadata only from role annotations. Adding metadata-only context annotations will let projects assign type and assembly context defaults while preserving one primary role and feeding the existing selector and contract model.

## What Changes

- Ship the six approved metadata-only attributes (`Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, and `Runtime`) in the source-only annotation package.
- Extract their compile-time string values at type and assembly scope and compose them with the winning role's existing metadata.
- Apply the reviewed precedence and conflict rules deterministically; context evidence never creates or replaces a role.
- Prove the composed metadata is available through the existing role index and remains compatible with custom YAML mappings and legacy role-only policies.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `semantic-annotation-contract`: specify the metadata-only context annotation behavior and its bounded composition rules.
- `attribute-role-extraction`: extract canonical context evidence and compose it with custom or canonical role metadata.
- `semantic-role-index`: expose the effective composed metadata through existing role descriptors.
- `semantic-role-catalog`: make the approved metadata precedence and conflict behavior explicit in the specification.

## Impact

Affected code is limited to the annotation source package, Core canonical annotation extraction/composition, and focused Core tests. No new role, unrestricted metadata key, runtime dependency, policy authority, or public API is introduced.
