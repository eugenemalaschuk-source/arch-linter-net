# core-composition-root Specification

## Purpose
Defines the Core composition root (`ArchitectureEngine` / `ArchitectureEngineBuilder`) that wires the validation and baseline application services through `Microsoft.Extensions.DependencyInjection`, confines container APIs to the composition boundary, and preserves the existing static entry points as compatibility facades.
## Requirements
### Requirement: ArchitectureEngineBuilder composes the default application services
`ArchLinterNet.Core.Composition.ArchitectureEngineBuilder` SHALL register the default `IArchitectureValidationApplicationService` and `IArchitectureBaselineApplicationService` implementations via an `AddArchLinterNetCore()` extension method on `IServiceCollection`, and SHALL build an `ArchitectureEngine` from the resulting service provider via a `Build()` method.

#### Scenario: Building an engine with default registrations
- **WHEN** `new ArchitectureEngineBuilder().AddArchLinterNetCore().Build()` is called
- **THEN** the returned `ArchitectureEngine` SHALL be able to resolve and invoke both the validation and baseline application services

### Requirement: ArchitectureEngine resolves application services without exposing the container
`ArchLinterNet.Core.Composition.ArchitectureEngine` SHALL expose `Validate(ValidationRequest, ValidationTiming?)`, `GenerateBaseline(BaselineGenerationRequest)`, and `ValidateAsmdef(AsmdefValidationRequest)` methods that resolve `IArchitectureValidationApplicationService`/`IArchitectureBaselineApplicationService`/`IAsmdefValidationService` internally and invoke them. `ArchitectureEngine` SHALL NOT expose a generic service-resolution method (e.g. `GetService<T>`) and SHALL NOT expose its underlying `IServiceProvider`.

#### Scenario: Validate produces the same outcome as the legacy static service
- **WHEN** an `ArchitectureEngine` built via `ArchitectureEngineBuilder` validates a `ValidationRequest` against a known policy
- **THEN** the returned `ValidationOutcome` SHALL equal what `ArchitectureValidationService.Validate` returns for the same request

#### Scenario: GenerateBaseline produces the same outcome as the legacy static service
- **WHEN** an `ArchitectureEngine` built via `ArchitectureEngineBuilder` generates a baseline for a `BaselineGenerationRequest`
- **THEN** the returned `BaselineGenerationOutcome` SHALL equal what `ArchitectureBaselineService.Generate` returns for the same request

#### Scenario: ValidateAsmdef resolves and invokes the asmdef application service
- **WHEN** an `ArchitectureEngine` built via `ArchitectureEngineBuilder` calls `ValidateAsmdef` with an `AsmdefValidationRequest`
- **THEN** the returned `AsmdefValidationOutcome` SHALL equal what a directly-constructed `AsmdefValidationService` (given the same collaborators) returns for the same request

### Requirement: Container APIs are confined to the composition boundary
Only types under `ArchLinterNet.Core.Composition` SHALL reference `Microsoft.Extensions.DependencyInjection` container types (`IServiceCollection`, `IServiceProvider`, `ServiceProvider`, `ServiceCollection`). No type under `ArchLinterNet.Core.Execution`, `ArchLinterNet.Core.Scanning`, `ArchLinterNet.Core.Resolution`, `ArchLinterNet.Core.Discovery`, or `ArchLinterNet.Core.Contracts` SHALL take a constructor or method dependency on `IServiceProvider` or any other container type.

#### Scenario: Architecture policy enforces the boundary
- **WHEN** the self-architecture policy (`architecture/dependencies.arch.yml`) is evaluated against the Core assembly
- **THEN** it SHALL report a violation if any namespace outside `ArchLinterNet.Core.Composition` references `Microsoft.Extensions.DependencyInjection`

### Requirement: Static facades preserve existing public entry points
`ArchLinterNet.Core.Validation.ArchitectureValidationService.Validate` and `ArchLinterNet.Core.Validation.ArchitectureBaselineService.Generate` SHALL keep their existing static signatures and SHALL delegate to a lazily-constructed default `ArchitectureEngine`, so any external caller of these static entry points requires no source changes. The CLI, `ArchitectureValidator`, and the Testing adapter each build and call their own composed `ArchitectureEngine` directly rather than the static facades, but observable behavior SHALL remain identical for all callers.

#### Scenario: Existing static call sites keep working unmodified
- **WHEN** `ArchitectureValidationService.Validate(request)` is called exactly as before this change
- **THEN** it SHALL return the same `ValidationOutcome` it returned prior to introducing the composition root

