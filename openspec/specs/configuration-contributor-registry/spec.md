# configuration-contributor-registry Specification

## Purpose
TBD - created by archiving change split-configuration-inspection-contributors. Update Purpose after archive.
## Requirements
### Requirement: Configuration reference contribution is per-family, not centralized
Each contract family that references a configuration-owned concept (layer names, external dependency groups, package groups, project paths) SHALL report those references through its own `ArchitectureContractFamilyDescriptor.ConfigurationContributor` delegate, rather than through hand-written per-family logic inside `ArchitectureAnalysisSession.CheckConfiguration`. Adding configuration-reference reporting for a new or existing family SHALL require only setting that family's descriptor's `ConfigurationContributor` in `ArchitectureContractFamilyRegistry`, with no edit to `ArchitectureAnalysisSession.CheckConfiguration` itself.

#### Scenario: New family's configuration references require no session edit
- **WHEN** a contract family's descriptor is given a non-null `ConfigurationContributor` that calls `collector.AddLayerNames`/`AddExternalGroupNames`/`AddPackageGroupNames`
- **THEN** `ArchitectureAnalysisSession.CheckConfiguration` SHALL surface configuration violations for that family's invalid references without any change to `ArchitectureAnalysisSession.cs`

### Requirement: ArchitectureConfigurationReferenceCollector aggregates contributions across families
`ArchitectureConfigurationReferenceCollector` SHALL expose `AddLayerNames(string? contractId, IEnumerable<string> names)`, `AddExternalGroupNames(IEnumerable<string> names)`, `AddPackageGroupNames(IEnumerable<string> names)`, `AddPackageContractSource(string contractName, string? contractId, string source)`, and `AddProjectMetadataProject(string contractName, string? contractId, string projectPath)`, and SHALL expose read-only views of everything added through those methods for `CheckConfiguration`'s validation logic to consume after all contributors for a mode have run.

#### Scenario: Multiple families contributing the same layer name are merged
- **WHEN** two different contract families' contributors each call `AddLayerNames` with the same layer name but different contract IDs
- **THEN** the collector's read-only view SHALL associate that layer name with the union of all contract IDs that referenced it, matching the pre-refactor behavior of `layerReferencingContractIds`

### Requirement: Configuration checking behavior is unchanged for currently-covered families
For every family whose descriptor has a non-null `ConfigurationContributor` (`dependency`, `layer`, `allow_only`, `cycle`, `method_body`, `independence`, `protected`, `external`, `external_allow_only`, `package_dependency`, `package_allow_only`, `project_metadata`, `type_placement`, `attribute_usage`, `inheritance`, `interface_implementation`), `ArchitectureAnalysisSession.CheckConfiguration(strict)` SHALL produce the same `ArchitectureViolation` instances (same subject, kind, messages, and `ForbiddenExternalGroup`/`ForbiddenPackageGroup`/`ProjectMetadataKind` fields) as it did before this change, for the same input policy document and target assemblies, in both strict and audit modes, including the rule_input-coverage dangling-deferral interaction.

#### Scenario: Unknown layer reference still produces a configuration violation
- **WHEN** a contract from any of the sixteen contributing families references a layer name not declared in `layers`, and that reference is not fully owned by a `rule_input`-scope coverage contract
- **THEN** `CheckConfiguration` SHALL report an `"empty layer namespace"` (or resolution-failure) violation identical to pre-refactor behavior

#### Scenario: Unknown package or external dependency group reference still produces a configuration violation
- **WHEN** a `package_dependency`/`package_allow_only` contract references a package group not declared in `packages`, or an `external`/`external_allow_only` contract references an external dependency group not declared in `external_dependencies`
- **THEN** `CheckConfiguration` SHALL report an `"unknown package group"` or `"unknown external dependency group"` violation identical to pre-refactor behavior

#### Scenario: Families without a configuration contributor are unaffected
- **WHEN** the document contains contracts for `layer_template`, `assembly_dependency`, `assembly_allow_only`, `assembly_independence`, `public_api_surface`, `asmdef`, `acyclic_sibling`, `coverage`, or `composition`
- **THEN** `CheckConfiguration` SHALL NOT produce any configuration-reference violation for those contracts' fields, matching current behavior (including that `composition`'s `AllowedOnlyInLayers` remains unchecked)

