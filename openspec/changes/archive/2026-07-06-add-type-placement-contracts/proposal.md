## Why

ArchLinterNet already validates dependency direction, layer containment, external/package/assembly boundaries, and method-body usage — but it has no way to say "this architectural role must live here" or "this architectural role must be named like this." Issue #85 asks for a first-class contract family that governs *where* an architectural role (a controller, a handler, a domain event, a `MonoBehaviour`) is allowed to live and *how* it must be named, using constrained, schema-backed matchers rather than free-form regex/expression selectors.

## What Changes

- Add a new contract family `type_placement` (YAML groups `strict_type_placement`/`audit_type_placement`) that selects types by constrained matchers (`name_suffix`, `name_prefix`, `namespace`, `layer`, `base_type`, `implements_interface`, `has_attribute`, combined with AND semantics) and enforces:
  - **Placement**: the matched type's actual layer/namespace/project/assembly must be one of a declared allowed set (`must_reside_in_layers`/`must_reside_in_namespaces`/`must_reside_in_projects`/`must_reside_in_assemblies`).
  - **Naming**: the matched type's simple name must/must-not carry a declared suffix/prefix (`required_name_suffix`/`required_name_prefix`/`forbidden_name_suffix`/`forbidden_name_prefix`).
  - A rule must declare at least one placement expectation or one naming expectation — a selector with no expectation is a load-time configuration error.
- New model `ArchitectureTypePlacementContract` (+ nested `ArchitectureTypeMatcher`), wired through the existing catalog/handler-registry seam — **zero changes to `ArchitectureContractExecutor`**.
- New reflection-based matcher helper reusing `ArchitectureTypeIndex`/`Type.BaseType`/`Type.GetInterfaces()`/`Type.GetCustomAttributesData()` — no new Roslyn symbol plumbing (project-aware Roslyn from #61 stays scoped to method-body call resolution).
- `must_reside_in_projects` is documented explicitly as assembly-name equivalence via project discovery (`ArchitectureDiscoveredProject.AssemblyName`), not physical `.csproj` membership — there is no Type→project mapping anywhere in this codebase today, so this is a scoped, honest capability, not a hidden gap.
- Diagnostics: four new optional fields on `ArchitectureViolation` (`ExpectedTypeLocation`, `ActualTypeLocation`, `ExpectedTypeName`, `ActualTypeName`), a new `ArchitectureDiagnosticKind.TypePlacement`, a new `TypePlacementDiagnostic` record, and mapper/formatter wiring (human text + CI JSON) — one diagnostic per (type, contract), carrying whichever of placement/naming actually failed.
- JSON schema, docs (`docs/contracts/type-placement.md` + index/policy-format/AI-guidance updates), and `archlinternet.capabilities.json` updated.
- Strict contracts fail validation; audit contracts report without failing. Existing contract families are unchanged (purely additive).

## Capabilities

### New Capabilities
- `type-placement-contracts`: strict/audit contracts that select architectural roles by name/namespace/layer/base-type/interface/attribute matchers and require them to reside in a declared layer/namespace/project/assembly and/or carry a declared naming suffix/prefix.

### Modified Capabilities
(none — existing dependency, layer, external, package, and assembly contract families are unchanged; this is purely additive.)

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`: new `ArchitectureTypePlacementContract`/`ArchitectureTypeMatcher` models, new `StrictTypePlacement`/`AuditTypePlacement` groups on `ArchitectureContractGroups`.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.TypePlacement.cs` (new): `CheckTypePlacementContract`.
- `src/ArchLinterNet.Core/Scanning/ArchitectureTypeRoleMatcher.cs` (new): reflection-based matcher combination logic.
- `src/ArchLinterNet.Core/Model/ArchitectureViolation.cs`: four new optional fields.
- `src/ArchLinterNet.Core/Model/ArchitectureDiagnosticKind.cs`, `src/ArchLinterNet.Core/Model/TypePlacementDiagnostic.cs` (new).
- `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticMapper.cs`, `ArchitectureDiagnosticFormatter.cs`.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractCatalog.cs`: two new `AddGroup` calls.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractHandlers.cs`: new `TypePlacementContractHandler`.
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs`: one new DI registration.
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs`: wire new groups into duplicate-ID + "selector with no expectation" validation.
- `schema/dependencies.arch.schema.json`: new `$defs.typePlacementContract`/`$defs.typeMatcher` and two new array properties.
- `docs/contracts/type-placement.md` (new), `docs/contracts/index.md`, `docs/policy-format/index.md`, `docs/policy-format/supported-capabilities.md`, `docs/reference/yaml-schema.md`, `docs/ai/capabilities.md`, `docs/ai/policy-authoring-guide.md`, `archlinternet.capabilities.json`.
- New tests under `tests/ArchLinterNet.Core.Tests/`.
