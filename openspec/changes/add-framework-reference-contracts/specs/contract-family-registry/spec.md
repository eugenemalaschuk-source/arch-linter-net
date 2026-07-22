## MODIFIED Requirements

### Requirement: Contract family catalog metadata is defined by an ordered descriptor registry
`ArchLinterNet.Core.Execution.ArchitectureContractFamilyRegistry` SHALL expose an ordered `IReadOnlyList<ArchitectureContractFamilyDescriptor>` (`All`) containing exactly one descriptor per contract family known to `ArchitectureContractCatalog`, including the `framework_dependency` and `framework_allow_only` families. Each descriptor SHALL carry: a family id, the strict YAML group name, the audit YAML group name, a baseline-capability flag, an accessor that extracts that family's strict contracts from an `ArchitectureContractGroups` instance, an accessor that extracts its audit contracts, and an informational list of the CLR types the family owns.

#### Scenario: Registry contains the framework_dependency and framework_allow_only families with no duplicates
- **WHEN** `ArchitectureContractFamilyRegistry.All` is enumerated
- **THEN** it SHALL contain exactly one descriptor with family id `framework_dependency` and exactly one descriptor with family id `framework_allow_only`
- **AND** no two descriptors SHALL share the same family id

#### Scenario: Registry order matches the historical executor dispatch order
- **WHEN** the family ids of `ArchitectureContractFamilyRegistry.All`, taken in list order, are compared to the order pinned by `ArchitectureContractCatalogTests.FamiliesInOrder_MatchesHistoricalExecutorDispatchOrder`
- **THEN** the two sequences SHALL be identical

### Requirement: Baseline capability is sourced from descriptor data
`ArchitectureContractCatalog.BaselineCapableGroups` and `ResolveGroup` SHALL determine whether a family is baseline-capable by looking up that family's `ArchitectureContractFamilyDescriptor.IsBaselineCapable` flag in the registry, rather than a hardcoded family-name exclusion list. The `framework_dependency` and `framework_allow_only` families SHALL be baseline-capable, consistent with `package_dependency` and `package_allow_only`.

#### Scenario: asmdef and layer_template remain excluded from baseline capability
- **WHEN** `ArchitectureContractCatalog.BaselineCapableGroups()` is called on a catalog built from a document containing `asmdef` and `layer_template` contracts
- **THEN** the groups associated with those two families SHALL NOT appear in the result, matching current behavior

#### Scenario: framework_dependency and framework_allow_only are baseline-capable
- **WHEN** `ArchitectureContractCatalog.BaselineCapableGroups()` is called on a catalog built from a document containing `framework_dependency` and `framework_allow_only` contracts
- **THEN** the groups associated with those two families SHALL appear in the result

### Requirement: Descriptor owns its family's checker and exposes an inert extension surface for future family decomposition
`ArchitectureContractFamilyDescriptor` SHALL expose a `Checker` property of type `ArchitectureContractChecker` (a delegate taking an `ArchitectureAnalysisSession` and an `IArchitectureContract`, returning an `ArchitectureHandlerResult`); `ArchitectureContractHandlerRegistry` SHALL read and invoke it for every family during contract execution, including `framework_dependency` and `framework_allow_only`. `ArchitectureContractFamilyDescriptor` SHALL additionally expose a `ConfigurationContributor` property of type `ArchitectureConfigurationContributor?` (a delegate taking an `ArchitectureAnalysisSession`, an `ArchitectureConfigurationReferenceCollector`, and an `IArchitectureContract`, returning `void`), defaulting to `null`; `ArchitectureAnalysisSession.CheckConfiguration` SHALL invoke it, when non-null, once per contract instance of that family for the mode (strict/audit) being checked, and the `framework_dependency`/`framework_allow_only` descriptors SHALL each have a non-null `ConfigurationContributor`. `ArchitectureContractFamilyDescriptor` SHALL further expose an `OwnedContractTypes` property (`IReadOnlyList<Type>`) and an `AdditionalValidation` property (`Action<ArchitectureContractDocument>?`, defaulting to `null`); unlike `Checker` and `ConfigurationContributor`, both of these remain inert and unread by `ArchitectureContractCatalog.Build` or any other production code path.

#### Scenario: AdditionalValidation is never invoked
- **WHEN** `ArchitectureContractCatalog.Build` processes any document, including one containing contracts for every family
- **THEN** no descriptor's `AdditionalValidation` delegate SHALL be invoked, and `ArchitecturePolicyDocumentLoader.Load`'s existing validation sequence SHALL be unchanged

#### Scenario: Every descriptor has a non-null Checker
- **WHEN** `ArchitectureContractFamilyRegistry.All` is enumerated
- **THEN** every descriptor's `Checker` property SHALL be non-null

#### Scenario: Checker is invoked during contract execution
- **WHEN** `ArchitectureContractHandlerRegistry.Execute(family, session, contract)` is called for a family present in `ArchitectureContractFamilyRegistry.All`
- **THEN** that family's descriptor's `Checker` delegate SHALL be invoked with the given `session` and `contract`, and its return value SHALL be returned unchanged

#### Scenario: framework_dependency and framework_allow_only carry a non-null ConfigurationContributor
- **WHEN** `ArchitectureContractFamilyRegistry.All` is enumerated
- **THEN** the descriptors for `framework_dependency` and `framework_allow_only` SHALL each have a non-null `ConfigurationContributor`

#### Scenario: ConfigurationContributor is invoked during configuration checking
- **WHEN** `ArchitectureAnalysisSession.CheckConfiguration(strict)` runs and the document contains contracts belonging to a family whose descriptor has a non-null `ConfigurationContributor`
- **THEN** that delegate SHALL be invoked once per contract instance returned by the descriptor's `StrictContracts`/`AuditContracts` accessor for the requested mode, with the session, a shared `ArchitectureConfigurationReferenceCollector`, and that contract instance
