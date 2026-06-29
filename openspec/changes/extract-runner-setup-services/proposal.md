## Why

`ArchitectureRunnerFactory` (`src/ArchLinterNet.Core/Execution/ArchitectureRunnerFactory.cs`) is a static class whose `LoadDocument` and `BuildRunner` methods concentrate policy/baseline loading, repository-root resolution, condition-set resolution, project discovery, and assembly resolution into one procedural pipeline. Being static, none of these setup concerns can be substituted with a fake in a unit test, and the pipeline is the last major static orchestration step blocking issue #132's Core architecture-health goal (composition root and instance application services already landed in #134/#135).

## What Changes

- Add six thin instance wrapper services, each delegating to one existing static helper, registered in the composition root and constructor-injected rather than called statically:
  - `IArchitecturePolicyDocumentLoader` (`Contracts/`) — policy YAML load + implemented-coverage-scope validation
  - `IArchitectureBaselineLoadingService` (`Contracts/`) — baseline document load + merge
  - `IArchitectureRepositoryRootResolver` (`Resolution/`) — repository root resolution
  - `IConditionSetResolutionService` (`Contracts/`) — condition-set/preprocessor-symbol resolution
  - `IArchitectureProjectDiscoveryService` (`Discovery/`) — project discovery + merging discovered assemblies/source roots back into the document
  - `IArchitectureAssemblyResolutionService` (`Resolution/`) — assembly resolution, including the project-coverage-contract bypass/diagnostic-throw branching
- Add `IArchitectureRunnerSetupService` (`Execution/`) as an instance orchestrator, constructor-injected with the six services above, exposing `LoadDocument(...)` and `BuildRunner(...)` with the same signatures and behavior as today's static methods.
- **BREAKING (internal-only)**: delete the static `ArchitectureRunnerFactory` class. Verified via repository-wide search that it is called only by `ArchitectureValidationApplicationService`, `ArchitectureBaselineApplicationService`, and 3 test files in `tests/ArchLinterNet.Core.Tests/` — no CLI, public API, Testing adapter, or Unity call sites exist, so there is no externally observable breaking change.
- Register the six wrapper services and `IArchitectureRunnerSetupService` in `ServiceCollectionExtensions.AddArchLinterNetCore()`.
- Update `ArchitectureValidationApplicationService` and `ArchitectureBaselineApplicationService` to take `IArchitectureRunnerSetupService` via constructor injection instead of calling `ArchitectureRunnerFactory` statics.
- Update the affected test files to construct `ArchitectureRunnerSetupService` directly with real (production) wrapper instances.
- Add at least one new unit test that constructs `ArchitectureRunnerSetupService` with a fake `IArchitectureAssemblyResolutionService` (or `IArchitectureProjectDiscoveryService`), proving a setup dependency is replaceable without touching the file system or loading real assemblies.

The underlying static helpers (`ArchitectureContractLoader`, `ArchitectureBaselineLoader`, `ArchitectureBaselineMerger`, `ArchitectureRepositoryRootLocator`, `ConditionSetResolver`, `ArchitectureProjectDiscovery`, `ArchitectureAssemblyResolver`) are NOT converted or removed — they are used directly elsewhere (`ArchLinterNet.Unity.AsmdefValidator`, `LayerTemplateExpander`, and their own dedicated test suites) and stay exactly as they are.

## Capabilities

### New Capabilities
- `runner-setup-services`: the focused, instance, constructor-injected services that replace `ArchitectureRunnerFactory`'s static setup pipeline, and the seam they give for faking individual setup dependencies in tests.

### Modified Capabilities
- `shared-validation-service`: the "Shared setup and execution building blocks" requirement currently names the static `ArchitectureRunnerFactory.LoadDocument`/`BuildRunner` methods as the shared setup building block. It must be updated to name `IArchitectureRunnerSetupService` instead, since the static class is removed.

## Impact

- `src/ArchLinterNet.Core/Execution/ArchitectureRunnerFactory.cs` — deleted.
- `src/ArchLinterNet.Core/Execution/` — new `IArchitectureRunnerSetupService` + `ArchitectureRunnerSetupService`.
- `src/ArchLinterNet.Core/Contracts/`, `Resolution/`, `Discovery/` — new wrapper interfaces/classes (no changes to the existing static helpers in these folders).
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs` — new registrations.
- `src/ArchLinterNet.Core/Validation/ArchitectureValidationApplicationService.cs`, `ArchitectureBaselineApplicationService.cs` — constructor changes (no parameterless constructor; DI container resolves dependencies).
- `tests/ArchLinterNet.Core.Tests/ArchitectureRunnerFactoryDiscoveryTests.cs`, `CoverageContractReservedTests.cs`, `ArchitectureCoverageInventoryTests.cs` — updated to use `ArchitectureRunnerSetupService` directly.
- No change to YAML policy behavior, discovery semantics, CLI commands, public API, or Testing adapter surface.
