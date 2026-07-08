## 1. Scaffolding

- [x] 1.1 Create `src/ArchLinterNet.Core/Contracts/Validators/` folder.
- [x] 1.2 Add `IArchitecturePolicyDocumentValidator` interface (`Validate(ArchitectureContractDocument document)`).
- [x] 1.3 Add `ArchitecturePolicyDocumentValidatorPipeline` (or similarly named) internal static class exposing the ordered `IReadOnlyList<IArchitecturePolicyDocumentValidator> All`.

## 2. Extract cross-family validators (kept centralized per issue guidance, but as pipeline entries)

- [x] 2.1 Extract `ValidateDuplicateIds` into `DuplicateIdValidator` — verbatim body, same message.
- [x] 2.2 Extract `ValidateLayerNamespaces` into `LayerNamespacesValidator` — verbatim body, same message.

## 3. Extract family-specific validators (verbatim method-body lifts, same exception types/messages)

- [x] 3.1 `AcyclicSiblingValidator` (from `ValidateAcyclicSiblingContracts`).
- [x] 3.2 `AssemblyIndependenceValidator` (from `ValidateAssemblyIndependenceContracts`).
- [x] 3.3 `AssemblyDependencyValidator` (from `ValidateAssemblyDependencyContracts`).
- [x] 3.4 `AssemblyAllowOnlyValidator` (from `ValidateAssemblyAllowOnlyContracts`).
- [x] 3.5 `PackageDependencyValidator` (from `ValidatePackageDependencyContracts`).
- [x] 3.6 `PackageAllowOnlyValidator` (from `ValidatePackageAllowOnlyContracts`).
- [x] 3.7 `ProjectMetadataValidator` (from `ValidateProjectMetadataContracts`).
- [x] 3.8 `TypePlacementValidator` (from `ValidateTypePlacementContracts`).
- [x] 3.9 `PublicApiSurfaceValidator` (from `ValidatePublicApiSurfaceContracts`).
- [x] 3.10 `AttributeUsageValidator` (from `ValidateAttributeUsageContracts`).
- [x] 3.11 `InheritanceValidator` (from `ValidateInheritanceContracts`).
- [x] 3.12 `InterfaceImplementationValidator` (from `ValidateInterfaceImplementationContracts`).
- [x] 3.13 `CompositionValidator` (from `ValidateCompositionContracts`).

## 4. Extract coverage family validator

- [x] 4.1 Create `CoverageValidator` wrapping `ValidateCoverageNamespaces` (dispatcher + inline `namespace`-scope checks), `ValidateRuleInputCoverageContract`, `ValidateDependencyEdgeCoverageContract`, `ValidateProjectOrAssemblyCoverageContract`, and `ValidateImplementedCoverageScopes`, preserving their internal call order.
- [x] 4.2 Move `CollectLayerBearingContractIds` and any other shared helpers `CoverageValidator` needs to an accessible location (stays internal to `Contracts`).
- [x] 4.3 Delete `ArchitecturePolicyDocumentLoader.CoverageValidation.cs` once its contents are fully absorbed into `CoverageValidator`.

## 5. Rewire the loader

- [x] 5.1 Populate `ArchitecturePolicyDocumentValidatorPipeline.All` in the exact order: `DuplicateIdValidator` → `AcyclicSiblingValidator` → `LayerNamespacesValidator` → `CoverageValidator` → `AssemblyIndependenceValidator` → `AssemblyDependencyValidator` → `AssemblyAllowOnlyValidator` → `PackageDependencyValidator` → `PackageAllowOnlyValidator` → `ProjectMetadataValidator` → `TypePlacementValidator` → `PublicApiSurfaceValidator` → `AttributeUsageValidator` → `InheritanceValidator` → `InterfaceImplementationValidator` → `CompositionValidator`.
- [x] 5.2 Rewrite `ArchitecturePolicyDocumentLoader.Load` to: deserialize → `AssignFallbackIds(document)` → `foreach (var validator in ArchitecturePolicyDocumentValidatorPipeline.All) validator.Validate(document);`.
- [x] 5.3 Remove the now-dead private validation methods and the `NormalizeToContractId`/`AssignFallbackIds`/`GetAllContracts`/`HasNonBlankEntry` helpers stay only if still referenced; delete anything unused.
- [x] 5.4 Confirm `ArchitecturePolicyDocumentLoader.cs` no longer contains any family-specific validation logic.

## 6. Tests

- [x] 6.1 Run the full existing test suite (`dotnet test`) and confirm all 16 affected test files pass unchanged (no message/assertion edits).
- [x] 6.2 Add/confirm at least one direct unit test per new validator class (may reuse existing loader-level tests if they already exercise the validator through `Load`) proving representative invalid configs still fail with equivalent diagnostics, per issue #210's acceptance criteria. (Confirmed: every extracted validator already has dedicated `Load`-level coverage in the existing per-family test files — verified by full green run.)
- [x] 6.3 Add a regression test asserting pipeline order (e.g. a document invalid in both `ValidateDuplicateIds` and an unrelated family fails with the duplicate-id message), mirroring the scenario in `specs/policy-document-validation-pipeline/spec.md`. (Added `ContractLoaderTests.LoadFromPath_DuplicateIdsAndUnrelatedFamilyInvalid_ThrowsDuplicateIdErrorFirst`.)

## 7. Docs & spec sync

- [x] 7.1 Update `docs/internal/core-architecture-blueprint.md` to document the new validator pipeline location/pattern and note it alongside the "adding a new contract family" checklist.
- [x] 7.2 Run `openspec validate --all` after archiving to confirm the new `policy-document-validation-pipeline` spec is well-formed. (75/75 specs passed.)

## 8. Validation gate

- [x] 8.1 Run `make fmt`. (No changes needed.)
- [x] 8.2 Run `make acceptance`; fix any failures. (928 Core tests, 112 CLI tests, 3 Unity tests, all green.)
