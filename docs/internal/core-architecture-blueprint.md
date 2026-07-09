# Core Architecture Blueprint

This is internal project documentation for maintaining the `arch-linter-net` repository. It is intentionally excluded from the public MkDocs/GitHub Pages product site.

This document defines the target healthy `ArchLinterNet.Core` architecture for the architecture-health refactor story (#132) before implementation tasks (#134–#142) begin. It is the design/spec slice tracked as #133.

It distinguishes **architecture goals** (module responsibility, dependency direction, state ownership, extension model) from **IoC/container mechanics** (the chosen composition tool is a wiring decision, not the goal itself).

## Current-state summary

Core already has the right directory shape (`Contracts/`, `Discovery/`, `Execution/`, `Model/`, `Reporting/`, `Resolution/`, `Scanning/`, `Validation/`), and parts of the target architecture already exist:

- `ArchitectureValidator` is already a thin compatibility facade that delegates to `Validation.ArchitectureValidationService`.
- `ArchitectureAnalysisSession`/`ArchitectureAnalysisContext` already separate per-run state (`TypeIndex`, `ReferenceGraph`, coverage inventory cache) from a runner.
- Every contract family executes through a checker resolved from `ArchitectureContractFamilyRegistry.All` (see [Handler/checker extension model](#handlerchecker-extension-model)); there is no remaining family invoked as a direct `runner.CheckXxx()` call inline in `ArchitectureContractExecutor` (#211).

What is not yet healthy:

- `ArchitectureContractRunner` is a 2,428-line `sealed partial class` split across four files (`ArchitectureContractRunner.cs`, `.Checking.cs`, `.Coverage.cs`, `.PolicyConsistency.cs`). It owns per-run state *and* the checking logic for nearly every contract family — the "god object" this refactor must shrink.
- Most orchestration classes (`ArchitectureRunnerFactory`, `ArchitectureContractExecutor`, `ArchitectureValidationService`, `ArchitectureBaselineService`) are static, and several I/O-touching classes (`ArchitectureRepositoryRootLocator`, `ArchitectureProjectDiscovery`/`ArchitectureSolutionParser`/`ArchitectureProjectFileParser`, `ArchitectureAssemblyResolver`, the `Scanning/` classes) are static too, so none of them can be substituted with fakes in tests.
- `ArchLinterNet.Unity.AsmdefValidator` bypasses the application seam entirely (see [Unity-facing adapter seam](#unity-facing-adapter-seam)).

## Module graph and dependency direction

```text
Adapters (Cli, public API, Testing, Unity)
  -> Application (ArchitectureValidationService, ArchitectureBaselineService,
                   IAsmdefValidationService; ArchitectureValidator/
                   ArchitectureBaselineService stay as compatibility facades)
      -> Execution (ArchitectureEngine/EngineBuilder, per-run session/context,
                     ArchitectureContractFamilyRegistry descriptors and their
                     Checker delegates, resolved through
                     ArchitectureContractHandlerRegistry)
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

Every contract family executes through an `ArchitectureContractChecker` delegate (`ArchLinterNet.Core.Execution.Abstractions`) owned by that family's `ArchitectureContractFamilyDescriptor.Checker` — not a per-family `IArchitectureContractHandler` implementation class. This superseded the earlier handler-class-per-family model (#211): dependency, layer, cycle, coverage, allow_only, method_body, asmdef, independence, protected, external, acyclic_sibling, and every other family all resolve their checker the same way, from the same descriptor list.

`ArchitectureContractHandlerRegistry` is **descriptor-populated**, not DI-populated and not a static `CreateDefault()` factory:

- It has a parameterless constructor: it builds its family-keyed lookup by iterating `ArchitectureContractFamilyRegistry.All` (`src/ArchLinterNet.Core/Execution/ArchitectureContractFamilyRegistry.cs`) and reading each descriptor's `Checker`.
- There is no per-family DI registration (`services.AddSingleton<...>()`) — the composition root registers `ArchitectureContractHandlerRegistry` itself as a single singleton, with no constructor arguments.
- Neither the registry nor any `Checker` delegate depends on `IServiceProvider`.

Adding a new contract family requires:

1. Adding the contract schema/model under `Contracts/`.
1. Adding one descriptor to `ArchitectureContractFamilyRegistry.All` (`src/ArchLinterNet.Core/Execution/ArchitectureContractFamilyRegistry.cs`) carrying that family's catalog metadata (YAML group names, baseline capability, strict/audit accessors) *and* its `Checker` delegate (typically a one-line lambda that casts the contract and calls the matching `ArchitectureAnalysisSession.CheckXxxContract` method).
1. If the family's YAML configuration needs load-time validation (beyond schema deserialization), adding an `IArchitecturePolicyDocumentValidator` implementation under `Contracts/Validators/` and registering it in `ArchitecturePolicyDocumentValidatorPipeline.All` (`src/ArchLinterNet.Core/Contracts/Validators/ArchitecturePolicyDocumentValidatorPipeline.cs`) — see [Policy document validation pipeline](#policy-document-validation-pipeline) below.

No step requires editing a central god executor, adding a new handler class, or adding a composition-root registration line. In particular, `ArchitectureContractCatalog.cs` should not need edits for a new family — it builds its descriptors and family order generically from `ArchitectureContractFamilyRegistry.All` (see the `contract-family-registry` OpenSpec capability); only a rare cross-family ordering policy change would touch that file directly. This is the concrete acceptance signal for #137, #211, and the post-coverage expansion story (#104).

### Family checker classes (`Execution.Checkers`)

`ArchitectureAnalysisSession.Check*Contract` is the entry point every descriptor's `Checker` delegate calls, but for a growing subset of families it is now a thin wrapper rather than the algorithm's home. The target split (#213) is: **the session owns per-run context, caches, and shared mutable state; a family's checking algorithm lives in its own class under `ArchLinterNet.Core.Execution.Checkers`, taking only the specific collaborators it needs** (e.g. target assemblies, the type index, an `ArchitectureContractExecutionContext`) instead of the whole session.

Extracted so far: `AssemblyIndependenceChecker`, `PublicApiSurfaceChecker`, `InheritanceChecker` (`assembly_independence`, `public_api_surface`, `inheritance`). For each, `ArchitectureAnalysisSession.Check*Contract` keeps only what is genuinely shared session infrastructure — the `IsContractSelected` gate, the `IsDanglingButCoveredByRuleInputCoverage` deferral (where the family has one), `CreateExecutionContext`, and `CollectUnmatchedIgnores` — and delegates the actual violation-detection algorithm to the checker class. The registry lambda in `ArchitectureContractFamilyRegistry.cs` is unchanged: it still calls `session.CheckXxxContract(...)`, receiving the session as `contract-handler-execution` requires; only what that method does internally changed. See the `family-checker-extraction` OpenSpec capability.

Not yet extracted, and intentionally deferred to scoped follow-ups rather than bundled into one large change:

- `ArchitectureAnalysisSession.Checking.cs` — 11 distinct family algorithms (`dependency`, `layer`, `layer_template`, `allow_only`, `cycle`, `method_body`, `asmdef`, `independence`, `protected`, `external`, `external_allow_only`, `acyclic_sibling`) bundled in one ~750-line file; each needs its own extraction, not one shared move.
- `ArchitectureAnalysisSession.Coverage.cs` and `.PolicyConsistency.cs` — large (~600–800 lines) and either cross-cutting (`PolicyConsistency` runs once per run, not per contract) or internally multi-algorithm.
- `Composition`, `AttributeUsage`, `InterfaceImplementation`, `TypePlacement` — share cross-family helpers (`IsAllowedLocation`, `ResolveProjectAssemblyNames`, currently living in `ArchitectureAnalysisSession.TypePlacement.cs`) that need an explicit ownership decision (moved with one checker, promoted to a shared service, or left session-owned) before any one of the four can be extracted cleanly.
- `AssemblyDependency`, `PackageDependency`, `ProjectMetadata` — each also registers a `ConfigurationContributor` closure in `ArchitectureContractFamilyRegistry.cs`; extracting the checker without deciding whether the contributor moves with it is a separate design question.

## Policy document validation pipeline

`ArchitecturePolicyDocumentLoader.Load` (`src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs`) deserializes `dependencies.arch.yml` and then runs an ordered pipeline of `IArchitecturePolicyDocumentValidator` instances (`ArchitecturePolicyDocumentValidatorPipeline.All`, `src/ArchLinterNet.Core/Contracts/Validators/`) against the parsed document, one class per contract family plus two cross-family checks (duplicate ids, layer namespaces). No step of that pipeline lives on the loader class itself — see the `policy-document-validation-pipeline` OpenSpec capability.

This is a separate mechanism from the `ArchitectureContractFamilyRegistry`/`ArchitectureContractFamilyDescriptor` extension point described above, and intentionally so: `Contracts` depends on nothing else in Core (see the dependency-direction table below), while the registry lives in `Execution`. `ArchitectureContractFamilyDescriptor.AdditionalValidation` therefore cannot be invoked from `Load` without inverting that dependency direction, and remains unused. Do not try to unify the two registries across the module boundary — keep contract cataloguing (`Execution`) and policy-document validation (`Contracts`) as separate, independently-ordered lists.

The pipeline order is load-bearing: validators throw eagerly and first-match-wins, so a document invalid in more than one family always fails with the first pipeline entry's exception. Preserve the existing order when adding an entry unless a reviewed compatibility note says otherwise.

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

These seams are consumed by `Discovery`, `Resolution`, and `Scanning` services (and, narrowly, by Core's own `Asmdef.AsmdefValidationService`, which serves the Unity host through `IAsmdefValidationService`) — not by `Execution` handlers directly, preserving the dependency direction above.

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
- The `ArchitectureContractHandlerRegistry.CreateDefault()` static factory (superseded — the registry now has a parameterless constructor that reads `ArchitectureContractFamilyRegistry.All`; see [Handler/checker extension model](#handlerchecker-extension-model)).

## Core interface namespace convention

Prefer bounded `*.Abstractions` namespaces over a single generic `ArchLinterNet.Core.Interfaces` namespace. An interface earns a move into `<Module>.Abstractions` when it is consumed from a Core module *other than* the one that defines it (plus `Composition`, which wires everything) — that is what makes it a public/application seam, extension/plugin contract, or replaceable infrastructure seam rather than an internal feature seam. Any data/record type that exists only to describe a moved interface's contract (e.g. a handler's result type) moves with it, so the abstraction never depends back on its own implementation namespace.

Current inventory:

| Interface | Category | Namespace |
|---|---|---|
| `IArchitectureValidationApplicationService`, `IArchitectureBaselineApplicationService` | Public/application seam | `ArchLinterNet.Core.Validation.Abstractions` |
| `ArchitectureContractChecker` delegate (+ `ArchitectureHandlerResult`) | Extension/plugin contract | `ArchLinterNet.Core.Execution.Abstractions` |
| `IArchitectureContractExecutor` (+ `ArchitectureContractExecutionResult`), `IArchitectureRunnerSetupService` (+ `ArchitectureRunnerSetup`) | Public/application seam | `ArchLinterNet.Core.Execution.Abstractions` |
| `IArchitectureContractHandlerRegistry`, `IArchitectureContractRunner` | Public/application seam (extracted from `ArchitectureContractHandlerRegistry`/`ArchitectureContractRunner` so the seam interfaces above don't take a concrete Execution type as a parameter or payload) | `ArchLinterNet.Core.Execution.Abstractions` |
| `IArchitecturePolicyDocumentLoader`, `IArchitectureBaselineLoadingService`, `IArchitectureBaselineGenerator`, `IConditionSetResolutionService` | Replaceable infrastructure seam | `ArchLinterNet.Core.Contracts.Abstractions` |
| `IArchitectureProjectDiscoveryService` | Replaceable infrastructure seam | `ArchLinterNet.Core.Discovery.Abstractions` |
| `IArchitectureRepositoryRootResolver` | Replaceable infrastructure seam | `ArchLinterNet.Core.Resolution.Abstractions` |
| `IArchitectureFileSystem`, `IArchitectureEnvironment`, `IArchitectureAssemblyLoader`, `IRoslynCompilationFactory` | Replaceable infrastructure seam | `ArchLinterNet.Core.IO` — namespace unchanged (documented equivalent to `.Abstractions`), but files live under `IO/Abstractions/` for folder-layout consistency with the other modules. This is the one place folder path and namespace intentionally diverge. |
| `IArchitectureAsmdefScanner` | Replaceable infrastructure seam | `ArchLinterNet.Core.Scanning.Abstractions` — moved by #201 once `Asmdef.AsmdefValidationService` started consuming it, crossing the `Scanning` → `Asmdef` module boundary; it was an internal feature seam at the time of the 2026-07-03 audit above. |
| `IArchitectureAssemblyResolutionService`, `IArchitectureDiagnosticFormatter`, `IArchitectureSarifFormatter`, `IArchitectureSolutionParser`, `IArchitectureProjectFileParser`, `IArchitectureSourceScanner`, `IArchitectureExternalDependencyIlScanner`, `IArchitectureIlMethodBodyScanner` | Internal feature seam | stays with its feature (`Execution`, `Reporting`, `Discovery`, `Scanning`) — consumed only from its own module plus `Composition`. Re-audited by #201: all of `Reporting`'s and the rest of `Scanning`'s interfaces were re-checked against the classification rule above and confirmed to still be consumed only within their own module, so none of them moved. |
| `IArchitectureContract` | Data/model marker interface | stays in `Contracts` with the other contract models |

### Accepted exception: `Execution.Abstractions` referencing `ArchitectureAnalysisSession`

The `ArchitectureContractChecker` delegate, `IArchitectureContractExecutor.Execute`, and `IArchitectureContractHandlerRegistry.Execute` all take `ArchLinterNet.Core.Execution.ArchitectureAnalysisSession` as a parameter, and `IArchitectureContractRunner.Session` exposes it as a property getter — every one of these is a reference from `Execution.Abstractions` to that same concrete, behavior-owning class, which stays in `Execution`, not `Execution.Abstractions`. This is a deliberate, reviewed exception rather than an oversight: `ArchitectureAnalysisSession` is the per-run session/context object every contract-family checker and orchestrator is handed by design (see [Session/state ownership](#sessionstate-ownership)); it is also the active target of the god-object shrink tracked by #137/#138. Introducing a full `IArchitectureAnalysisSession` seam now — mirroring a ~2,500-line class still being decomposed elsewhere — would be a speculative abstraction this refactor's own non-goals rule out ("creating interfaces for every class", "moving all interfaces blindly"). `ArchitectureContractHandlerRegistry` and `ArchitectureContractRunner` did get extracted (above) because both were small, stable, already-thin facades where the extraction was cheap; `ArchitectureAnalysisSession` is neither. Revisit this exception if/when #137/#138 finish shrinking the session type.

Self-policy guardrail candidate for [#142](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/142): forbid any `*.Abstractions` namespace from depending on its sibling implementation namespace, with one precise, named exception — `ArchLinterNet.Core.Execution.Abstractions` referencing `ArchLinterNet.Core.Execution.ArchitectureAnalysisSession` (as a parameter or property type) is allowed; no other `Execution` type, and no reference from any other `*.Abstractions` namespace to its sibling, is exempted. Also forbid introducing any `ArchLinterNet.Core.Interfaces` namespace.

Self-policy guardrail candidate for [#215](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/215): the #208–#216 refactor chain replaced this repo's central contract-family bottlenecks with focused extension points. None of the following five guardrails can be expressed as an `architecture/dependencies.arch.yml` contract, so they stay code-review-governed — the first four because no contract family inspects file structure, branch count, or a dispatch pattern's shape, and the fifth because a blanket dependency-direction rule would falsely reject an existing, valid dependency (see its own explanation below). A reviewer should reject a change that reintroduces any of them:

- `ArchLinterNet.Core.Execution.ArchitectureContractFamilyRegistry` and `ArchLinterNet.Core.Contracts.ArchitectureContractFamilyBindings` (`Contracts/ArchitectureContractFamilyBindings.cs`) growing a new inline per-family conditional instead of one appended `ArchitectureContractFamilyDescriptor`/`ArchitectureContractFamilyBinding` entry.
- `ArchitectureAnalysisSession` regaining inline per-family checking or configuration-inspection logic instead of a new class under `ArchLinterNet.Core.Execution.Checkers` or a new `ArchitectureConfigurationContributor` under `ArchLinterNet.Core.Execution.Abstractions`.
- `ArchitectureDiagnosticMapper.FromViolation` (`Reporting/ArchitectureDiagnosticMapper.cs`) regrowing an if/switch dispatch chain instead of a new family supplying an `IArchitectureDiagnosticPayload` under `ArchLinterNet.Core.Model` (see [Adding a new diagnostic family](#adding-a-new-diagnostic-family)).
- `ArchitectureContractModels.cs`/`ArchitectureContractGroups` regrowing an inline `[YamlMember]` cluster for a new contract group instead of a new file under `ArchLinterNet.Core.Contracts.Families`.
- New checkers (`ArchLinterNet.Core.Execution.Checkers`), validators (`ArchLinterNet.Core.Contracts.Validators`), and configuration contributors (`ArchLinterNet.Core.Execution.Abstractions`) reaching directly into a CLI/reporting *adapter* (a formatter, a console/JSON writer, or any `ArchLinterNet.Cli` type) to produce or shape output, instead of depending only on `Contracts`/`Model` abstractions and the per-run session/context they are handed. This cannot be a blanket dependency-direction rule: `core_execution` already legitimately depends on `core_reporting` today — `IArchitectureRunnerSetupService.BuildRunner` (`Execution/Abstractions/IArchitectureRunnerSetupService.cs`) takes a `ValidationTiming` parameter, a `Reporting` type, as part of its seam signature — so a rule forbidding `core_execution`/`core_contracts` from referencing `core_reporting` at all would break that existing, valid dependency. The distinction a reviewer must apply is data-shape vs. adapter-behavior: referencing a `Reporting` *type* used as seam input/output data (like `ValidationTiming`) is fine; a checker/validator/contributor calling into a formatter, writer, or CLI-specific presentation type is the regression to reject.

## Test architecture

Two fake-based test styles cover the seams this refactor introduced, and new contract-family tests should follow whichever shape fits the code under test rather than defaulting to a full end-to-end fixture:

- **Fake infrastructure seam tests** — fake one `IO`/`Discovery`/`Resolution` infrastructure interface (`IArchitectureFileSystem`, `IArchitectureAssemblyLoader`, `IRoslynCompilationFactory`, `IArchitectureProjectDiscoveryService`, `IArchitectureRepositoryRootResolver`, `IArchitectureEnvironment`) and pass it directly into the scanner/resolver/service under test. See `ArchitectureSourceScannerFakeSeamTests`, `ArchitectureAssemblyResolverFakeSeamTests`, `ArchitectureProjectDiscoveryServiceFakeFileSystemTests`, and `ArchitectureRunnerSetupServiceFakeDependencyTests` (`tests/ArchLinterNet.Core.Tests/`).
- **Fake service composition tests** — fake all of an application service's collaborators (`IArchitectureRunnerSetupService`, `IArchitectureContractHandlerRegistry`, `IArchitectureContractExecutor`, `IArchitectureBaselineGenerator`) to drive `ArchitectureValidationApplicationService`/`ArchitectureBaselineApplicationService` end to end without a real file system, assembly, or Roslyn compilation ever being touched. See `ArchitectureValidationApplicationServiceFakeCompositionTests` and `ArchitectureBaselineApplicationServiceFakeCompositionTests`. This is the pattern to reach for when testing an application-seam class: fake its interface-typed constructor parameters, not the infrastructure underneath them.

`ArchitectureValidatorTests` and `ArchitectureBaselineIntegrationTests` remain as real-file, real-assembly integration coverage — both styles matter and neither replaces the other. The fake-based tests prove the seams are independently replaceable and keep new-contract-family fixtures small; the integration tests catch anything a fake's simplified contract might miss.

## Diagnostics and reporting

Checkers/handlers produce structured diagnostics/violations only. Formatters and mappers under `Reporting/` (`ArchitectureDiagnosticFormatter`, `ArchitectureDiagnosticMapper`, `ArchitectureCoverageSummary`) only format already-structured results — they must not reach back into `Execution`, `Discovery`, `Resolution`, or `Scanning`. CLI output concerns stay at the `ArchLinterNet.Cli` boundary. JSON and human-readable output compatibility is preserved unless a behavior change is deliberately reviewed and documented (out of scope for this refactor per #132's non-goals).

### Adding a new diagnostic family

`ArchitectureViolation` (`Model/ArchitectureViolation.cs`) carries only fields common to every family — `ContractName`, `ContractId`, `SourceType`, `ForbiddenNamespace`, `ForbiddenReferences`, `MatchedNamespacePrefixes` — plus one `Payload` slot of type `IArchitectureDiagnosticPayload`. Family-specific evidence never goes on `ArchitectureViolation` itself. To add a new family:

1. Add a sealed `ArchitectureDiagnostic` subtype for the family's output shape (as today).
1. Add a sealed payload record implementing `IArchitectureDiagnosticPayload` (one method, `ToDiagnostic(ArchitectureViolation violation)`) that builds the family's diagnostic from the payload's own fields plus the violation's common fields. See `InheritancePayload`, `CompositionPayload`, etc. for the pattern.
1. At the checker/finder that detects the violation, construct `new ArchitectureViolation(...) { Payload = new YourFamilyPayload(...) }`.

That's it — `ArchitectureDiagnosticMapper.FromViolation` dispatches via `violation.Payload?.ToDiagnostic(violation)` and needs no edits for a new family, nor does `ArchitectureViolation` gain new fields. A violation with no `Payload` set falls through to a plain `DependencyDiagnostic`, which is the correct behavior for checkers that never carry family-specific evidence (e.g. `AssemblyIndependenceChecker`).

`PolicyConsistencyDiagnostic` predates this pattern and is constructed directly by its own contributor without going through `ArchitectureViolation`/the mapper at all — that remains valid for diagnostics that never share the common violation shape.

## Non-goals

- This document does not implement any part of the refactor; #134–#142 do.
- This document does not change YAML policy behavior or add new user-facing contract families.
- This document does not define validation mode/baseline/condition-set semantics for the Unity-facing `IAsmdefValidationService` — it is intentionally narrower than full validation.

## References

- Parent story: [#132](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/132).
- This document: [#133](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/133).
- Implementation tasks that depend on the decisions above: #134 (composition root), #135 (validation/baseline as instance services), #136 (runner setup pipeline services), #137 (handler/checker normalization), #138 (runner-to-session shrink), #139 (infrastructure seams), #140 (adapter rewiring), #141 (seam tests), #142 (self-policy guardrails).
