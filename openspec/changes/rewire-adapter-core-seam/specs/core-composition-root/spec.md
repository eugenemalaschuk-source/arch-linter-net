## MODIFIED Requirements

### Requirement: ArchitectureEngineBuilder composes the default application services
`ArchLinterNet.Core.Composition.ArchitectureEngineBuilder` SHALL register the default `IArchitectureValidationApplicationService`, `IArchitectureBaselineApplicationService`, and `IAsmdefValidationService` implementations via an `AddArchLinterNetCore()` extension method on `IServiceCollection`, and SHALL build an `ArchitectureEngine` from the resulting service provider via a `Build()` method.

#### Scenario: Building an engine with default registrations
- **WHEN** `new ArchitectureEngineBuilder().AddArchLinterNetCore().Build()` is called
- **THEN** the returned `ArchitectureEngine` SHALL be able to resolve and invoke validation, baseline, and asmdef application services

### Requirement: ArchitectureEngine resolves application services without exposing the container
`ArchLinterNet.Core.Composition.ArchitectureEngine` SHALL expose `Validate(ValidationRequest, ValidationTiming?)`, `GenerateBaseline(BaselineGenerationRequest)`, and `ValidateAsmdef(AsmdefValidationRequest)` methods that resolve `IArchitectureValidationApplicationService`, `IArchitectureBaselineApplicationService`, and `IAsmdefValidationService` internally and invoke them. `ArchitectureEngine` SHALL NOT expose a generic service-resolution method (e.g. `GetService<T>`) and SHALL NOT expose its underlying `IServiceProvider`.

#### Scenario: Validate produces the same outcome as the legacy static service
- **WHEN** an `ArchitectureEngine` built via `ArchitectureEngineBuilder` validates a `ValidationRequest` against a known policy
- **THEN** the returned `ValidationOutcome` SHALL equal what `ArchitectureValidationService.Validate` returns for the same request

#### Scenario: GenerateBaseline produces the same outcome as the legacy static service
- **WHEN** an `ArchitectureEngine` built via `ArchitectureEngineBuilder` generates a baseline for a `BaselineGenerationRequest`
- **THEN** the returned `BaselineGenerationOutcome` SHALL equal what `ArchitectureBaselineService.Generate` returns for the same request

#### Scenario: ValidateAsmdef uses the composed asmdef service
- **WHEN** an `ArchitectureEngine` built via `ArchitectureEngineBuilder` validates an `AsmdefValidationRequest`
- **THEN** the returned `AsmdefValidationOutcome` SHALL be produced by the registered `IAsmdefValidationService`

### Requirement: Static facades preserve existing public entry points
`ArchLinterNet.Core.Validation.ArchitectureValidationService.Validate` and `ArchLinterNet.Core.Validation.ArchitectureBaselineService.Generate` SHALL keep their existing static signatures and SHALL delegate to a lazily-constructed default `ArchitectureEngine`, so existing external callers require no source changes. In-repository adapters SHOULD consume `ArchitectureEngine` directly when they need the composed Core seam.

#### Scenario: Existing static call sites keep working unmodified
- **WHEN** `ArchitectureValidationService.Validate(request)` is called exactly as before this change
- **THEN** it SHALL return the same `ValidationOutcome` it returned prior to introducing the composition root, with the same observable behavior
