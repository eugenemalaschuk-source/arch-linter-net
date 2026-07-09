# contract-handler-execution Specification

## Purpose
Defines the contract-family checker execution model: every contract family executes through an `ArchitectureContractChecker` delegate owned by that family's `ArchitectureContractFamilyDescriptor`, the handler registry is a descriptor-populated instance service rather than a DI-populated one or a static default-handler factory, and neither checkers nor the registry depend on `IServiceProvider`.
## Requirements
### Requirement: Every contract family executes through a descriptor-owned checker
`ArchLinterNet.Core.Execution.ArchitectureContractExecutor` SHALL dispatch every contract family (`dependency`, `layer`, `layer_template`, `allow_only`, `cycle`, `acyclic_sibling`, `method_body`, `asmdef`, `independence`, `assembly_independence`, `assembly_dependency`, `assembly_allow_only`, `package_dependency`, `package_allow_only`, `project_metadata`, `protected`, `external`, `external_allow_only`, `type_placement`, `public_api_surface`, `attribute_usage`, `inheritance`, `interface_implementation`, `composition`, `coverage`) through `ArchitectureContractHandlerRegistry.Execute(family, session, contract)`, where `session` is the per-run `ArchitectureAnalysisSession`. The registry SHALL resolve the checker for each family from that family's `ArchitectureContractFamilyDescriptor.Checker` delegate (an `ArchitectureContractChecker`) rather than from a DI-registered `IArchitectureContractHandler` instance. `ArchitectureContractExecutor` SHALL NOT call `ArchitectureContractRunner.CheckXxxContract` methods directly for any family, and no checker SHALL receive an `ArchitectureContractRunner` instance.

#### Scenario: Previously-direct family routes through the registry
- **WHEN** `ArchitectureContractExecutor.Execute` processes a contract document containing an `allow_only`, `method_body`, `asmdef`, `independence`, `protected`, `external`, or `acyclic_sibling` contract
- **THEN** the violations or cycles produced SHALL be identical to what calling the corresponding `ArchitectureContractRunner.CheckXxxContract` method directly would have produced for the same contract and session state

#### Scenario: Checker execution receives the session, not the runner
- **WHEN** `ArchitectureContractHandlerRegistry.Execute` dispatches a contract to the family's resolved `ArchitectureContractChecker` delegate
- **THEN** the delegate SHALL receive the `ArchitectureAnalysisSession` for the current validation run as its context parameter, and SHALL NOT receive an `ArchitectureContractRunner`

### Requirement: ArchitectureContractHandlerRegistry is an instance service populated from the descriptor registry
`ArchLinterNet.Core.Execution.ArchitectureContractHandlerRegistry` SHALL be built by iterating `ArchLinterNet.Core.Execution.ArchitectureContractFamilyRegistry.All` and reading each descriptor's `Checker` delegate into a family-keyed lookup. It SHALL NOT be constructed from a DI-supplied `IEnumerable` of per-family handler implementations, and SHALL NOT expose or rely on a static `CreateDefault()` factory.

#### Scenario: Registry built from the descriptor registry exposes every family
- **WHEN** an `ArchitectureContractHandlerRegistry` is constructed
- **THEN** `TryGetHandler` SHALL succeed for every family id present in `ArchitectureContractFamilyRegistry.All`, including `layer_template`

#### Scenario: Unknown family still throws
- **WHEN** `ArchitectureContractHandlerRegistry.Execute` is called with a family string that has no descriptor in `ArchitectureContractFamilyRegistry.All`
- **THEN** it SHALL throw `InvalidOperationException`, matching existing behavior

### Requirement: Contract checkers and the registry do not depend on IServiceProvider
No `ArchitectureContractChecker` delegate assignment, no `ArchitectureContractFamilyDescriptor`, and `ArchitectureContractHandlerRegistry` itself, SHALL take a constructor or method dependency on `IServiceProvider` or any other `Microsoft.Extensions.DependencyInjection` container type.

#### Scenario: Architecture policy enforces the boundary
- **WHEN** the self-architecture policy (`architecture/dependencies.arch.yml`) is evaluated against the Core assembly
- **THEN** it SHALL report a violation if any type under `ArchLinterNet.Core.Execution` references `Microsoft.Extensions.DependencyInjection`

### Requirement: Checker resolution is owned by the descriptor registry, not per-family composition-root registrations
`ArchLinterNet.Core.Composition.ServiceCollectionExtensions.AddArchLinterNetCore()` SHALL NOT register a separate checker-shaped service per contract family. It SHALL register `ArchitectureContractHandlerRegistry` as a singleton built from `ArchitectureContractFamilyRegistry.All`, with no per-family registration lines and no per-family constructor arguments.

#### Scenario: Adding a new family requires only a descriptor entry
- **WHEN** a new contract family's descriptor (including its `Checker` delegate) is appended to `ArchitectureContractFamilyRegistry.All`
- **THEN** `ArchitectureContractExecutor` and `ServiceCollectionExtensions.AddArchLinterNetCore()` require no source changes to dispatch contracts of that family through the registry

