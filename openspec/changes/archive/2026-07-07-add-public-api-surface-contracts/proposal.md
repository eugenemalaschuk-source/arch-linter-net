## Why

ArchLinterNet already validates dependency direction, layer containment, protected surfaces, and external/package/assembly boundaries — but it has no way to say "this assembly's exported public/protected API is exactly this set of types and members." Issue #83 asks for a first-class contract family so library assemblies can declare their intended public API surface and fail validation when a type or member is accidentally exported (made `public`/`protected`/`protected internal`) without being declared, including special handling for public constants, which are risky for consumers because compilers inline const values at compile time.

## What Changes

- Add a new contract family `public_api_surface` (YAML groups `strict_public_api_surface`/`audit_public_api_surface`) that:
  - Targets one or more assemblies (`assemblies: [...]`) by assembly name.
  - Declares the expected exported API surface inline as a YAML list of deterministic, normalized signature strings (`declared_api: [...]`) — an ArchLinterNet-native format, not the two-file `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` Roslyn-analyzer convention.
  - Reports every exported (`public`, `protected`, or `protected internal`) type or member reachable from the target assemblies that is not present in `declared_api`.
  - Supports an opt-in `forbid_public_constants_unless_declared` boolean (default `false`) plus an `allowed_public_constants` list of fully-qualified member names, giving policy authors an explicit lever against accidental public constants without changing the default behavior of existing/simple policies.
- `protected` and `protected internal` members are treated as exported API surface by default (same as `public`) — they are visible to any external subclass, so undeclared ones are violations.
- New model `ArchitecturePublicApiSurfaceContract`, wired through the existing catalog/handler-registry seam — zero changes to `ArchitectureContractExecutor` dispatch.
- New reflection-based signature scanner reusing existing `ArchitectureTypeScanner`/`ArchitectureTypeIndex` idioms (no new Roslyn-compilation plumbing; project-aware Roslyn stays scoped to method-body call resolution).
- Diagnostics: new optional fields on `ArchitectureViolation`, a new `ArchitectureDiagnosticKind.PublicApiSurface`, a new `PublicApiSurfaceDiagnostic` record, and mapper/formatter wiring (human text + CI JSON) — one violation per undeclared exported type/member.
- JSON schema, docs (`docs/contracts/public-api-surface.md` + index/policy-format/AI-guidance updates), and `archlinternet.capabilities.json` updated.
- Strict contracts fail validation; audit contracts report without failing. Existing contract families are unchanged (purely additive).

## Capabilities

### New Capabilities
- `public-api-surface-contracts`: strict/audit contracts that declare the intended exported public/protected API surface (types, methods, properties, fields, constants, events) of one or more target assemblies as a normalized signature allowlist, and report any exported type or member not present in that allowlist, with an opt-in stricter mode for public constants.

### Modified Capabilities
(none — existing dependency, layer, external, package, protected-surface, and type-placement contract families are unchanged; this is purely additive.)

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`: new `ArchitecturePublicApiSurfaceContract` model, new `StrictPublicApiSurface`/`AuditPublicApiSurface` groups on `ArchitectureContractGroups`.
- `src/ArchLinterNet.Core/Scanning/ArchitecturePublicApiSurfaceScanner.cs` (new): reflection-based enumeration and normalization of exported types/members into signature strings.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.PublicApiSurface.cs` (new): `CheckPublicApiSurfaceContract`.
- `src/ArchLinterNet.Core/Model/ArchitectureViolation.cs`: new optional fields for the undeclared signature and forbidden-constant reason.
- `src/ArchLinterNet.Core/Model/ArchitectureDiagnosticKind.cs`, `src/ArchLinterNet.Core/Model/PublicApiSurfaceDiagnostic.cs` (new).
- `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticMapper.cs`, `ArchitectureDiagnosticFormatter.cs`.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractCatalog.cs`: two new `AddGroup` calls.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractHandlers.cs`: new `PublicApiSurfaceContractHandler`.
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs`: one new DI registration (if applicable to this seam).
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs`: wire new groups into duplicate-ID validation.
- `schema/dependencies.arch.schema.json`: new `$defs.publicApiSurfaceContract` and two new array properties.
- `docs/contracts/public-api-surface.md` (new), `docs/contracts/index.md`, `docs/policy-format/index.md`, `docs/policy-format/supported-capabilities.md`, `docs/reference/yaml-schema.md`, `docs/ai/capabilities.md`, `docs/ai/policy-authoring-guide.md`, `archlinternet.capabilities.json`.
- New tests under `tests/ArchLinterNet.Core.Tests/`.
