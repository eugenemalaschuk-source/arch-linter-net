## Context

`ArchLinterNet.Core` has 24 interfaces spread across `Contracts/`, `Discovery/`, `Execution/`, `IO/`, `Reporting/`, `Resolution/`, `Scanning/`, and `Validation/`. Most share a `.cs` file with their sole implementation (e.g. `Contracts/ArchitecturePolicyDocumentLoader.cs` defines both `IArchitecturePolicyDocumentLoader` and `ArchitecturePolicyDocumentLoader`). `docs/internal/core-architecture-blueprint.md` already defines the module graph and dependency-direction rules this change must respect; it does not yet define a namespace convention for interfaces themselves. No interface in Core is currently referenced from `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, or `ArchLinterNet.Unity` — all cross-assembly calls go through the static compatibility facades (`ArchitectureValidationService.Validate`, `ArchitectureBaselineService.Generate`, `ArchLinterNet.Unity.AsmdefValidator`). So "public/application seam" here means a seam that crosses an *internal* Core module boundary per the blueprint's table (e.g. Contracts exposing a service to Execution, Execution exposing a service to Validation) — not a seam crossing an assembly boundary.

## Goals / Non-Goals

**Goals:**
- Classify every Core interface into one of the five categories from #155 and record the rationale.
- Give every public/application seam, extension/plugin contract, and cross-module infrastructure seam a discoverable, bounded `*.Abstractions` namespace (or documented equivalent), in a file separate from its implementation.
- Keep the move behavior-preserving: no signature, DI lifetime, or registration-order changes.
- Document the convention in the blueprint so #142 has concrete guardrail candidates.

**Non-Goals:**
- Creating an interface for every class, or moving every interface (explicit non-goals in #155).
- Renaming `ArchLinterNet.Core.IO` — it already functions as a bounded, interface-only namespace for the four low-level I/O seams; per #155's "or a documented equivalent" allowance, this is treated as satisfying the convention without a rename.
- Changing any Scanning/Discovery-internal interface's visibility or promoting it to public — those stay `internal` and feature-local per the blueprint (only `Execution`, `Discovery`, `Resolution`, `Scanning` consume them, never crossing up into `Validation` or across into another sibling module).
- Implementing #142's self-policy contracts — this change only feeds candidate rules into that issue.

## Decisions

### Classification rule: move only where a real internal-module boundary is crossed

An interface is a "public/application seam", "extension/plugin contract", or "replaceable infrastructure seam" worth relocating when it is consumed from a Core module *other than* the one that defines it (plus `Composition`, which wires everything). An interface consumed only from its own defining folder (plus `Composition`) is an internal feature seam and stays put — moving it would add indirection without adding discoverability, since nothing outside its feature needs to find it.

This was checked by grepping, for each interface, every folder under `src/ArchLinterNet.Core` that references it:

| Interface | File (current) | Consumed from (beyond own folder + Composition) | Category | Target |
|---|---|---|---|---|
| `IArchitectureValidationApplicationService` | `Validation/IArchitectureValidationApplicationService.cs` | — (already the outermost application seam; this *is* the seam Adapters route through) | Public/application seam | `Validation.Abstractions` |
| `IArchitectureBaselineApplicationService` | `Validation/IArchitectureBaselineApplicationService.cs` | — (same as above) | Public/application seam | `Validation.Abstractions` |
| `IArchitectureContractHandler` (+ `ArchitectureHandlerResult`) | `Execution/ArchitectureContractHandler.cs` | Implemented by 11 handler classes across `Execution`; this is the documented plugin/extension point in the blueprint's "Handler/checker extension model" | Extension/plugin contract | `Execution.Abstractions` |
| `IArchitectureContractExecutor` | `Execution/ArchitectureContractExecutor.cs` | `Validation` | Public/application seam (Execution → Validation) | `Execution.Abstractions` |
| `IArchitectureRunnerSetupService` | `Execution/ArchitectureRunnerSetupService.cs` | `Validation` | Public/application seam (Execution → Validation) | `Execution.Abstractions` |
| `IArchitecturePolicyDocumentLoader` | `Contracts/ArchitecturePolicyDocumentLoader.cs` | `Execution` | Replaceable infrastructure seam (Contracts → Execution) | `Contracts.Abstractions` |
| `IArchitectureBaselineLoadingService` | `Contracts/ArchitectureBaselineLoadingService.cs` | `Execution` | Replaceable infrastructure seam (Contracts → Execution) | `Contracts.Abstractions` |
| `IArchitectureBaselineGenerator` | `Contracts/ArchitectureBaselineGenerator.cs` | `Validation` | Replaceable infrastructure seam (Contracts → Validation) | `Contracts.Abstractions` |
| `IConditionSetResolutionService` | `Contracts/ConditionSetResolutionService.cs` | `Execution` | Replaceable infrastructure seam (Contracts → Execution) | `Contracts.Abstractions` |
| `IArchitectureProjectDiscoveryService` | `Discovery/ArchitectureProjectDiscoveryService.cs` | `Execution` | Replaceable infrastructure seam (Discovery → Execution) | `Discovery.Abstractions` |
| `IArchitectureRepositoryRootResolver` | `Resolution/ArchitectureRepositoryRootResolver.cs` | `Execution` | Replaceable infrastructure seam (Resolution → Execution) | `Resolution.Abstractions` |
| `IArchitectureFileSystem` | `IO/IArchitectureFileSystem.cs` | `Contracts`, `Discovery`, `Resolution`, `Scanning` (widely consumed I/O primitive) | Replaceable infrastructure seam | stays in `IO` (documented equivalent) |
| `IArchitectureEnvironment` | `IO/IArchitectureEnvironment.cs` | widely consumed | Replaceable infrastructure seam | stays in `IO` |
| `IArchitectureAssemblyLoader` | `IO/IArchitectureAssemblyLoader.cs` | `Execution` | Replaceable infrastructure seam | stays in `IO` |
| `IRoslynCompilationFactory` | `IO/IRoslynCompilationFactory.cs` | `Scanning` | Replaceable infrastructure seam | stays in `IO` |
| `IArchitectureAssemblyResolutionService` | `Execution/ArchitectureAssemblyResolutionService.cs` | only `Execution` + `Composition` | Internal feature seam | stays in `Execution` |
| `IArchitectureDiagnosticFormatter` | `Reporting/ArchitectureDiagnosticFormatter.cs` | only `Reporting` + `Composition` | Internal feature seam / leaf | stays in `Reporting` |
| `IArchitectureSolutionParser` (internal) | `Discovery/ArchitectureSolutionParser.cs` | only `Discovery` + `Composition` | Internal feature seam | stays in `Discovery` |
| `IArchitectureProjectFileParser` (internal) | `Discovery/ArchitectureProjectFileParser.cs` | only `Discovery` + `Composition` | Internal feature seam | stays in `Discovery` |
| `IArchitectureAsmdefScanner` (internal) | `Scanning/ArchitectureAsmdefScanner.cs` | only `Scanning` + `Composition` | Internal feature seam | stays in `Scanning` |
| `IArchitectureSourceScanner` (internal) | `Scanning/ArchitectureSourceScanner.cs` | only `Scanning` + `Composition` | Internal feature seam | stays in `Scanning` |
| `IArchitectureExternalDependencyIlScanner` (internal) | `Scanning/ArchitectureExternalDependencyIlScanner.cs` | only `Scanning` + `Composition` | Internal feature seam | stays in `Scanning` |
| `IArchitectureIlMethodBodyScanner` (internal) | `Scanning/ArchitectureIlMethodBodyScanner.cs` | only `Scanning` + `Composition` | Internal feature seam | stays in `Scanning` |
| `IArchitectureContract` | `Contracts/ArchitectureContractModels.cs` | pure schema, consumed everywhere as a data shape, not a service | Data/model marker interface | stays in `Contracts` (with the other contract models) |

### Contract-shape records move with their interface

`ArchitectureContractExecutionResult` (returned by `IArchitectureContractExecutor.Execute`) and `ArchitectureRunnerSetup` (returned by `IArchitectureRunnerSetupService.BuildRunner`) are pure data shapes that exist only to describe their interface's contract, exactly like `ArchitectureHandlerResult`. Leaving them behind in `Execution` while their interface moves to `Execution.Abstractions` would make the abstraction depend back on the implementation namespace — the opposite of the dependency direction #142's guardrail candidates are meant to enforce. Both move into `Execution.Abstractions` alongside their interface.

### `IO` keeps its namespace instead of becoming `IO.Abstractions`

The four `IO/*.cs` files already contain nothing but one interface and its one implementation each, and the folder contains no other concrete orchestration classes. Renaming `ArchLinterNet.Core.IO` → `ArchLinterNet.Core.IO.Abstractions` would touch every consumer's `using` directive (the most widely-referenced interfaces in Core: `IArchitectureFileSystem` alone is used from four other modules) for zero discoverability gain, since `IO` is already unambiguous. Splitting each file into an interface file and an implementation file captures the acceptance criterion ("concrete implementations do not share files with... seam interfaces") without the rename churn. This is recorded as the "documented equivalent" #155 allows.

### `ArchitectureHandlerResult` moves with `IArchitectureContractHandler`

`ArchitectureHandlerResult` is the return type every handler implementation constructs and every caller of `IArchitectureContractHandler.Execute` consumes — it is part of the extension contract's shape, not an internal execution detail. It moves into `Execution.Abstractions` alongside the interface rather than staying behind in `Execution`.

### No self-policy contract added in this change

#155 says "feed final dependency/self-policy guardrail candidates into #142" — it does not ask this change to add new `architecture/dependencies.arch.yml` contracts. Two candidates are recorded for #142 to implement:
1. Forbid any `*.Abstractions` namespace from depending on its sibling non-abstractions namespace (abstractions must not depend on implementations).
2. Forbid introducing any `ArchLinterNet.Core.Interfaces` namespace.

Adding these now would be scope creep into #142's story; `NamespaceGlobPattern`'s existing prefix-match semantics (confirmed by reading `Resolution/NamespaceGlobPattern.cs`) mean the current layer definitions (e.g. `core_execution: namespace: ArchLinterNet.Core.Execution`) already match the new `.Abstractions` sub-namespaces by prefix, so no `architecture/dependencies.arch.yml` layer changes are required for this change to stay policy-compliant.

## Risks / Trade-offs

- [Moving a widely-referenced interface's namespace touches many `using` directives across Core and its test project] → Each move is a single find-and-replace of one `using ArchLinterNet.Core.X;` → `using ArchLinterNet.Core.X.Abstractions;` per affected file; `task acceptance:fresh` (build + full test suite) catches any missed reference as a compile error, not a silent behavior change.
- [Splitting `IArchitectureContractHandler`'s file also relocates `ArchitectureHandlerResult`, a record referenced by all 11 handler implementations] → Same mechanical `using` fix in each handler file; verified via compile.
- [Under-scoping: leaving `IArchitectureAssemblyResolutionService`/`IArchitectureDiagnosticFormatter` in place could be seen as inconsistent with the blueprint's "infrastructure seam" framing] → Documented explicitly above: the blueprint's infra-seam table refers to the lower-level `IO` primitives these orchestrate, not to the orchestrating service itself; the orchestrating service is not consumed outside its own module today, so moving it now would be speculative relocation ahead of actual cross-module need (#155's own non-goal: "moving all interfaces blindly").

## Migration Plan

1. Create `Abstractions/` subfolders under `Validation/`, `Execution/`, `Contracts/`, `Discovery/`, `Resolution/`; move/split each targeted interface file into `<Module>/Abstractions/<Interface>.cs` with namespace `ArchLinterNet.Core.<Module>.Abstractions`, leaving the implementation class in its original file/namespace.
2. Split the four `IO/*.cs` files into interface-only and implementation-only files, same namespace.
3. Fix every `using` directive across `src/ArchLinterNet.Core`, `tests/ArchLinterNet.Core.Tests` that references a moved interface.
4. Update `Composition/ServiceCollectionExtensions.cs` usings.
5. Document the convention and inventory table in `docs/internal/core-architecture-blueprint.md`.
6. Build, run the full test suite (`task acceptance:fresh`), fix any compile errors from missed usings.

No rollback beyond `git revert` is needed — this is a compile-time-verified, behavior-preserving rename.

## Open Questions

None — scope and target namespaces are fully determined by the module-boundary-crossing rule above.
