## MODIFIED Requirements

### Requirement: Shared setup and execution building blocks
`ArchLinterNet.Core.Execution.Abstractions.IArchitectureRunnerSetupService` SHALL provide `LoadDocument` (policy load + optional baseline merge) and `BuildRunner` (repository-root resolution, condition-set resolution, assembly resolution, runner construction). `ArchLinterNet.Core.Execution.ArchitectureContractExecutor` SHALL provide `Execute`, which runs all contract families for a given mode (`strict`/`audit`) against a runner and returns aggregated violations and cycles, including layer-template expansion.

#### Scenario: Baseline generation reuses the shared building blocks
- **WHEN** `ArchitectureBaselineApplicationService.Generate` runs
- **THEN** it SHALL call `IArchitectureRunnerSetupService.LoadDocument`/`BuildRunner` and `ArchitectureContractExecutor.Execute` for setup and contract execution, rather than re-implementing them, while keeping baseline-specific control flow (the configuration-violation early exit, and running both `strict` and `audit` modes for `Mode = "all"`) in the application service

#### Scenario: Baseline generation does not run asmdef contracts
- **WHEN** `ArchitectureBaselineApplicationService.Generate` calls `ArchitectureContractExecutor.Execute`
- **THEN** it SHALL pass `includeAsmdefContracts: false`, so `strict_asmdef`/`audit_asmdef` contracts are never included in generated baselines
