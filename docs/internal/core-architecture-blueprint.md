# Core Architecture Blueprint

This is internal project documentation for maintaining the `arch-linter-net` repository. It is intentionally excluded from the public MkDocs/GitHub Pages product site.

This document defines the target healthy `ArchLinterNet.Core` architecture for the architecture-health refactor story (#132) before implementation tasks (#134–#142) begin. It is the design/spec slice tracked as #133.

It distinguishes **architecture goals** (module responsibility, dependency direction, state ownership, extension model) from **IoC/container mechanics** (the chosen composition tool is a wiring decision, not the goal itself).

## Current-state summary

Core already has the right directory shape (`Contracts/`, `Discovery/`, `Execution/`, `Model/`, `Reporting/`, `Resolution/`, `Scanning/`, `Validation/`), and parts of the target architecture already exist:

- `ArchitectureValidator` is already a thin compatibility facade that delegates to `Validation.ArchitectureValidationService`.
- `ArchitectureAnalysisSession`/`ArchitectureAnalysisContext` already separate per-run state (`TypeIndex`, `ReferenceGraph`, coverage inventory cache) from a runner.
- `IArchitectureContractHandler` already exists and is used for 4 of ~11 contract families (dependency, layer, cycle, coverage).

What is not yet healthy:

- `ArchitectureContractRunner` is a 2,428-line `sealed partial class` split across four files (`ArchitectureContractRunner.cs`, `.Checking.cs`, `.Coverage.cs`, `.PolicyConsistency.cs`). It owns per-run state *and* the checking logic for nearly every contract family — the "god object" this refactor must shrink.
- Contract execution is inconsistent: dependency/layer/cycle/coverage go through `IArchitectureContractHandler`; allow_only, method_body, asmdef, independence, protected, external, and acyclic_sibling are invoked as direct `runner.CheckXxx()` calls inline in the static `ArchitectureContractExecutor`.
- Most orchestration classes (`ArchitectureRunnerFactory`, `ArchitectureContractExecutor`, `ArchitectureValidationService`, `ArchitectureBaselineService`) are static, and several I/O-touching classes (`ArchitectureRepositoryRootLocator`, `ArchitectureProjectDiscovery`/`ArchitectureSolutionParser`/`ArchitectureProjectFileParser`, `ArchitectureAssemblyResolver`, the `Scanning/` classes) are static too, so none of them can be substituted with fakes in tests.
- `ArchLinterNet.Unity.AsmdefValidator` bypasses the application seam entirely (see [Unity-facing adapter seam](#unity-facing-adapter-seam)).

## Module graph and dependency direction

```text
Adapters (Cli, public API, Testing, Unity)
  -> Application (ArchitectureValidationService, ArchitectureBaselineService,
                   IAsmdefValidationService; ArchitectureValidator/
                   ArchitectureBaselineService stay as compatibility facades)
      -> Execution (ArchitectureEngine/EngineBuilder, per-run session/context,
                     IArchitectureContractHandler registry and handlers)
          -> Discovery, Resolution, Scanning
              -> Infrastructure seams (file system, YAML file loading, source
                                         discovery, Roslyn, assembly loading,
                                         environment, clock)

Contracts   -- pure schema/data models; depended on by every layer above,
               depends on nothing in this graph.
Reporting   -- leaf; formats results produced by Execution; consumed by
               Application and Adapters; never depended on by Execution/
               Discovery/Resolution/Scanning.
```

Hard rule: no module may depend upward. `Discovery`, `Resolution`, and `Scanning` never depend on `Execution` or `Validation`. `Execution` never depends on `Reporting` for behavior (only to produce data `Reporting` later formats). `Contracts` depends on nothing else in Core.

### Dependency-direction summary table

| Module | May depend on | Must not depend on |
|---|---|---|
| `Validation` (application seam) | `Execution`, `Contracts`, `Reporting` | Adapters |
| `Execution` | `Contracts`, `Discovery`, `Resolution`, `Scanning`, `Reporting` (data only) | `Validation`, Adapters |
| `Contracts` | nothing (pure schema) | everything else |
| `Discovery` | `Contracts`, infrastructure seam interfaces | `Execution`, `Validation`, `Resolution`, `Scanning` |
| `Resolution` | `Contracts`, infrastructure seam interfaces | `Execution`, `Validation` |
| `Scanning` | `Contracts`, infrastructure seam interfaces | `Execution`, `Validation`, `Discovery`, `Resolution` |
| `Reporting` | `Model`, `Contracts` | `Execution`, `Validation`, `Discovery`, `Resolution`, `Scanning` |
| `ArchLinterNet.Cli` / public API / `ArchLinterNet.Testing` | `Validation` application services only | `Execution`, `Discovery`, `Resolution`, `Scanning` internals; container APIs |
| `ArchLinterNet.Unity` | `IAsmdefValidationService` only | contract loaders, repository-root locators, scanners, container APIs |

## Application seam

`ArchLinterNet.Cli`, the public API, and `ArchLinterNet.Testing` already route through the application seam — `ArchitectureValidationService.Validate`, `ArchitectureBaselineService.Generate`, and the `ArchitectureValidator` compatibility wrapper — instead of owning runner/executor orchestration directly. This blueprint confirms that seam as the **supported contract**, not a redesign target.

The evolution is: these currently-static orchestrators become thin instance services (`IValidationService`, `IBaselineService` or equivalent) built by the composition root, with the existing static entry points (`ArchitectureValidationService`, `ArchitectureBaselineService`, `ArchitectureValidator`) retained only as compatibility facades that resolve and call the composed services. CLI commands, exit codes, public API signatures, and Testing adapter behavior must not change.

## Unity-facing adapter seam

`ArchLinterNet.Unity.AsmdefValidator` currently bypasses the shared validation seam: it calls `ArchitectureContractLoader.LoadFromPath`, `ArchitectureRepositoryRootLocator.ResolveFrom`, and `ArchitectureAsmdefScanner.FindAsmdefViolations` directly, and only ever evaluates `StrictAsmdef` contracts — it has no concept of validation mode, baseline, selected contract IDs, or condition sets.

**Decision: target a narrow composed `IAsmdefValidationService` / `AsmdefValidationService`, not the full validation seam.** Folding Unity onto the full validation seam would require inventing mode/baseline/condition-set semantics that the Unity-facing tool has never needed and nobody has asked for.

Target shape:

- `IAsmdefValidationService` owns contract loading, repository-root resolution, and asmdef scanning orchestration as one composed Core application service.
- `ArchLinterNet.Unity.AsmdefValidator` becomes a thin compatibility facade over that service.
- Unity adapter code must not call contract loaders, repository-root locators, scanners, runner setup, the executor, or any container API directly.
- Full validation (mode, baseline, condition sets, full diagnostics) remains a separate application service for Cli/public API/Testing; this blueprint does not define those semantics for the asmdef-only Unity path (non-goal, matches #140's non-goal).

## Composition root

`Microsoft.Extensions.DependencyInjection` is the default Core composition mechanism, unless a later implementation PR documents a concrete reason to deviate.

Rules:

- Expose a Core `ArchitectureEngine` / `ArchitectureEngineBuilder` (or equivalent named facade) as the supported entry point for building application services.
- `IServiceCollection` and `ServiceProvider` (and any other container-specific API) stay inside the composition root/builder only.
- Core services use constructor injection; no service outside the composition root resolves anything from a container.
- `IServiceProvider` must never be injected into runners, scanners, checkers, handlers, registries, or other execution/domain code.
- Existing static/public entry points remain only as compatibility facades over the composed application services — they do not gain new static orchestration logic.
- This must reduce hand-written factory boilerplate compared to today's static pipeline (`ArchitectureRunnerFactory`, ad hoc `new XxxFactory(...).Create(...)` chains), not just rename it. Factories are kept only where they model a genuine runtime decision or runtime parameter (e.g. resolving which assemblies to load for *this* run), never as generic ceremony wrapping every service.

## Handler/checker extension model

Every contract family executes through an `IArchitectureContractHandler` (or a documented modular equivalent). This generalizes the pattern already used for dependency/layer/cycle/coverage to allow_only, method_body, asmdef, independence, protected, external, and acyclic_sibling, so #137 can remove the remaining direct `runner.CheckXxx()` calls from `ArchitectureContractExecutor`.

The handler registry is **DI-populated**, not a static `CreateDefault()` factory:

- Each family handler is registered explicitly: `services.AddSingleton<IArchitectureContractHandler, DependencyContractHandler>()`, one registration per family.
- The registry (if one is retained as a convenience lookup) is an instance service built from `IEnumerable<IArchitectureContractHandler>` at composition time, keyed by `Family`.
- Neither the registry nor any handler depends on `IServiceProvider`.

Adding a new contract family requires:

1. Adding the contract schema/model under `Contracts/`.
2. Adding an `IArchitectureContractHandler` implementation for the family.
3. Registering it with the composition root (`services.AddSingleton<IArchitectureContractHandler, NewFamilyHandler>()`).
4. Wiring catalog/schema lookup so the family is selectable by mode.

No step requires editing a central god executor or a static default-handler list. This is the concrete acceptance signal for #137 and the post-coverage expansion story (#104).

## Session/state ownership

Per-run mutable state must be scoped to a single validation run and owned by an explicit session/context object (the existing `ArchitectureAnalysisSession`/`ArchitectureAnalysisContext` is the starting point to extend, not replace). Long-lived services (anything built once by the composition root and reused across runs) must be stateless, or hold only replaceable *collaborators* — never another run's results.

Ownership decisions for #135/#138/#139 to implement against:

| State | Owned by |
|---|---|
| Contract document, selected contract IDs | Session/context (set once at session start) |
| Repository root, resolved project/source inputs | Session/context |
| Type index, reference graph | Session/context (already true today via `ArchitectureAnalysisSession`) |
| Roslyn compilation / source analysis state | Session/context, built lazily through the source-discovery infrastructure seam |
| Assembly probing/loading state | Session/context, built through the assembly-loading infrastructure seam |
| Baseline candidates | Session/context, populated during the run, read by baseline generation |
| Unmatched-ignore tracking | Session/context |
| Coverage inventory, findings, summaries | Session/context (already cached per-session today via `BuildCoverageInventory`) |
| Policy-consistency findings | Session/context |
| Timing/phase measurement (`ValidationTiming`) | Passed into the session/handlers as a collaborator, not stored as session state — timing is an observer, not run state |

A validation/baseline run constructs exactly one session/context; handlers and checkers read from it and write findings into it but do not hold their own per-run caches outside it.

## Infrastructure seams

The following currently-static, I/O-touching classes become instance services behind interfaces, registered through the composition root, so they can be faked in tests:

| Concern | Current static implementation | Target seam |
|---|---|---|
| File system access | scattered `File.*`/`Directory.*` calls | `IFileSystem` |
| YAML file loading | the I/O portion of `ArchitectureContractLoader`/`ArchitectureBaselineLoader` | `IFileSystem`-backed loading; the YAML-to-model mapping itself stays a pure static helper |
| Source file discovery | `ArchitectureProjectDiscovery`, `ArchitectureSolutionParser`, `ArchitectureProjectFileParser` | `IProjectDiscovery` |
| Roslyn compilation creation | `ArchitectureSourceScanner`, `ArchitectureIlMethodBodyScanner` | `ISourceScanner` / compilation-factory seam |
| Assembly loading/probing | `ArchitectureAssemblyResolver` | `IAssemblyResolver` |
| Repository root resolution | `ArchitectureRepositoryRootLocator` | `IRepositoryRootLocator` |
| Asmdef scanning | `ArchitectureAsmdefScanner` | `IAsmdefScanner` |
| Environment variable access | scattered `Environment.GetEnvironmentVariable` calls | `IEnvironment` |
| Clock/time access (stale-output checks) | `DateTime.UtcNow`/`DateTime.Now` calls | `IClock` |

These seams are consumed by `Discovery`, `Resolution`, and `Scanning` services (and, narrowly, by the Unity `IAsmdefValidationService`) — not by `Execution` handlers directly, preserving the dependency direction above.

## Static helper vs. static-service rule

**Allowed to remain static** — pure, deterministic, no I/O, no behavior a test would ever need to replace:

- `ArchitectureContractLoader`'s YAML-to-model mapping logic (the parsing itself, not the file read).
- `NamespaceGlobPattern`, `ProjectPathGlob`.
- `ConditionSetResolver`.
- `ArchitectureBaselineMerger` (pure merge over in-memory models).
- `LayerTemplateExpander`.
- `ArchitectureTypeNames` and other diagnostic/violation factory helpers.

**Forbidden as static production services** — anything that does I/O, reflection, or Roslyn work, or that orchestrates other services and therefore needs to be replaceable in tests:

- `ArchitectureRunnerFactory`, `ArchitectureContractExecutor`, `ArchitectureValidationService`, `ArchitectureBaselineService` (become composed instance services; existing static methods become compatibility facades only).
- `ArchitectureRepositoryRootLocator`, `ArchitectureProjectDiscovery`/`ArchitectureSolutionParser`/`ArchitectureProjectFileParser`, `ArchitectureAssemblyResolver`.
- `ArchitectureSourceScanner`, `ArchitectureIlMethodBodyScanner`, `ArchitectureAsmdefScanner`, `ArchitectureReferenceScanner`.
- The `ArchitectureContractHandlerRegistry.CreateDefault()` static factory (becomes DI-populated; see [Handler/checker extension model](#handlerchecker-extension-model)).

## Diagnostics and reporting

Checkers/handlers produce structured diagnostics/violations only. Formatters and mappers under `Reporting/` (`ArchitectureDiagnosticFormatter`, `ArchitectureDiagnosticMapper`, `ArchitectureCoverageSummary`) only format already-structured results — they must not reach back into `Execution`, `Discovery`, `Resolution`, or `Scanning`. CLI output concerns stay at the `ArchLinterNet.Cli` boundary. JSON and human-readable output compatibility is preserved unless a behavior change is deliberately reviewed and documented (out of scope for this refactor per #132's non-goals).

## Non-goals

- This document does not implement any part of the refactor; #134–#142 do.
- This document does not change YAML policy behavior or add new user-facing contract families.
- This document does not define validation mode/baseline/condition-set semantics for the Unity-facing `IAsmdefValidationService` — it is intentionally narrower than full validation.

## References

- Parent story: [#132](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/132).
- This document: [#133](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/133).
- Implementation tasks that depend on the decisions above: #134 (composition root), #135 (validation/baseline as instance services), #136 (runner setup pipeline services), #137 (handler/checker normalization), #138 (runner-to-session shrink), #139 (infrastructure seams), #140 (adapter rewiring), #141 (seam tests), #142 (self-policy guardrails).
