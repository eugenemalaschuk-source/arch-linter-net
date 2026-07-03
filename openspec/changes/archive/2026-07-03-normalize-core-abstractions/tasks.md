## 1. Validation.Abstractions

- [x] 1.1 Move `IArchitectureValidationApplicationService` into `Validation/Abstractions/IArchitectureValidationApplicationService.cs` under `ArchLinterNet.Core.Validation.Abstractions`.
- [x] 1.2 Move `IArchitectureBaselineApplicationService` into `Validation/Abstractions/IArchitectureBaselineApplicationService.cs` under `ArchLinterNet.Core.Validation.Abstractions`.
- [x] 1.3 Fix `using` directives in `ArchitectureValidationApplicationService.cs`, `ArchitectureBaselineApplicationService.cs`, `ServiceCollectionExtensions.cs`, and any other referencing file.

## 2. Execution.Abstractions

- [x] 2.1 Move `IArchitectureContractHandler` and `ArchitectureHandlerResult` into `Execution/Abstractions/IArchitectureContractHandler.cs` under `ArchLinterNet.Core.Execution.Abstractions`.
- [x] 2.2 Split `IArchitectureContractExecutor` out of `Execution/ArchitectureContractExecutor.cs` into `Execution/Abstractions/IArchitectureContractExecutor.cs` under `ArchLinterNet.Core.Execution.Abstractions`; leave the `ArchitectureContractExecutor` class in `Execution/ArchitectureContractExecutor.cs`.
- [x] 2.3 Split `IArchitectureRunnerSetupService` out of `Execution/ArchitectureRunnerSetupService.cs` into `Execution/Abstractions/IArchitectureRunnerSetupService.cs` under `ArchLinterNet.Core.Execution.Abstractions`; leave the `ArchitectureRunnerSetupService` class in place.
- [x] 2.4 Fix `using` directives in all 11 `IArchitectureContractHandler` implementations, `ArchitectureContractExecutor.cs`, `ArchitectureRunnerSetupService.cs`, `ArchitectureContractHandlerRegistry.cs`, `ServiceCollectionExtensions.cs`, `ArchitectureValidationApplicationService.cs`, `ArchitectureBaselineApplicationService.cs`, and any other referencing file.

## 3. Contracts.Abstractions

- [x] 3.1 Split `IArchitecturePolicyDocumentLoader` out of `Contracts/ArchitecturePolicyDocumentLoader.cs` into `Contracts/Abstractions/IArchitecturePolicyDocumentLoader.cs` under `ArchLinterNet.Core.Contracts.Abstractions`.
- [x] 3.2 Split `IArchitectureBaselineLoadingService` out of `Contracts/ArchitectureBaselineLoadingService.cs` into `Contracts/Abstractions/IArchitectureBaselineLoadingService.cs`.
- [x] 3.3 Split `IArchitectureBaselineGenerator` out of `Contracts/ArchitectureBaselineGenerator.cs` into `Contracts/Abstractions/IArchitectureBaselineGenerator.cs`.
- [x] 3.4 Split `IConditionSetResolutionService` out of `Contracts/ConditionSetResolutionService.cs` into `Contracts/Abstractions/IConditionSetResolutionService.cs`.
- [x] 3.5 Fix `using` directives in `Execution/ArchitectureRunnerSetupService.cs`, `Validation/ArchitectureBaselineApplicationService.cs`, `ServiceCollectionExtensions.cs`, and any other referencing file.

## 4. Discovery.Abstractions and Resolution.Abstractions

- [x] 4.1 Split `IArchitectureProjectDiscoveryService` out of `Discovery/ArchitectureProjectDiscoveryService.cs` into `Discovery/Abstractions/IArchitectureProjectDiscoveryService.cs` under `ArchLinterNet.Core.Discovery.Abstractions`.
- [x] 4.2 Split `IArchitectureRepositoryRootResolver` out of `Resolution/ArchitectureRepositoryRootResolver.cs` into `Resolution/Abstractions/IArchitectureRepositoryRootResolver.cs` under `ArchLinterNet.Core.Resolution.Abstractions`.
- [x] 4.3 Fix `using` directives in `Execution/ArchitectureRunnerSetupService.cs`, `ServiceCollectionExtensions.cs`, and any other referencing file.

## 5. IO file splitting (no namespace change)

- [x] 5.1 Split `IO/IArchitectureFileSystem.cs` into interface-only `IArchitectureFileSystem.cs` and implementation-only `ArchitectureFileSystem.cs`, both `ArchLinterNet.Core.IO`.
- [x] 5.2 Split `IO/IArchitectureEnvironment.cs` into `IArchitectureEnvironment.cs` and `ArchitectureEnvironment.cs`.
- [x] 5.3 Split `IO/IArchitectureAssemblyLoader.cs` into `IArchitectureAssemblyLoader.cs` and `ArchitectureAssemblyLoader.cs`.
- [x] 5.4 Split `IO/IRoslynCompilationFactory.cs` into `IRoslynCompilationFactory.cs` and `RoslynCompilationFactory.cs`.

## 6. Composition and documentation

- [x] 6.1 Update `Composition/ServiceCollectionExtensions.cs` `using` directives for all new `*.Abstractions` namespaces.
- [x] 6.2 Add a "Core interface namespace convention" section to `docs/internal/core-architecture-blueprint.md` with the classification rule and the full inventory table from `design.md`, including the two #142 self-policy guardrail candidates.

## 7. Tests and verification

- [x] 7.1 Fix `using` directives across `tests/ArchLinterNet.Core.Tests/**` for every moved interface.
- [x] 7.2 Run `make fmt`.
- [x] 7.3 Run `task acceptance:fresh`; fix any compile errors from missed `using` directives; confirm all existing tests remain green.

## 8. Spec sync and archive

- [x] 8.1 Confirm the `runner-setup-services` and `shared-validation-service` delta specs match the final code; run `openspec validate --all` (or equivalent) after archiving.
- [x] 8.2 Run `opsx-archive` for this change.
