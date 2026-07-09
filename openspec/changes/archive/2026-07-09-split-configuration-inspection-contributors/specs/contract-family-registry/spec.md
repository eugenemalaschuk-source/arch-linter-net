## MODIFIED Requirements

### Requirement: Descriptor owns its family's checker and exposes an inert extension surface for future family decomposition
`ArchitectureContractFamilyDescriptor` SHALL expose a `Checker` property of type `ArchitectureContractChecker` (a delegate taking an `ArchitectureAnalysisSession` and an `IArchitectureContract`, returning an `ArchitectureHandlerResult`); `ArchitectureContractHandlerRegistry` SHALL read and invoke it for every family during contract execution. `ArchitectureContractFamilyDescriptor` SHALL additionally expose a `ConfigurationContributor` property of type `ArchitectureConfigurationContributor?` (a delegate taking an `ArchitectureAnalysisSession`, an `ArchitectureConfigurationReferenceCollector`, and an `IArchitectureContract`, returning `void`), defaulting to `null`; `ArchitectureAnalysisSession.CheckConfiguration` SHALL invoke it, when non-null, once per contract instance of that family for the mode (strict/audit) being checked. `ArchitectureContractFamilyDescriptor` SHALL further expose an `OwnedContractTypes` property (`IReadOnlyList<Type>`) and an `AdditionalValidation` property (`Action<ArchitectureContractDocument>?`, defaulting to `null`); unlike `Checker` and `ConfigurationContributor`, both of these remain inert and unread by `ArchitectureContractCatalog.Build` or any other production code path.

#### Scenario: AdditionalValidation is never invoked
- **WHEN** `ArchitectureContractCatalog.Build` processes any document, including one containing contracts for every family
- **THEN** no descriptor's `AdditionalValidation` delegate SHALL be invoked, and `ArchitecturePolicyDocumentLoader.Load`'s existing validation sequence SHALL be unchanged

#### Scenario: Every descriptor has a non-null Checker
- **WHEN** `ArchitectureContractFamilyRegistry.All` is enumerated
- **THEN** every descriptor's `Checker` property SHALL be non-null

#### Scenario: Checker is invoked during contract execution
- **WHEN** `ArchitectureContractHandlerRegistry.Execute(family, session, contract)` is called for a family present in `ArchitectureContractFamilyRegistry.All`
- **THEN** that family's descriptor's `Checker` delegate SHALL be invoked with the given `session` and `contract`, and its return value SHALL be returned unchanged

#### Scenario: Sixteen descriptors carry a non-null ConfigurationContributor
- **WHEN** `ArchitectureContractFamilyRegistry.All` is enumerated
- **THEN** the descriptors for `dependency`, `layer`, `allow_only`, `cycle`, `method_body`, `independence`, `protected`, `external`, `external_allow_only`, `package_dependency`, `package_allow_only`, `project_metadata`, `type_placement`, `attribute_usage`, `inheritance`, and `interface_implementation` SHALL each have a non-null `ConfigurationContributor`
- **AND** every other family's descriptor (including `composition`) SHALL have `ConfigurationContributor` equal to `null`

#### Scenario: ConfigurationContributor is invoked during configuration checking
- **WHEN** `ArchitectureAnalysisSession.CheckConfiguration(strict)` runs and the document contains contracts belonging to a family whose descriptor has a non-null `ConfigurationContributor`
- **THEN** that delegate SHALL be invoked once per contract instance returned by the descriptor's `StrictContracts`/`AuditContracts` accessor for the requested mode, with the session, a shared `ArchitectureConfigurationReferenceCollector`, and that contract instance
