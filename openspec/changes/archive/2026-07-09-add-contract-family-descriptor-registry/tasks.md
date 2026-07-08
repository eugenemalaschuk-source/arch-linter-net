## 1. Descriptor and registry

- [x] 1.1 Add `ArchitectureContractFamilyDescriptor.cs` in `src/ArchLinterNet.Core/Execution/`: sealed record with `FamilyId`, `StrictGroupName`, `AuditGroupName`, `IsBaselineCapable`, `StrictContracts` (`Func<ArchitectureContractGroups, IEnumerable<IArchitectureContract>>`), `AuditContracts` (same shape), `OwnedContractTypes` (`IReadOnlyList<Type>`, default empty), `AdditionalValidation` (`Action<ArchitectureContractDocument>?`, default null).
- [x] 1.2 Add `ArchitectureContractFamilyRegistry.cs` in `src/ArchLinterNet.Core/Execution/`: static class exposing `IReadOnlyList<ArchitectureContractFamilyDescriptor> All`, populated with exactly the 25 families in the same order as the current `AddGroup` calls in `ArchitectureContractCatalog.Build` (dependency, layer, layer_template, allow_only, cycle, method_body, asmdef, independence, assembly_independence, assembly_dependency, assembly_allow_only, package_dependency, package_allow_only, project_metadata, protected, external, external_allow_only, acyclic_sibling, type_placement, public_api_surface, attribute_usage, inheritance, interface_implementation, composition, coverage). Set `IsBaselineCapable = false` only for `asmdef` and `layer_template`. The `layer_template` descriptor's accessors wrap `LayerTemplateExpander.Expand(...)`.

## 2. Catalog refactor

- [x] 2.1 Refactor `ArchitectureContractCatalog.Build` to loop over `ArchitectureContractFamilyRegistry.All`, calling the existing `AddGroup` local function with each descriptor's `StrictGroupName`/`"strict"`/`FamilyId`/`StrictContracts(groups)` and `AuditGroupName`/`"audit"`/`FamilyId`/`AuditContracts(groups)`, removing the hand-written per-family `AddGroup` calls.
- [x] 2.2 Replace `IsGroupResolvable(string family)` with a lookup against `ArchitectureContractFamilyRegistry.All`'s `IsBaselineCapable` flag for the matching `FamilyId`.

## 3. Tests

- [x] 3.1 Run the existing `tests/ArchLinterNet.Core.Tests/ArchitectureContractCatalogTests.cs` suite unmodified and confirm all assertions still pass, including `FamiliesInOrder_MatchesHistoricalExecutorDispatchOrder`, `ResolveGroup_ExcludesAsmdefContracts`, and `ResolveGroup_ExcludesExpandedLayerTemplateContracts`.
- [x] 3.2 Add a new test (e.g. `ArchitectureContractFamilyRegistryTests.cs`) asserting `ArchitectureContractFamilyRegistry.All` has exactly 25 descriptors, no duplicate `FamilyId` values, and that the `FamilyId` sequence equals the historical order list.
- [x] 3.3 Add a test asserting the `layer_template` descriptor's strict/audit accessors, given a document with `StrictLayerTemplates`/`AuditLayerTemplates` entries, return the same result as calling `LayerTemplateExpander.Expand` directly.
- [x] 3.4 Add a test asserting no descriptor's `AdditionalValidation` is invoked during `ArchitectureContractCatalog.Build` (e.g. assert it remains `null` for every descriptor in this change, or use a sentinel that would throw/flag if invoked).

## 4. Validation

- [x] 4.1 Run `make fmt`.
- [x] 4.2 Run `make acceptance` and fix any failures.
