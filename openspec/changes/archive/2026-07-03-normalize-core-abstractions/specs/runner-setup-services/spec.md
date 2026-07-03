## MODIFIED Requirements

### Requirement: Runner setup is composed from focused, replaceable services
`ArchLinterNet.Core.Execution.Abstractions.IArchitectureRunnerSetupService` SHALL expose `LoadDocument(string policyPath, string? baselinePath, ValidationTiming? timing)` and `BuildRunner(ArchitectureContractDocument document, string policyPath, string? conditionSetName, IReadOnlyList<string>? preprocessorSymbols, HashSet<string>? selectedContractIds, bool enableUnmatchedIgnoreTracking, ValidationTiming? timing, string? mode)` with the same behavior as the static `ArchitectureRunnerFactory` it replaces. Its default implementation SHALL receive `IArchitecturePolicyDocumentLoader`, `IArchitectureBaselineLoadingService`, `IArchitectureRepositoryRootResolver`, `IConditionSetResolutionService`, `IArchitectureProjectDiscoveryService`, and `IArchitectureAssemblyResolutionService` via constructor injection rather than calling static helpers directly.

#### Scenario: LoadDocument behavior is preserved
- **WHEN** `IArchitectureRunnerSetupService.LoadDocument` is called with a policy path and an optional baseline path
- **THEN** it SHALL return the same `ArchitectureContractDocument` (policy loaded, implemented-coverage-scopes validated, and baseline merged when a baseline path is given) that the static `ArchitectureRunnerFactory.LoadDocument` returned for the same inputs

#### Scenario: BuildRunner behavior is preserved
- **WHEN** `IArchitectureRunnerSetupService.BuildRunner` is called with a loaded document and the same setup parameters previously accepted by `ArchitectureRunnerFactory.BuildRunner`
- **THEN** it SHALL return an equivalent `ArchitectureRunnerSetup` (same repository root, same resolved/missing assemblies, same diagnostics, same project-coverage bypass behavior), and SHALL throw the same `InvalidOperationException` in the same no-assemblies-resolved and condition-set-resolution-failure cases
