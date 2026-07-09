## 1. Payload abstraction

- [x] 1.1 Add `IArchitectureDiagnosticPayload` interface (`ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation)`) in `src/ArchLinterNet.Core/Model/IArchitectureDiagnosticPayload.cs`
- [x] 1.2 Add `MatchedNamespacePrefixes` and `Payload` properties to `ArchitectureViolation` (keep existing ~35 fields in place for now, additive only)

## 2. Payload types (one file per family in `src/ArchLinterNet.Core/Model/`)

- [x] 2.1 `DependencyPayload` (`SourceLayer`, `TargetLayer`, `AllowedImporters`) → `DependencyDiagnostic`
- [x] 2.2 `ConfigurationPayload` (`TemplateName`, `ContainerNamespace`, `DependencyPaths`) → `ConfigurationDiagnostic`
- [x] 2.3 `ExternalDependencyPayload` (`ForbiddenExternalGroup`) → `ExternalDependencyDiagnostic`
- [x] 2.4 `PackageDependencyPayload` (`ForbiddenPackageGroup`) → `PackageDependencyDiagnostic`
- [x] 2.5 `TypePlacementPayload` (`ExpectedTypeLocation`, `ActualTypeLocation`, `ExpectedTypeName`, `ActualTypeName`) → `TypePlacementDiagnostic`
- [x] 2.6 `PublicApiSurfacePayload` (`UndeclaredApiSignature`, `ForbiddenPublicConstant`, `ApiAssemblyName`, `ApiVisibility`) → `PublicApiSurfaceDiagnostic`
- [x] 2.7 `AttributeUsagePayload` (`MatchedAttribute`, `AttributeUsageKind`, `ExpectedAttributeLocation`, `ActualAttributeLocation`) → `AttributeUsageDiagnostic`
- [x] 2.8 `InheritancePayload` (`ForbiddenBaseType`, `InheritanceSourceSurface`) → `InheritanceDiagnostic`
- [x] 2.9 `InterfaceImplementationPayload` (`MatchedInterface`, `ImplementationKind`, `ExpectedImplementationLocation`, `ActualImplementationLocation`) → `InterfaceImplementationDiagnostic`
- [x] 2.10 `CompositionPayload` (`SourceMember`, `MatchedForbiddenApi`, `ExpectedCompositionBoundary`) → `CompositionDiagnostic`
- [x] 2.11 `ProjectMetadataPayload` (`ProjectMetadataKind`, `ProjectMetadataKey`, `ProjectMetadataExpectedValue`, `ProjectMetadataActualValue`, `ProjectMetadataSourcePath`) → `ProjectMetadataDiagnostic`

## 3. Mapper dispatch

- [x] 3.1 Update `ArchitectureDiagnosticMapper.FromViolation` to try `violation.Payload?.ToDiagnostic(violation)` first
- [x] 3.2 Keep the existing if-chain as a fallback for violations still using the old fields, guarded so it only runs when `Payload` is null (transitional safety net for this same change)
- [x] 3.3 Run `ArchitectureDiagnosticMapperTests.cs` to confirm no regression before touching call sites

## 4. Migrate call sites family by family (verify tests after each)

- [x] 4.1 Dependency layer fields: `ArchitectureAnalysisSession.cs`, `ArchitectureNamespaceViolationFinder.cs` → construct `DependencyPayload`
- [x] 4.2 Configuration: `ArchitectureAnalysisSession.Checking.cs`, `LayerTemplateExpander.cs` → construct `ConfigurationPayload`
- [x] 4.3 ExternalDependency: `ArchitectureExternalDependencyViolationFinder.cs`, `ArchitectureExternalDependencyIlScanner.cs` → construct `ExternalDependencyPayload`
- [x] 4.4 PackageDependency: `ArchitectureAnalysisSession.PackageDependency.cs` → construct `PackageDependencyPayload`
- [x] 4.5 TypePlacement: `ArchitectureAnalysisSession.TypePlacement.cs` → construct `TypePlacementPayload`
- [x] 4.6 PublicApiSurface: `Execution/Checkers/PublicApiSurfaceChecker.cs` → construct `PublicApiSurfacePayload`
- [x] 4.7 AttributeUsage: `ArchitectureAnalysisSession.AttributeUsage.cs` → construct `AttributeUsagePayload`
- [x] 4.8 Inheritance: `Execution/Checkers/InheritanceChecker.cs` → construct `InheritancePayload`
- [x] 4.9 InterfaceImplementation: `ArchitectureAnalysisSession.InterfaceImplementation.cs` → construct `InterfaceImplementationPayload`
- [x] 4.10 Composition: `ArchitectureAnalysisSession.Composition.cs` → construct `CompositionPayload`
- [x] 4.11 ProjectMetadata: `ArchitectureAnalysisSession.ProjectMetadata.cs` → construct `ProjectMetadataPayload`
- [x] 4.12 Grep `src/ArchLinterNet.Core` for the old field names to confirm zero remaining production call sites set them directly

## 5. Remove the transitional bag fields

- [x] 5.1 Delete the ~35 old family-specific nullable fields from `ArchitectureViolation`
- [x] 5.2 Delete the transitional if-chain fallback from `ArchitectureDiagnosticMapper.FromViolation`, leaving only the null-check + dispatch + bare `DependencyDiagnostic` fallback
- [x] 5.3 Fix compile errors in tests referencing the removed fields: `ArchitectureDiagnosticMapperTests.cs`, `ArchitectureSarifFormatterTests.cs`, `ProtectedContractTests.EdgeCases.cs`, `ArchitectureValidationApplicationServiceFakeCompositionTests.cs`, and any others the build surfaces

## 6. Regression coverage

- [x] 6.1 Add/extend tests proving `ArchitectureDiagnosticMapper.FromViolation` output is unchanged for at least: Configuration, ExternalDependency, TypePlacement, PublicApiSurface, Inheritance, Composition (representative spread across the family list)
- [x] 6.2 Confirm `ArchitectureSarifFormatterTests.cs` output is unchanged for the same representative families
- [x] 6.3 Confirm human-readable and CI JSON output (`ArchitectureDiagnosticFormatter`) is unchanged via existing formatter tests, updated only where they construct violations directly

## 7. Documentation

- [x] 7.1 Document the new family-addition recipe (payload type + construction call, no edits to `ArchitectureViolation` or the mapper) in `docs/internal/core-architecture-blueprint.md`

## 8. Spec sync and validation

- [x] 8.1 Run `openspec validate typed-diagnostic-payloads --strict` and fix any issues
- [x] 8.2 Run `make fmt`
- [x] 8.3 Run `make acceptance` and confirm green
