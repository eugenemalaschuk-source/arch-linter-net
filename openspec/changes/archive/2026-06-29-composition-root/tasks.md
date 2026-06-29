## 1. Dependency setup

- [x] 1.1 Add `Microsoft.Extensions.DependencyInjection` package reference to `src/ArchLinterNet.Core/ArchLinterNet.Core.csproj`.

## 2. Application service interfaces

- [x] 2.1 Add `IArchitectureValidationApplicationService` (method: `Validate(ValidationRequest, ValidationTiming?)`) to `src/ArchLinterNet.Core/Validation/`.
- [x] 2.2 Add `IArchitectureBaselineApplicationService` (method: `Generate(BaselineGenerationRequest)`) to `src/ArchLinterNet.Core/Validation/`.
- [x] 2.3 Move the existing body of `ArchitectureValidationService.Validate` into a new `ArchitectureValidationApplicationService : IArchitectureValidationApplicationService`.
- [x] 2.4 Move the existing body of `ArchitectureBaselineService.Generate` into a new `ArchitectureBaselineApplicationService : IArchitectureBaselineApplicationService`.

## 3. Composition root

- [x] 3.1 Create `src/ArchLinterNet.Core/Composition/ArchitectureEngine.cs` exposing `Validate(ValidationRequest, ValidationTiming?)` and `GenerateBaseline(BaselineGenerationRequest)`, resolving the two application-service interfaces from an internal `IServiceProvider` it owns.
- [x] 3.2 Create `src/ArchLinterNet.Core/Composition/ArchitectureEngineBuilder.cs` wrapping an `IServiceCollection`, with a `Build()` method returning `ArchitectureEngine`.
- [x] 3.3 Create `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs` with `AddArchLinterNetCore(this IServiceCollection)` registering both application services as singletons.

## 4. Compatibility facades

- [x] 4.1 Update `ArchitectureValidationService.Validate` to delegate to a lazily-built default `ArchitectureEngine` instead of running the pipeline inline.
- [x] 4.2 Update `ArchitectureBaselineService.Generate` to delegate to the same lazily-built default `ArchitectureEngine`.
- [x] 4.3 Verify `ArchitectureValidator` and `ArchitectureValidationBuilder` (Testing adapter) require no source changes.

## 5. Architecture governance

- [x] 5.1 Add a contract to `architecture/dependencies.arch.yml` restricting `Microsoft.Extensions.DependencyInjection` imports to `ArchLinterNet.Core.Composition`.
- [x] 5.2 Run `rtk make lint-architecture` and resolve any new findings.

## 6. Tests

- [x] 6.1 Add tests in `tests/ArchLinterNet.Core.Tests/` building an `ArchitectureEngine` via `ArchitectureEngineBuilder` and asserting it resolves both application services.
- [x] 6.2 Add a test asserting `ArchitectureEngine.Validate` returns an outcome equal to calling `ArchitectureValidationService.Validate` directly for the same request/policy fixture.
- [x] 6.3 Add a test asserting `ArchitectureEngine.GenerateBaseline` returns an outcome equal to calling `ArchitectureBaselineService.Generate` directly for the same request/policy fixture.
- [x] 6.4 Confirm existing `ArchitectureValidationService`/`ArchitectureBaselineService`/`ArchitectureValidator` tests still pass unmodified.

## 7. Validation

- [x] 7.1 Run `rtk make fmt`.
- [x] 7.2 Run `rtk make acceptance` (this repo has no Taskfile/`task` CLI; `make acceptance` is the documented equivalent per AGENTS.md) and fix failures.
