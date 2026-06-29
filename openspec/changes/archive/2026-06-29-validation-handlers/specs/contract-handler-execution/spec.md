## ADDED Requirements

### Requirement: Every contract family executes through an IArchitectureContractHandler
`ArchLinterNet.Core.Execution.ArchitectureContractExecutor` SHALL dispatch every contract family (`dependency`, `layer`, `layer_template`, `allow_only`, `cycle`, `acyclic_sibling`, `method_body`, `asmdef`, `independence`, `protected`, `external`, `coverage`) through `ArchitectureContractHandlerRegistry.Execute(family, runner, contract)`. It SHALL NOT call `ArchitectureContractRunner.CheckXxxContract` methods directly for any family.

#### Scenario: Previously-direct family routes through the registry
- **WHEN** `ArchitectureContractExecutor.Execute` processes a contract document containing an `allow_only`, `method_body`, `asmdef`, `independence`, `protected`, `external`, or `acyclic_sibling` contract
- **THEN** the violations or cycles produced SHALL be identical to what calling the corresponding `ArchitectureContractRunner.CheckXxxContract` method directly would have produced for the same contract and runner state

### Requirement: ArchitectureContractHandlerRegistry is an instance service populated from DI-registered handlers
`ArchLinterNet.Core.Execution.ArchitectureContractHandlerRegistry` SHALL be constructed from `IEnumerable<IArchitectureContractHandler>`, building its family-keyed lookup from the `Family` property of each supplied handler. It SHALL NOT expose or rely on a static `CreateDefault()` factory.

#### Scenario: Registry built from registered handlers exposes every family
- **WHEN** an `ArchitectureContractHandlerRegistry` is constructed from the full set of `IArchitectureContractHandler` implementations registered via `AddArchLinterNetCore()`
- **THEN** `TryGetHandler` SHALL succeed for `dependency`, `layer`, `layer_template`, `allow_only`, `cycle`, `acyclic_sibling`, `method_body`, `asmdef`, `independence`, `protected`, `external`, and `coverage`

#### Scenario: Unknown family still throws
- **WHEN** `ArchitectureContractHandlerRegistry.Execute` is called with a family string that has no registered handler
- **THEN** it SHALL throw `InvalidOperationException`, matching existing behavior

### Requirement: Contract handlers and the registry do not depend on IServiceProvider
No `IArchitectureContractHandler` implementation, and `ArchitectureContractHandlerRegistry` itself, SHALL take a constructor or method dependency on `IServiceProvider` or any other `Microsoft.Extensions.DependencyInjection` container type.

#### Scenario: Architecture policy enforces the boundary
- **WHEN** the self-architecture policy (`architecture/dependencies.arch.yml`) is evaluated against the Core assembly
- **THEN** it SHALL report a violation if any type under `ArchLinterNet.Core.Execution` references `Microsoft.Extensions.DependencyInjection`

### Requirement: Handler registrations are owned by the composition root
`ArchLinterNet.Core.Composition.ServiceCollectionExtensions.AddArchLinterNetCore()` SHALL register each `IArchitectureContractHandler` implementation via `services.AddSingleton<IArchitectureContractHandler, TImplementation>()`, one registration per family, and SHALL register `ArchitectureContractHandlerRegistry` as a singleton resolved from those registrations.

#### Scenario: Adding a new family requires only a handler registration
- **WHEN** a new contract family's `IArchitectureContractHandler` implementation is added and registered via `services.AddSingleton<IArchitectureContractHandler, NewFamilyHandler>()`
- **THEN** `ArchitectureContractExecutor` requires no source changes to dispatch contracts of that family through the registry
