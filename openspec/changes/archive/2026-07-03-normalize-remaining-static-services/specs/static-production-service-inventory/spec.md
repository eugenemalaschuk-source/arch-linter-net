## MODIFIED Requirements

### Requirement: Production orchestrators named by architecture-health issues are instance-based
Every class classified in `docs/internal/static-class-inventory.md` as a production service/orchestrator/scanner/resolver/parser/loader SHALL be exposed as an interface with an instance implementation registered in the Core composition root (or, for a stateless per-run collaborator consumed only by a non-DI-composed domain object such as `ArchitectureAnalysisSession`, an instance class constructed directly at the point of use), rather than invoked via static method calls from application services.

#### Scenario: Application service resolves the contract executor via DI
- **WHEN** `ArchitectureValidationApplicationService` or `ArchitectureBaselineApplicationService` is constructed through `AddArchLinterNetCore()`
- **THEN** it receives an `IArchitectureContractExecutor` instance through constructor injection and does not call any static `ArchitectureContractExecutor` method

#### Scenario: Contract execution behavior is unchanged
- **WHEN** the same contract document, mode, and handler registry are run through the instance-based `IArchitectureContractExecutor` that were previously run through the static `ArchitectureContractExecutor.Execute`
- **THEN** the resulting violations, cycles, coverage violations, and coverage summaries are identical

#### Scenario: Policy document loading and repository root resolution have no hidden global state
- **WHEN** `IArchitecturePolicyDocumentLoader` or `IArchitectureRepositoryRootResolver` is resolved through `AddArchLinterNetCore()` and invoked
- **THEN** neither implementation holds a `static readonly Lazy<T>` field; all caching, if any, is scoped to the DI-managed instance's lifetime, not a process-wide static field

#### Scenario: Project discovery and assembly resolution own their logic directly
- **WHEN** `IArchitectureProjectDiscoveryService` or `IArchitectureAssemblyResolutionService` is resolved through `AddArchLinterNetCore()` and invoked
- **THEN** the resolution logic executes as instance methods on the service itself, with no forwarding call to a static `ArchitectureProjectDiscovery` or `ArchitectureAssemblyResolver` class (neither class exists after this change)

#### Scenario: Baseline loading, generation, and diagnostic formatting are instance-based
- **WHEN** `IArchitectureBaselineLoadingService`, `IArchitectureBaselineGenerator`, or `IArchitectureDiagnosticFormatter` is resolved through `AddArchLinterNetCore()` and invoked
- **THEN** the corresponding logic executes as instance methods, with no call to a static `ArchitectureBaselineLoader`, `ArchitectureBaselineMerger`, `ArchitectureBaselineGenerator`, or `ArchitectureDiagnosticFormatter` class (none of these remain declared as `static class`)

#### Scenario: Scanners consumed by a per-run session are instance classes
- **WHEN** `ArchitectureAnalysisSession` checks an asmdef, method-body, or external-dependency contract
- **THEN** it invokes an instance method on a directly-constructed `ArchitectureAsmdefScanner`, `ArchitectureSourceScanner`, `ArchitectureIlMethodBodyScanner`, or `ArchitectureExternalDependencyIlScanner` instance, none of which is declared `static class`, and produces the same violations as the prior static implementation

### Requirement: Static class inventory is maintained
The repository SHALL maintain a document classifying every production `static class` under `src/` into one of: pure helper/deterministic mapper, extension method container, constants/options holder, compatibility facade delegating to a composed service, or production service/orchestrator/scanner/resolver/finder.

#### Scenario: Reviewer checks static class classification
- **WHEN** a reviewer or #142 guardrail author needs to know whether a given static class is an intentional exception or unaddressed debt
- **THEN** `docs/internal/static-class-inventory.md` lists the class with its classification and rationale

#### Scenario: No production-service static classes remain unclassified as debt
- **WHEN** a reviewer checks `docs/internal/static-class-inventory.md` after this change
- **THEN** every class previously listed in section (e) as a "follow-up candidate" is either marked "Converted" with its replacing interface/service named, or explicitly documented as a reviewed exception with a rationale
