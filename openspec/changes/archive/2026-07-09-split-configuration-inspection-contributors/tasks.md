## 1. Abstractions

- [x] 1.1 Add `ArchitectureConfigurationContributor` delegate to `src/ArchLinterNet.Core/Execution/Abstractions/` (`(ArchitectureAnalysisSession session, ArchitectureConfigurationReferenceCollector collector, IArchitectureContract contract) => void`)
- [x] 1.2 Add `ArchitectureConfigurationReferenceCollector` class exposing `AddLayerNames`, `AddExternalGroupNames`, `AddPackageGroupNames`, `AddPackageContractSource`, `AddProjectMetadataProject`, and read-only accessors matching the internal shapes `CheckConfiguration` uses today (dictionary of layer name -> contract-id set; two string sets; two tuple lists)

## 2. Descriptor and registry wiring

- [x] 2.1 Add nullable `ConfigurationContributor` property (`{ get; init; }`, default `null`) to `ArchitectureContractFamilyDescriptor`
- [x] 2.2 Promote `GetTypePlacementReferencedLayerNames`, `GetAttributeUsageReferencedLayerNames`, `GetInterfaceImplementationReferencedLayerNames` from `private static` to `internal static` in `ArchitectureAnalysisSession.PolicyConsistency.cs`
- [x] 2.3 In `ArchitectureContractFamilyRegistry.cs`, set `ConfigurationContributor` for: `dependency`, `layer`, `allow_only`, `cycle`, `method_body`, `independence`, `protected`, `external`, `external_allow_only`, `package_dependency`, `package_allow_only`, `project_metadata`, `type_placement`, `attribute_usage`, `inheritance`, `interface_implementation` — each lambda reproducing exactly the corresponding block currently in `CheckConfiguration`'s strict/audit loops (including the `IsContractSelected` guard for `package_dependency`/`package_allow_only`/`project_metadata`)
- [x] 2.4 Leave `ConfigurationContributor` unset (`null`) for `layer_template`, `assembly_dependency`, `assembly_allow_only`, `assembly_independence`, `public_api_surface`, `asmdef`, `acyclic_sibling`, `coverage`, `composition` — do not wire `composition` even though `GetReferencedLayerNames` already has a case for it (documented gap, see design.md §5)

## 3. Rewrite CheckConfiguration

- [x] 3.1 In `ArchitectureAnalysisSession.cs`, replace the local closures/mutable collections (lines ~265-302) with one `ArchitectureConfigurationReferenceCollector` instance
- [x] 3.2 Replace the duplicated strict/audit per-family blocks (lines ~304-511) with a single loop over `ArchitectureContractFamilyRegistry.All`, selecting `descriptor.StrictContracts(Document.Contracts)` or `descriptor.AuditContracts(Document.Contracts)` per the `strict` flag, invoking `descriptor.ConfigurationContributor?.Invoke(this, collector, contract)` for each contract
- [x] 3.3 Update the remaining validation logic (layer resolution/rule_input-coverage deferral, external group check, package group check, package/project metadata cross-checks) to read from the collector's accessors instead of the removed locals — no logic changes beyond the read source
- [x] 3.4 Leave the missing-assembly/discovery-diagnostic loop (lines ~241-263) untouched

## 4. Regression tests

- [x] 4.1 Confirm `ConfigurationCheckTests.cs`, `ConfigurationCheckByModeTests.cs`, `ProjectMetadataConfigurationTests.cs`, `RuleInputCoverageValidationTests.cs` all pass unmodified against the refactored `CheckConfiguration`
- [x] 4.2 Add or extend a test in `PackageDependencyConfigurationTests.cs` covering an unknown package group referenced by a `package_dependency` (or `package_allow_only`) contract, verifying identical violation shape post-refactor
- [x] 4.3 Add or extend a test in `ExternalAllowOnlyContractTests.cs` (or `ExternalDependencyContractTests.cs`) covering an unknown external dependency group, verifying identical violation shape post-refactor
- [x] 4.4 Add a direct unknown-layer regression test for one of `type_placement`/`attribute_usage`/`interface_implementation` (currently only indirectly covered via the shared extraction helpers) confirming the promoted `internal static` helpers still produce the "empty layer namespace" / rule_input-deferral behavior unchanged
- [x] 4.5 Add a `ArchitectureContractFamilyRegistryTests`-style test asserting exactly the 16 families listed in task 2.3 have non-null `ConfigurationContributor` and every other family (including `composition`) has `null`

## 5. Validation

- [x] 5.1 Run `make fmt`
- [x] 5.2 Run `make acceptance` and confirm green
- [x] 5.3 Run `openspec validate --all` after archiving
