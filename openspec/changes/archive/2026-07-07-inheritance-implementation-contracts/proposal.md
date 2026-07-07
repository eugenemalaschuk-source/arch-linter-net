# Proposal: inheritance-implementation-contracts

## Why

Dependency rules catch references, but several architecture violations are only expressible as type relationships: domain types inheriting from framework base classes (`UnityEngine.MonoBehaviour`, `Microsoft.EntityFrameworkCore.DbContext`, ASP.NET base types), or application ports being implemented outside the adapter/infrastructure boundary. Issue #87 asks for declarative, statically checked inheritance and implementation boundary contracts so policies can govern these relationships with deterministic diagnostics in both strict and audit modes.

## What Changes

- Add an **inheritance contract family** (`contracts.strict_inheritance` / `contracts.audit_inheritance`): forbid types in selected source layers/namespaces from inheriting (directly or transitively) from selected base types, matched by exact fully-qualified name (`forbidden_base_types`) or namespace/type prefix (`forbidden_base_type_prefixes`).
- Add an **interface implementation contract family** (`contracts.strict_interface_implementation` / `contracts.audit_interface_implementation`): restrict where selected interfaces (`interfaces` exact names, `interface_prefixes` prefixes) may be implemented, using the established location vocabulary `allowed_only_in_layers/namespaces/projects/assemblies` and `forbidden_in_layers/namespaces/projects/assemblies`.
- Both families support `id`, `reason`, `ignored_violations`, strict failure, audit-only reporting, and unmatched-ignore tracking, mirroring existing families.
- Emit deterministic diagnostics identifying the violating type, the matched base type / interface, the rule, and the expected boundary.
- Update JSON schema, capabilities metadata, docs (contracts pages, policy-format, AI policy-authoring guidance), and the architecture coverage report tooling.

No breaking changes: existing dependency/layer contract behavior is untouched; new YAML keys are additive.

## Capabilities

### New Capabilities
- `inheritance-contracts`: forbid inheritance from selected base types for types in selected source layers/namespaces, with exact and prefix matching, strict/audit modes, ignores, and deterministic diagnostics.
- `interface-implementation-contracts`: restrict where selected interfaces may be implemented via allowed-only and forbidden location lists (layers/namespaces/projects/assemblies), with exact and prefix matching, strict/audit modes, ignores, and deterministic diagnostics.

### Modified Capabilities

(none — existing contract family requirements are unchanged; new families are additive)

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` — two new contract classes + group properties.
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.*` — fail-closed load-time validation of the new families.
- `src/ArchLinterNet.Core/Execution` — catalog entries, handlers, two new `ArchitectureAnalysisSession` partials.
- `src/ArchLinterNet.Core/Scanning` — base-type chain / implemented-interface matching (reusing the defensive-reflection posture of `ArchitectureTypeRoleMatcher`, extended to generic and nested types).
- `src/ArchLinterNet.Core/Model` + `Reporting` — new diagnostic kinds/records, violation fields, formatter/mapper support.
- `schema/dependencies.arch.schema.json`, `archlinternet.capabilities.json` — schema and capability metadata.
- `docs/` — new contract pages, policy-format index, supported capabilities, AI policy-authoring guide.
- `tools/scripts/architecture_coverage_report.py` — recognize the new families.
- `tests/ArchLinterNet.Core.Tests` — fixtures and tests for both families (framework base type leakage, port implementation boundary, allowed adapter implementation, generic interfaces/base types, nested types, strict failure, audit-only, ignores, loader validation).
