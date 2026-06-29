## Why

Core's production entry points (`ArchitectureValidationService.Validate`, `ArchitectureBaselineService.Generate`) are static methods with no composition seam, so callers cannot swap or intercept the application-service collaborators behind them without editing Core itself. Issue #134 asks for a lightweight composition root so collaborators become explicit and replaceable, without turning ArchLinterNet into a hosted application or multiplying hand-written factories.

## What Changes

- Add `IArchitectureValidationApplicationService` and `IArchitectureBaselineApplicationService` interfaces in `ArchLinterNet.Core.Validation`, with default implementations that contain the existing `ArchitectureValidationService.Validate` / `ArchitectureBaselineService.Generate` pipeline logic.
- Add an `ArchLinterNet.Core.Composition` namespace containing `ArchitectureEngine` and `ArchitectureEngineBuilder`. The builder wraps `IServiceCollection`, registers the default application services via an `AddArchLinterNetCore()` extension method, and builds the engine from an internal `ServiceProvider`. These two types are the only place `Microsoft.Extensions.DependencyInjection` container APIs (`IServiceCollection`, `ServiceProvider`) appear in Core.
- `ArchitectureEngine` exposes `Validate(ValidationRequest, ValidationTiming?)` and `GenerateBaseline(BaselineGenerationRequest)`, resolving the application services internally — consumers never see `IServiceProvider`.
- `ArchitectureValidationService.Validate` and `ArchitectureBaselineService.Generate` keep their existing static signatures and behavior, now delegating to a lazily-built default `ArchitectureEngine` instance, preserving them as compatibility facades. `ArchitectureValidator` and the Testing adapter need no changes since they already call the static services.
- Add a `Microsoft.Extensions.DependencyInjection` package reference to `ArchLinterNet.Core` only.

## Capabilities

### New Capabilities
- `core-composition-root`: the `ArchitectureEngine` / `ArchitectureEngineBuilder` composition seam, the application-service interfaces it wires, and the constraint that container APIs stay confined to the composition boundary.

### Modified Capabilities
- (none — `shared-validation-service` requirements about delegation and behavior are preserved as-is; the pipeline logic moves but its observable contract does not change)

## Impact

- Affected code: `src/ArchLinterNet.Core/Validation/ArchitectureValidationService.cs`, `src/ArchLinterNet.Core/Validation/ArchitectureBaselineService.cs`, new `src/ArchLinterNet.Core/Composition/` directory.
- New dependency: `Microsoft.Extensions.DependencyInjection` in `ArchLinterNet.Core.csproj`.
- No changes to CLI, Testing, or Unity packages, and no changes to YAML or CLI behavior.
- New tests under `tests/ArchLinterNet.Core.Tests/` covering engine construction and resolution.
