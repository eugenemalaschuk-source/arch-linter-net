## Why

Issue #155 is the namespace-convention follow-up to #133–#139/#154/#159: Core interfaces now live scattered across feature folders, several sharing a file with their concrete implementation, with no documented rule for when an interface should live in a bounded `*.Abstractions` namespace versus stay feature-local. #142's self-policy guardrail work needs an explicit, already-applied convention to encode as an enforceable rule rather than inventing one from scratch.

## What Changes

- Inventory all 24 Core interfaces and classify each as: public/application seam, extension/plugin contract, replaceable infrastructure seam, internal feature seam, or data/model marker interface (recorded in `design.md`).
- Move interfaces that cross an internal Core module boundary (per `docs/internal/core-architecture-blueprint.md`'s dependency-direction table) into a bounded `*.Abstractions` namespace, in a new file separate from their implementation:
  - `ArchLinterNet.Core.Validation.Abstractions`: `IArchitectureValidationApplicationService`, `IArchitectureBaselineApplicationService` (the application seam Adapters route through).
  - `ArchLinterNet.Core.Execution.Abstractions`: `IArchitectureContractHandler` (+ `ArchitectureHandlerResult`, its extension-contract payload), `IArchitectureContractExecutor`, `IArchitectureRunnerSetupService`.
  - `ArchLinterNet.Core.Contracts.Abstractions`: `IArchitecturePolicyDocumentLoader`, `IArchitectureBaselineLoadingService`, `IArchitectureBaselineGenerator`, `IConditionSetResolutionService` (service contracts, not schema/data models).
  - `ArchLinterNet.Core.Discovery.Abstractions`: `IArchitectureProjectDiscoveryService`.
  - `ArchLinterNet.Core.Resolution.Abstractions`: `IArchitectureRepositoryRootResolver`.
- Split the four `ArchLinterNet.Core.IO` files (`IArchitectureFileSystem`, `IArchitectureEnvironment`, `IArchitectureAssemblyLoader`, `IRoslynCompilationFactory`) into an interface-only file and an implementation-only file each, without renaming the namespace — `ArchLinterNet.Core.IO` is documented as the equivalent bounded abstractions namespace for these infrastructure seams (it already contains nothing but interfaces conceptually; renaming would only add churn for no discoverability gain).
- Leave internal feature seams (`Discovery.ArchitectureSolutionParser`/`ArchitectureProjectFileParser`, all four `Scanning.*` scanner interfaces, `Execution.IArchitectureAssemblyResolutionService`, `Reporting.IArchitectureDiagnosticFormatter`) and the one data/model marker interface (`Contracts.IArchitectureContract`) where they are, with the rationale recorded in `design.md` — none of these currently cross an internal module boundary the way the moved interfaces do.
- Update `ServiceCollectionExtensions.AddArchLinterNetCore()` `using` directives for the new namespaces.
- Add a "Core interface namespace convention" section to `docs/internal/core-architecture-blueprint.md` documenting the classification rule and the inventory table, so future services know where new abstractions belong.
- Feed concrete self-policy guardrail candidates (documented, not implemented) for #142: a contract forbidding `*.Abstractions` namespaces from depending on their sibling implementation namespace, and forbidding any new `ArchLinterNet.Core.Interfaces` namespace.

## Capabilities

### New Capabilities

(none — this is a namespace/file reorganization, no new user-facing or contract behavior)

### Modified Capabilities

Most moved interfaces are referenced in their specs only by unqualified name (e.g. `IArchitectureContractHandler`, `IArchitecturePolicyDocumentLoader`), so their spec-level requirements are unaffected by the namespace move — no delta needed for `core-composition-root`, `contract-handler-execution`, `yaml-contract-loading`, `baseline-generation`, `condition-set-config`, or `project-discovery`. Two specs state the fully-qualified `ArchLinterNet.Core.Execution.IArchitectureRunnerSetupService` namespace in their normative requirement text, which changes to `ArchLinterNet.Core.Execution.Abstractions.IArchitectureRunnerSetupService`:

- `runner-setup-services`: the "Runner setup is composed from focused, replaceable services" requirement's FQN reference updates.
- `shared-validation-service`: the "Shared setup and execution building blocks" requirement's FQN reference updates.

## Impact

- `src/ArchLinterNet.Core/Validation/`, `Execution/`, `Contracts/`, `Discovery/`, `Resolution/`, `IO/`: new `Abstractions/` subfolders (except `IO/`, which stays flat) hold split-out interface files; concrete implementation files stay in place with updated `using` directives.
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs`: new `using` directives for the `*.Abstractions` namespaces.
- Any Core file referencing a moved interface: `using` directive updated to the new namespace.
- `tests/ArchLinterNet.Core.Tests/**`: `using` directives updated for moved interfaces; no test behavior changes.
- `docs/internal/core-architecture-blueprint.md`: new namespace-convention section with the interface inventory table.
- No changes to `src/ArchLinterNet.Cli`, `src/ArchLinterNet.Testing`, `src/ArchLinterNet.Unity`, YAML behavior, CLI commands, exit codes, or public API surfaces (none of the moved interfaces are referenced outside `ArchLinterNet.Core`).
