## MODIFIED Requirements

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
