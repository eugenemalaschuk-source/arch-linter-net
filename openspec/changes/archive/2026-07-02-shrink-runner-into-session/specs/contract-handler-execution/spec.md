## MODIFIED Requirements

### Requirement: Every contract family executes through an IArchitectureContractHandler
`ArchLinterNet.Core.Execution.ArchitectureContractExecutor` SHALL dispatch every contract family (`dependency`, `layer`, `layer_template`, `allow_only`, `cycle`, `acyclic_sibling`, `method_body`, `asmdef`, `independence`, `protected`, `external`, `coverage`) through `ArchitectureContractHandlerRegistry.Execute(family, session, contract)`, where `session` is the per-run `ArchitectureAnalysisSession`. It SHALL NOT call `ArchitectureContractRunner.CheckXxxContract` methods directly for any family, and no handler SHALL receive an `ArchitectureContractRunner` instance.

#### Scenario: Previously-direct family routes through the registry
- **WHEN** `ArchitectureContractExecutor.Execute` processes a contract document containing an `allow_only`, `method_body`, `asmdef`, `independence`, `protected`, `external`, or `acyclic_sibling` contract
- **THEN** the violations or cycles produced SHALL be identical to what calling the corresponding `ArchitectureContractRunner.CheckXxxContract` method directly would have produced for the same contract and session state

#### Scenario: Handler execution receives the session, not the runner
- **WHEN** `ArchitectureContractHandlerRegistry.Execute` dispatches a contract to its registered `IArchitectureContractHandler`
- **THEN** the handler's `Execute` method SHALL receive the `ArchitectureAnalysisSession` for the current validation run as its context parameter, and SHALL NOT receive an `ArchitectureContractRunner`
