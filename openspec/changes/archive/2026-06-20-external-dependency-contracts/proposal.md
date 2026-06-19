## Why

Architecture policies currently model vendor and framework namespaces through `external: true` layers, but Unity, SDK, and framework references are not first-party architecture layers. Projects need first-class external dependency contracts so pure or low-level layers can prevent framework/vendor leakage while keeping existing layer contracts focused on first-party boundaries.

## What Changes

- Add top-level `external_dependencies` declarations for named vendor/framework dependency groups.
- Add `strict_external` and `audit_external` contract groups that forbid references from a source layer to one or more external dependency groups.
- Match external references by namespace prefixes and type prefixes using referenced type metadata already available from project types.
- Emit diagnostics that identify the source type, contract, forbidden external dependency group, and matched external reference.
- Preserve existing `external: true` layer behavior as a backward-compatible escape hatch, while steering new docs and examples toward `external_dependencies` for vendor/framework control.
- Document MVP limits: no full method-body analysis, no static analysis of third-party package internals, and no guarantee when external assemblies are not resolvable enough for referenced type metadata.
- Update JSON schema, docs, examples, tests, and AI-facing guidance for the new policy shape.

## Capabilities

### New Capabilities
- `external-dependency-contracts`: First-class policy declarations and strict/audit contracts for controlling references from first-party layers to external vendor/framework dependency groups.

### Modified Capabilities

## Impact

- Core policy model and YAML loading gain `external_dependencies`, `strict_external`, and `audit_external` fields.
- Contract execution, CLI validation, test adapter validation, and strict validator need to evaluate the new external contract family consistently.
- Diagnostics and JSON output need external group context while remaining deterministic and compatible with existing violation reporting.
- Schema, docs, sample policies, and AI policy-authoring guidance need updates to describe the new model and its explicit scanner boundaries.
