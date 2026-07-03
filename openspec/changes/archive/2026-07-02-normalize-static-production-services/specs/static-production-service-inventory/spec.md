## ADDED Requirements

### Requirement: Static class inventory is maintained
The repository SHALL maintain a document classifying every production `static class` under `src/` into one of: pure helper/deterministic mapper, extension method container, constants/options holder, compatibility facade delegating to a composed service, or production service/orchestrator/scanner/resolver/finder.

#### Scenario: Reviewer checks static class classification
- **WHEN** a reviewer or #142 guardrail author needs to know whether a given static class is an intentional exception or unaddressed debt
- **THEN** `docs/internal/static-class-inventory.md` lists the class with its classification and rationale

### Requirement: Production orchestrators named by architecture-health issues are instance-based
A static class explicitly identified as production execution orchestration (such as `ArchitectureContractExecutor`) SHALL be exposed as an interface with an instance implementation registered in the Core composition root, rather than invoked via static method calls from application services.

#### Scenario: Application service resolves the contract executor via DI
- **WHEN** `ArchitectureValidationApplicationService` or `ArchitectureBaselineApplicationService` is constructed through `AddArchLinterNetCore()`
- **THEN** it receives an `IArchitectureContractExecutor` instance through constructor injection and does not call any static `ArchitectureContractExecutor` method

#### Scenario: Contract execution behavior is unchanged
- **WHEN** the same contract document, mode, and handler registry are run through the instance-based `IArchitectureContractExecutor` that were previously run through the static `ArchitectureContractExecutor.Execute`
- **THEN** the resulting violations, cycles, coverage violations, and coverage summaries are identical
