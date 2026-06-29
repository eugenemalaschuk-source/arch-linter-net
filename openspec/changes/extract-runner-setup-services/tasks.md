## 1. Wrapper services around existing static helpers

- [ ] 1.1 Add `IArchitecturePolicyDocumentLoader`/`ArchitecturePolicyDocumentLoader` in `src/ArchLinterNet.Core/Contracts/`, wrapping `ArchitectureContractLoader.LoadFromPath` plus the implemented-coverage-scope validation currently private in `ArchitectureRunnerFactory`
- [ ] 1.2 Add `IArchitectureBaselineLoadingService`/`ArchitectureBaselineLoadingService` in `src/ArchLinterNet.Core/Contracts/`, wrapping `ArchitectureBaselineLoader.LoadFromPath` + `ArchitectureBaselineMerger.MergeAndValidate`
- [ ] 1.3 Add `IArchitectureRepositoryRootResolver`/`ArchitectureRepositoryRootResolver` in `src/ArchLinterNet.Core/Resolution/`, wrapping `ArchitectureRepositoryRootLocator.ResolveFrom`
- [ ] 1.4 Add `IConditionSetResolutionService`/`ConditionSetResolutionService` in `src/ArchLinterNet.Core/Contracts/`, wrapping `ConditionSetResolver.TryResolve`
- [ ] 1.5 Add `IArchitectureProjectDiscoveryService`/`ArchitectureProjectDiscoveryService` in `src/ArchLinterNet.Core/Discovery/`, wrapping `ArchitectureProjectDiscovery.ResolveFromDocument` plus the discovery-result merge-back logic (`ApplyDiscoveryResult`) currently private in `ArchitectureRunnerFactory`
- [ ] 1.6 Add `IArchitectureAssemblyResolutionService`/`ArchitectureAssemblyResolutionService` in `src/ArchLinterNet.Core/Resolution/`, wrapping `ArchitectureAssemblyResolver.ResolveFromDocument` plus the project-coverage-contract bypass/diagnostic-throw branching (`HasProjectScopeCoverageContract`, `IsContractIdSelected`) currently private in `ArchitectureRunnerFactory`

## 2. Runner setup orchestrator

- [ ] 2.1 Add `IArchitectureRunnerSetupService` in `src/ArchLinterNet.Core/Execution/`, with `LoadDocument(...)` and `BuildRunner(...)` matching `ArchitectureRunnerFactory`'s current public signatures
- [ ] 2.2 Add `ArchitectureRunnerSetupService` implementing it, constructor-injected with the six services from section 1, replicating `ArchitectureRunnerFactory`'s current logic exactly (including the `ValidationTiming` measurement spans and analysis-context/runner construction as private methods)

## 3. Composition root wiring

- [ ] 3.1 Register the six wrapper services and `IArchitectureRunnerSetupService` in `ServiceCollectionExtensions.AddArchLinterNetCore()` (`AddSingleton`, consistent with existing registrations)

## 4. Application services migrate to constructor injection

- [ ] 4.1 Update `ArchitectureValidationApplicationService` to take `IArchitectureRunnerSetupService` via constructor injection, replacing its `ArchitectureRunnerFactory.LoadDocument`/`BuildRunner` calls
- [ ] 4.2 Update `ArchitectureBaselineApplicationService` to take `IArchitectureRunnerSetupService` via constructor injection, replacing its `ArchitectureRunnerFactory.LoadDocument`/`BuildRunner` calls

## 5. Test migration and new fakeability test

- [ ] 5.1 Update `tests/ArchLinterNet.Core.Tests/ArchitectureRunnerFactoryDiscoveryTests.cs` to construct `ArchitectureRunnerSetupService` directly with real wrapper instances instead of calling the static `ArchitectureRunnerFactory.BuildRunner`
- [ ] 5.2 Update the `ArchitectureRunnerFactory`/`BuildRunner` references in `tests/ArchLinterNet.Core.Tests/CoverageContractReservedTests.cs` and `tests/ArchLinterNet.Core.Tests/ArchitectureCoverageInventoryTests.cs` to use `ArchitectureRunnerSetupService`
- [ ] 5.3 Add a new focused unit test constructing `ArchitectureRunnerSetupService` with a fake `IArchitectureAssemblyResolutionService` (or `IArchitectureProjectDiscoveryService`) and real implementations of the other five dependencies, proving `BuildRunner` uses the fake's result without touching the file system or loading real assemblies
- [ ] 5.4 Grep the repository for any remaining `ArchitectureRunnerFactory` references and confirm none remain in production or test code

## 6. Cleanup and validation

- [ ] 6.1 Delete `src/ArchLinterNet.Core/Execution/ArchitectureRunnerFactory.cs`
- [ ] 6.2 Run `make fmt`
- [ ] 6.3 Run `task acceptance:fresh` and fix any failures
- [ ] 6.4 Run `openspec archive extract-runner-setup-services` and `openspec validate --all`
