## Why

Any .NET project can acquire a broad framework surface such as `Microsoft.AspNetCore.App` through an MSBuild `FrameworkReference` item without a normal `PackageReference`. Existing `packages`/`package_dependency` contracts cannot see these declarations, so architecture policy has no way to forbid or restrict framework-reference coupling (for example, keeping `Microsoft.AspNetCore.App` out of domain/module projects). This closes that gap by adding first-class strict/audit `FrameworkReference` governance, parallel to the existing package contract-family architecture. (Closes #359, P1 slice only — reusable multi-source/glob authoring is #369, P2, and is explicitly out of scope here.)

## What Changes

- Add MSBuild `FrameworkReference` parsing to project discovery (`ArchitectureProjectFileParser`), capturing the `Include` name and `Condition`/TFM context per project. No `Version` handling — `FrameworkReference` items carry no version attribute.
- Add a new top-level `framework_references` policy section for named framework groups, mirroring `packages`: each group supports `framework_names` (exact match, case-insensitive) and `framework_name_prefixes` (dot-segment prefix match, case-insensitive).
- Add `contracts.strict_framework_dependency` / `contracts.audit_framework_dependency` contracts that forbid a named source project from declaring a `FrameworkReference` matching one or more forbidden framework groups.
- Add `contracts.strict_framework_allow_only` / `contracts.audit_framework_allow_only` contracts that restrict a named source project to only declaring `FrameworkReference`s matching an allowed set of framework groups.
- Add typed `FrameworkReferenceDiagnostic` / `FrameworkReferenceAllowOnlyDiagnostic` (+ payload types) distinct from package diagnostics, rendered equivalently across human, unified JSON, SARIF, and the Testing API.
- Add load-time validators: source must resolve to a declared `analysis.target_assemblies` entry; unknown/unusable framework groups reported as `<configuration>` violations (mirroring package contract validation).
- Extend exact baseline identity/comparer support so framework-reference occurrences are distinguished per project/condition/TFM, consistent with existing package/exact-identity baseline behavior.
- Update JSON schema (`schema/dependencies.arch.schema.json` and the fragment schema), capability manifest, and AI/policy-authoring docs.
- No **BREAKING** changes: policies without `framework_references`/`framework_*` contracts are unaffected.

## Capabilities

### New Capabilities

- `framework-reference-contracts`: Evaluates strict/audit forbidden-`FrameworkReference` contracts against named framework groups.
- `framework-reference-allow-only-contracts`: Evaluates strict/audit allow-list `FrameworkReference` contracts restricting a project to an approved framework set.

### Modified Capabilities

- `contract-family-registry`: register the two new contract families (`framework_dependency`, `framework_allow_only`) alongside existing families so catalog/dispatch/configuration-collection behavior covers them.

## Impact

- `src/ArchLinterNet.Core`: new discovery models/parsing, new contract family files, new session evaluators, new diagnostics/payloads, new validators, registry/baseline/comparer updates.
- `schema/`: JSON schema additions for `framework_references` groups and the four new contract-group arrays.
- `openspec/specs/`: two new capability specs; `contract-family-registry` spec updated with the new families.
- `docs/`: new contract/policy-format pages, capability manifest and AI policy-authoring guidance updates.
- `tests/ArchLinterNet.Core.Tests`: new fixture/unit tests mirroring package contract tests, plus additions to the cross-cutting "every contract family" tests.
- No changes to CLI surface area beyond existing generic contract dispatch; no changes to existing package/external/assembly contract behavior.
