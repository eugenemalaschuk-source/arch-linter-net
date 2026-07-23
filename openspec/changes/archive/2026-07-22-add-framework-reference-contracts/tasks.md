## 1. Discovery / MSBuild parsing

- [x] 1.1 Add `ArchitectureDiscoveredFrameworkReference(string FrameworkName, string? Condition)` to `ProjectDiscoveryModels.cs`, mirroring `ArchitectureDiscoveredPackageReference`.
- [x] 1.2 Add `FrameworkReferences` list property to `DiscoveredProjectFile` and `ArchitectureDiscoveredProject`.
- [x] 1.3 Implement `ArchitectureProjectFileParser.ParseFrameworkReferences(XDocument)` parsing `FrameworkReference` items' `Include` and `Condition` attributes; wire into `Parse()`.
- [x] 1.4 Wire the new list through `ArchitectureProjectDiscoveryService`'s `DiscoveredProjectFile` → `ArchitectureDiscoveredProject` projection.

## 2. Contract models and schema

- [x] 2.1 Add `ArchitectureFrameworkReferenceGroup` model (`FrameworkNames`, `FrameworkNamePrefixes`) and a `FrameworkReferences` dictionary on `ArchitectureContractDocument`, mirroring `ArchitecturePackageGroup`/`Packages`.
- [x] 2.2 Add `Contracts/Families/FrameworkReferenceContractFamily.cs` (`ArchitectureFrameworkReferenceContract`: `Name`, `Id`, `Source`, `Forbidden`, `IgnoredViolations`, `Reason` — no `DependencyDepth`) plus `strict_framework_dependency`/`audit_framework_dependency` group properties on `ArchitectureContractGroups`.
- [x] 2.3 Add `Contracts/Families/FrameworkReferenceAllowOnlyContractFamily.cs` (`ArchitectureFrameworkReferenceAllowOnlyContract`: `Name`, `Id`, `Source`, `Allowed`, `IgnoredViolations`, `Reason`) plus `strict_framework_allow_only`/`audit_framework_allow_only` group properties.
- [x] 2.4 Extend `schema/dependencies.arch.schema.json`: add `frameworkReferenceGroup` $def, top-level `framework_references` section, `frameworkDependencyContract`/`frameworkAllowOnlyContract` $defs (no `dependency_depth`), and the four new contract-group array declarations.
- [x] 2.5 Apply the same schema additions to `schema/dependencies.arch.fragment.schema.json`.

## 3. Matching and evaluation

- [x] 3.1 Implement framework-reference matching (exact + dot-segment prefix, case-insensitive) — either a new `Resolution/ArchitectureFrameworkReferenceResolver.cs` mirroring `ArchitecturePackageDependencyResolver`, or a shared extraction if it does not risk changing existing package-matching behavior.
- [x] 3.2 Implement `Execution/ArchitectureAnalysisSession.FrameworkReference.cs`: `CheckFrameworkDependencyContract` and `CheckFrameworkAllowOnlyContract`, including `BuildFrameworkReferenceLookup()`, ignored-violations handling, and condition-aware occurrence identity.
- [x] 3.3 Add `Contracts/Validators/FrameworkReferenceValidator.cs` and `FrameworkReferenceAllowOnlyValidator.cs` (source-must-resolve-to-target-assembly at load time); register both in `ArchitecturePolicyDocumentValidatorPipeline.cs`.
- [x] 3.4 Register `framework_dependency` and `framework_allow_only` descriptors in `ArchitectureContractFamilyRegistry.cs`, including `ConfigurationContributor` wiring for unknown/unusable framework-group and missing-project-metadata checks (mirroring the package descriptors).

## 4. Diagnostics and rendering

- [x] 4.1 Add `FrameworkReference`/`FrameworkReferenceAllowOnly` members to `ArchitectureDiagnosticKind.cs`.
- [x] 4.2 Add `Model/FrameworkReferenceDiagnostic.cs` + `FrameworkReferencePayload.cs`, and `Model/FrameworkReferenceAllowOnlyDiagnostic.cs` + `FrameworkReferenceAllowOnlyPayload.cs`, mirroring the package diagnostic/payload pairs.
- [x] 4.3 Add `case FrameworkReferenceDiagnostic`/`case FrameworkReferenceAllowOnlyDiagnostic` arms in `ArchitectureDiagnosticFormatter.cs` (all switch sites already handling package diagnostics).
- [x] 4.4 Add framework-diagnostic category/logical-location mapping in `ArchitectureSarifFormatter.cs`.
- [x] 4.5 Verify `ArchLinterNet.Testing`'s `ArchitectureValidationResult.cs` surfaces the new diagnostic kinds without special-casing (confirm generically, add handling only if actually required).

## 5. Baseline identity

- [x] 5.1 Add `"framework_dependency" or "framework_allow_only" => "package"` (or a dedicated identity-kind bucket, per design.md decision) to `ArchitectureViolationIdentity.ResolveKind`.
- [x] 5.2 Add `StrictFrameworkDependency`/`AuditFrameworkDependency`/`StrictFrameworkAllowOnly`/`AuditFrameworkAllowOnly` properties and switch arms to `ArchitectureBaselineModels.cs`.
- [x] 5.3 Add framework contract-group-name → `contract.Id` selectors to `ArchitectureBaselineComparer.cs`.
- [x] 5.4 Confirm `ArchitectureBaselineLoadingService.cs` requires no framework-specific changes (generic dispatch); add handling only if it does.

## 6. Tests and fixtures

- [x] 6.1 Add `FrameworkReferenceContractTests.cs` and `FrameworkReferenceAllowOnlyContractTests.cs` mirroring the package contract test structure (in-memory discovered projects with `FrameworkReferences`).
- [x] 6.2 Add `FrameworkReferenceConfigurationTests.cs` (unknown/unusable group, missing project metadata) mirroring `PackageDependencyConfigurationTests.cs`.
- [x] 6.3 Add `FrameworkReferenceValidationTests.cs` mirroring `PackageDependencyValidationTests.cs` (unresolved-source rejection at load time).
- [x] 6.4 Add `ArchitectureProjectFileParser`/discovery tests covering `FrameworkReference` `Include`/`Condition` parsing on real on-disk `.csproj` fixtures (single-project and multi-project, including a conditional-reference case).
- [x] 6.5 Extend `ArchitectureDiagnosticFormatterTests` and `ArchitectureSarifFormatterTests` with framework-reference diagnostic cases.
- [x] 6.6 Update the cross-cutting "every family" tests: `AllContractFamiliesYamlRoundTripTests.cs`, `ArchitectureContractCatalogTests.cs`, `ArchitectureContractFamilyBindingsTests.cs`, `ArchitectureContractFamilyRegistryTests.cs`, `ArchitectureContractHandlerRegistryTests.cs`, `ArchitecturePolicyProvenanceTests.cs` to include the two new families.
- [x] 6.7 Add a baseline identity/migration test proving project/condition occurrence distinctness for framework references.

## 7. Docs and capability manifest

- [x] 7.1 Add `docs/contracts/framework-references.md`, mirroring `docs/contracts/package-dependencies.md` (dependency + allow-only, YAML examples, semantics, non-goals).
- [x] 7.2 Add `docs/policy-format/framework-references.md`, mirroring `docs/policy-format/package-dependencies.md`.
- [x] 7.3 Update `docs/ai/capabilities.md` to list the new framework-reference contract families.
- [x] 7.4 Update `docs/ai/policy-authoring-guide.md` with framework-reference authoring guidance (global-key namespacing, package-facing-metadata scope boundary language).
- [x] 7.5 Update `docs/reference/yaml-schema.md` with the new schema sections.

## 8. Self-architecture and validation

- [x] 8.1 Run `rtk make fmt` and inspect formatting changes.
- [x] 8.2 Run `rtk make acceptance` (lint-code-size, lint-dotnet-format, lint-architecture, all tests) and fix issue-related failures.
- [x] 8.3 Run `openspec validate --all` after spec synchronization.
