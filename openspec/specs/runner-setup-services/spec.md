# runner-setup-services Specification

## Purpose
Defines the focused, constructor-injected services that replace the static `ArchitectureRunnerFactory` setup pipeline (`IArchitecturePolicyDocumentLoader`, `IArchitectureBaselineLoadingService`, `IArchitectureRepositoryRootResolver`, `IConditionSetResolutionService`, `IArchitectureProjectDiscoveryService`, `IArchitectureAssemblyResolutionService`) and the `IArchitectureRunnerSetupService` orchestrator that composes them, so each setup concern is independently replaceable with a fake in unit tests while preserving the pipeline's existing observable behavior.
## Requirements
### Requirement: Runner setup is composed from focused, replaceable services
`ArchLinterNet.Core.Execution.Abstractions.IArchitectureRunnerSetupService` SHALL expose `LoadDocument(string policyPath, string? baselinePath, ValidationTiming? timing)` and `BuildRunner(ArchitectureContractDocument document, string policyPath, string? conditionSetName, IReadOnlyList<string>? preprocessorSymbols, HashSet<string>? selectedContractIds, bool enableUnmatchedIgnoreTracking, ValidationTiming? timing, string? mode)` with the same behavior as the static `ArchitectureRunnerFactory` it replaces. Its default implementation SHALL receive `IArchitecturePolicyDocumentLoader`, `IArchitectureBaselineLoadingService`, `IArchitectureRepositoryRootResolver`, `IConditionSetResolutionService`, `IArchitectureProjectDiscoveryService`, and `IArchitectureAssemblyResolutionService` via constructor injection rather than calling static helpers directly.

#### Scenario: LoadDocument behavior is preserved
- **WHEN** `IArchitectureRunnerSetupService.LoadDocument` is called with a policy path and an optional baseline path
- **THEN** it SHALL return the same `ArchitectureContractDocument` (policy loaded, implemented-coverage-scopes validated, and baseline merged when a baseline path is given) that the static `ArchitectureRunnerFactory.LoadDocument` returned for the same inputs

#### Scenario: BuildRunner behavior is preserved
- **WHEN** `IArchitectureRunnerSetupService.BuildRunner` is called with a loaded document and the same setup parameters previously accepted by `ArchitectureRunnerFactory.BuildRunner`
- **THEN** it SHALL return an equivalent `ArchitectureRunnerSetup` (same repository root, same resolved/missing assemblies, same diagnostics, same project-coverage bypass behavior), and SHALL throw the same `InvalidOperationException` in the same no-assemblies-resolved and condition-set-resolution-failure cases

### Requirement: Each setup dependency is independently replaceable
Each of `IArchitecturePolicyDocumentLoader`, `IArchitectureBaselineLoadingService`, `IArchitectureRepositoryRootResolver`, `IConditionSetResolutionService`, `IArchitectureProjectDiscoveryService`, and `IArchitectureAssemblyResolutionService` SHALL be a distinct interface that `ArchitectureRunnerSetupService` depends on only through its constructor, so any one of them can be substituted with a test fake without requiring a real file system, Roslyn compilation, or assembly load.

#### Scenario: Fake setup dependencies drive BuildRunner without touching the file system
- **WHEN** a unit test constructs `ArchitectureRunnerSetupService` with fake `IArchitectureRepositoryRootResolver`, `IArchitectureProjectDiscoveryService`, and `IArchitectureAssemblyResolutionService` implementations (each returning a fixed result without touching the file system, globbing for projects, or loading a real assembly)
- **THEN** `BuildRunner` SHALL use the fakes' results to construct the `ArchitectureAnalysisContext` and resulting `ArchitectureContractRunner`, without invoking the real `ArchitectureRepositoryRootLocator`, `ArchitectureProjectDiscovery`, or `ArchitectureAssemblyResolver`

### Requirement: Runner setup services are registered in the composition root
`ArchLinterNet.Core.Composition.ServiceCollectionExtensions.AddArchLinterNetCore()` SHALL register default implementations of `IArchitecturePolicyDocumentLoader`, `IArchitectureBaselineLoadingService`, `IArchitectureRepositoryRootResolver`, `IConditionSetResolutionService`, `IArchitectureProjectDiscoveryService`, `IArchitectureAssemblyResolutionService`, and `IArchitectureRunnerSetupService`, and `ArchitectureValidationApplicationService`/`ArchitectureBaselineApplicationService` SHALL receive `IArchitectureRunnerSetupService` via constructor injection instead of calling static methods.

#### Scenario: Application services resolve runner setup through the container
- **WHEN** an `ArchitectureEngine` is built via `ArchitectureEngineBuilder().AddArchLinterNetCore().Build()`
- **THEN** the resolved `IArchitectureValidationApplicationService` and `IArchitectureBaselineApplicationService` SHALL each use the container-resolved `IArchitectureRunnerSetupService` to perform setup, producing the same observable `ValidationOutcome`/`BaselineGenerationOutcome` as before this change

### Requirement: The static ArchitectureRunnerFactory is removed
`ArchLinterNet.Core.Execution.ArchitectureRunnerFactory` SHALL no longer exist as a static class. Its `LoadDocument` and `BuildRunner` behavior SHALL be fully covered by `IArchitectureRunnerSetupService`/`ArchitectureRunnerSetupService`.

#### Scenario: No remaining references to the static factory
- **WHEN** the Core codebase and its test suite are searched for `ArchitectureRunnerFactory`
- **THEN** no production or test code SHALL reference it

