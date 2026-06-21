## Why

External dependency contracts currently only detect forbidden vendor/framework references visible through type-level metadata (interfaces, base types, fields, properties, parameters, return types). References that appear exclusively inside method bodies — such as `UnityEngine.Debug.Log(...)` or `new NpgsqlConnection(connectionString).OpenAsync()` — slip through undetected. This is a known gap documented in the existing spec as "method-body-only usage is not guaranteed." Closing it makes external dependency governance more complete without changing the static-analysis scope.

## What Changes

- Extend external dependency violation detection to inspect method body IL references for forbidden external dependency groups.
- Reuse the existing `ArchitectureExternalDependencyResolver.MatchesGroup` namespace/type prefix matching rules — no new matching logic.
- Emit diagnostics that identify the violating source type, source member (method/constructor), forbidden external group, and the referenced external member/type.
- Keep strict and audit behavior consistent with existing external dependency contracts.
- Avoid scanning third-party package internals; only inspect first-party assemblies under validation.
- Update the existing `external-dependency-contracts` spec to reflect that method-body references are now detected (removing the "not guaranteed" carve-out).

## Capabilities

### Modified Capabilities
- `external-dependency-contracts`: The "Use referenced type metadata visible from project types" requirement changes — method-body-only references are no longer excluded from detection. A new scenario is added for method-body detection.
- `method-body-contracts`: May need a delta spec if the IL scanner's reuse path introduces any spec-visible behavior (e.g., new diagnostic category). Review during design.

### New Capabilities
(none — this extends existing capabilities)

## Impact

- **Code**: `ArchitectureExternalDependencyViolationFinder`, `ArchitectureContractRunner.Checking`, possibly new IL scanner adapter for external deps.
- **Diagnostics**: New violation category for method-body external dep references (includes method name in output).
- **Policy YAML**: No schema changes needed — existing `strict_external`/`audit_external` contracts gain method-body scanning automatically.
- **Tests**: New test fixtures for method calls, constructor calls, property access, generic types, allowed adapter layers, strict failure, audit-only reporting.
- **Docs**: Policy authoring guidance and architecture governance docs updated to reflect method-body detection.
