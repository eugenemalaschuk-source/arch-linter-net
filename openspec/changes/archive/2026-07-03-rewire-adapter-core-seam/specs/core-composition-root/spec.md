## MODIFIED Requirements

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
