## Why

ArchLinterNet already validates dependency direction, layer containment, external/package/assembly boundaries, and where architectural roles must live and how they must be named — but it has no way to say "this attribute/marker must only appear in this layer" or "this attribute must never appear in that layer." Issue #86 asks for a first-class contract family so policy authors can declaratively govern where specific attribute types (ASP.NET routing attributes, Unity serialization attributes, custom markers, etc.) are allowed or forbidden to appear, catching architectural-boundary leaks (e.g. an ASP.NET `[Route]` attribute showing up outside the API layer) without attempting to validate the semantic/security correctness of those markers.

## What Changes

- Add a new contract family `attribute_usage` (YAML groups `strict_attribute_usage`/`audit_attribute_usage`) that:
  - Targets one or more attribute types by exact fully-qualified name (`attributes: [...]`) and/or namespace/type-name prefix (`attribute_prefixes: [...]`).
  - Declares an `allowed_only_in_layers`/`allowed_only_in_namespaces`/`allowed_only_in_projects`/`allowed_only_in_assemblies` allow-list: any matching attribute usage found outside every declared location is a "misplaced" violation.
  - Declares a `forbidden_in_layers`/`forbidden_in_namespaces`/`forbidden_in_projects`/`forbidden_in_assemblies` deny-list: any matching attribute usage found inside a declared forbidden location is a "forbidden" violation.
  - A rule must declare at least one attribute selector (`attributes` or `attribute_prefixes`) and at least one location expectation (an `allowed_only_in_*` or `forbidden_in_*` list) — a selector with nothing to check is a load-time configuration error.
- Scans every declared member (constructors, methods excluding property/event accessor methods, properties, fields, events) and the type itself, **regardless of visibility** — unlike the public-API-surface family, this scanner does not filter by `public`/`protected`, since markers like Unity's `[SerializeField]` commonly decorate private fields and `[Authorize]`/`[Route]` can be internal.
- A member decorated with two different configured attributes yields two separate violations (one per matched attribute).
- New model `ArchitectureAttributeUsageContract`, wired through the existing catalog/handler-registry seam — zero changes to `ArchitectureContractExecutor`.
- New reflection-based attribute scanner reusing existing `ArchitectureTypeIndex`/`GetCustomAttributesData()` idioms, mirroring the defensive-reflection posture of `ArchitecturePublicApiSurfaceScanner`/`ArchitectureTypeRoleMatcher`.
- Diagnostics: four new optional fields on `ArchitectureViolation`, a new `ArchitectureDiagnosticKind.AttributeUsage`, a new `AttributeUsageDiagnostic` record, and mapper/formatter wiring (human text + CI JSON).
- JSON schema, docs (`docs/contracts/attribute-usage.md` + index/policy-format/AI-guidance updates), and `archlinternet.capabilities.json` updated.
- Strict contracts fail validation; audit contracts report without failing. Existing contract families are unchanged (purely additive).
- **Explicitly out of scope / deferred**: this is static marker *presence and placement* validation only, not a runtime authorization/security correctness check, and it does not implement "required marker" rules (e.g. "every public endpoint must carry `[Authorize]` or `[AllowAnonymous]`"). That capability is deferred to a documented follow-up.

## Capabilities

### New Capabilities
- `attribute-usage-contracts`: strict/audit contracts that declare which layers/namespaces/projects/assemblies specific attribute types are allowed to appear in (allow-list) or forbidden from appearing in (deny-list), scanning all types and members regardless of visibility.

### Modified Capabilities
(none — existing dependency, layer, external, package, protected-surface, type-placement, and public-api-surface contract families are unchanged; this is purely additive.)

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`: new `ArchitectureAttributeUsageContract` model, new `StrictAttributeUsage`/`AuditAttributeUsage` groups on `ArchitectureContractGroups`.
- `src/ArchLinterNet.Core/Scanning/ArchitectureAttributeUsageScanner.cs` (new): reflection-based enumeration of type/member attribute matches.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.AttributeUsage.cs` (new): `CheckAttributeUsageContract`.
- `src/ArchLinterNet.Core/Model/ArchitectureViolation.cs`: four new optional fields (`MatchedAttribute`, `AttributeUsageKind`, `ExpectedAttributeLocation`, `ActualAttributeLocation`).
- `src/ArchLinterNet.Core/Model/ArchitectureDiagnosticKind.cs`, `src/ArchLinterNet.Core/Model/AttributeUsageDiagnostic.cs` (new).
- `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticMapper.cs`, `ArchitectureDiagnosticFormatter.cs`.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractCatalog.cs`: two new `AddGroup` calls.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractHandlers.cs`: new `AttributeUsageContractHandler`.
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs`: one new DI registration.
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs`: wire new groups into duplicate-ID validation, plus new validation rejecting a contract with no attribute selector or no location expectation. Split into a `partial class` with a new `ArchitecturePolicyDocumentLoader.CoverageValidation.cs` (moving the pre-existing coverage-scope validation methods) to stay under the repo's 800-line file-size lint limit after the new validation method was added.
- `schema/dependencies.arch.schema.json`: new `$defs.attributeUsageContract` and two new array properties.
- `docs/contracts/attribute-usage.md` (new), `docs/contracts/index.md`, `docs/policy-format/index.md`, `docs/policy-format/supported-capabilities.md`, `docs/ai/capabilities.md`, `docs/ai/policy-authoring-guide.md`, `archlinternet.capabilities.json`.
- New tests under `tests/ArchLinterNet.Core.Tests/`.
