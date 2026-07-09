# Contract Family Registry Specification

## Purpose
Defines, per architecture contract family, its YAML group names, dispatch order, baseline capability, and contract accessors as an ordered descriptor registry — the extension point `ArchitectureContractCatalog` builds from instead of hand-written per-family wiring.
## Requirements
### Requirement: Contract family catalog metadata is defined by an ordered descriptor registry
`ArchLinterNet.Core.Execution.ArchitectureContractFamilyRegistry` SHALL expose an ordered `IReadOnlyList<ArchitectureContractFamilyDescriptor>` (`All`) containing exactly one descriptor per contract family known to `ArchitectureContractCatalog`. Each descriptor SHALL carry: a family id, the strict YAML group name, the audit YAML group name, a baseline-capability flag, an accessor that extracts that family's strict contracts from an `ArchitectureContractGroups` instance, an accessor that extracts its audit contracts, and an informational list of the CLR types the family owns.

#### Scenario: Registry contains all 25 known families with no duplicates
- **WHEN** `ArchitectureContractFamilyRegistry.All` is enumerated
- **THEN** it SHALL contain exactly 25 descriptors, one per family (`dependency`, `layer`, `layer_template`, `allow_only`, `cycle`, `method_body`, `asmdef`, `independence`, `assembly_independence`, `assembly_dependency`, `assembly_allow_only`, `package_dependency`, `package_allow_only`, `project_metadata`, `protected`, `external`, `external_allow_only`, `acyclic_sibling`, `type_placement`, `public_api_surface`, `attribute_usage`, `inheritance`, `interface_implementation`, `composition`, `coverage`)
- **AND** no two descriptors SHALL share the same family id

#### Scenario: Registry order matches the historical executor dispatch order
- **WHEN** the family ids of `ArchitectureContractFamilyRegistry.All`, taken in list order, are compared to the order pinned by `ArchitectureContractCatalogTests.FamiliesInOrder_MatchesHistoricalExecutorDispatchOrder`
- **THEN** the two sequences SHALL be identical

### Requirement: ArchitectureContractCatalog.Build is driven by the descriptor registry
`ArchitectureContractCatalog.Build` SHALL construct its descriptors and family order by iterating `ArchitectureContractFamilyRegistry.All` and, for each descriptor, invoking its strict and audit contract accessors against the input document's `Contracts` groups, instead of a fixed sequence of hand-written per-family calls.

#### Scenario: Catalog output is unchanged for an existing policy document
- **WHEN** `ArchitectureContractCatalog.Build` is called with an `ArchitectureContractDocument` containing contracts across multiple families, including `layer_template`
- **THEN** `FamiliesInOrder`, `ContractsFor(mode, family)`, `ContractsFor(mode)`, `AvailableContractIds(mode)`, and `ResolveGroup(contract)` SHALL return results identical to those produced before this change, for the same input

#### Scenario: layer_template contracts are still expanded before cataloguing
- **WHEN** the `layer_template` descriptor's strict or audit contract accessor is invoked against a document's `ArchitectureContractGroups`
- **THEN** it SHALL return the result of `LayerTemplateExpander.Expand` applied to the corresponding `StrictLayerTemplates`/`AuditLayerTemplates` list, not the raw `ArchitectureLayerTemplateContract` list

### Requirement: Baseline capability is sourced from descriptor data
`ArchitectureContractCatalog.BaselineCapableGroups` and `ResolveGroup` SHALL determine whether a family is baseline-capable by looking up that family's `ArchitectureContractFamilyDescriptor.IsBaselineCapable` flag in the registry, rather than a hardcoded family-name exclusion list.

#### Scenario: asmdef and layer_template remain excluded from baseline capability
- **WHEN** `ArchitectureContractCatalog.BaselineCapableGroups()` is called on a catalog built from a document containing `asmdef` and `layer_template` contracts
- **THEN** the groups associated with those two families SHALL NOT appear in the result, matching current behavior

#### Scenario: ResolveGroup still excludes non-baseline-capable families
- **WHEN** `ArchitectureContractCatalog.ResolveGroup` is called with an `ArchitectureAsmdefContract` instance from the catalog's source document
- **THEN** it SHALL return `null`, matching current behavior

### Requirement: Descriptor exposes an inert extension surface for future family decomposition
`ArchitectureContractFamilyDescriptor` SHALL expose an `OwnedContractTypes` property (`IReadOnlyList<Type>`) and an `AdditionalValidation` property (`Action<ArchitectureContractDocument>?`, defaulting to `null`), both still inert and unread by `ArchitectureContractCatalog.Build` or any other production code path. `ArchitectureContractFamilyDescriptor` SHALL additionally expose a `Checker` property of type `ArchitectureContractChecker` (a delegate taking an `ArchitectureAnalysisSession` and an `IArchitectureContract`, returning an `ArchitectureHandlerResult`). Unlike `OwnedContractTypes` and `AdditionalValidation`, `Checker` is not inert: `ArchitectureContractHandlerRegistry` reads and invokes it for every family during contract execution.

#### Scenario: AdditionalValidation is never invoked
- **WHEN** `ArchitectureContractCatalog.Build` processes any document, including one containing contracts for every family
- **THEN** no descriptor's `AdditionalValidation` delegate SHALL be invoked, and `ArchitecturePolicyDocumentLoader.Load`'s existing validation sequence SHALL be unchanged

#### Scenario: Every descriptor has a non-null Checker
- **WHEN** `ArchitectureContractFamilyRegistry.All` is enumerated
- **THEN** every descriptor's `Checker` property SHALL be non-null

#### Scenario: Checker is invoked during contract execution
- **WHEN** `ArchitectureContractHandlerRegistry.Execute(family, session, contract)` is called for a family present in `ArchitectureContractFamilyRegistry.All`
- **THEN** that family's descriptor's `Checker` delegate SHALL be invoked with the given `session` and `contract`, and its return value SHALL be returned unchanged

