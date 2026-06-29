## Context

`ArchitectureRunnerFactory.LoadDocument`/`BuildRunner` is the last major static orchestration step in the validation/baseline setup path. `IArchitectureValidationApplicationService` and `IArchitectureBaselineApplicationService` (from #135) are already instance classes resolved through the composition root (`ArchitectureEngineBuilder`/`ServiceCollectionExtensions`, from #134/#151), but both call straight into the static factory, so nothing in the setup pipeline is fakeable in a unit test.

A repository-wide search confirms `ArchitectureRunnerFactory` is called only by the two application services and by tests in `tests/ArchLinterNet.Core.Tests/` (`ArchitectureRunnerFactoryDiscoveryTests.cs`, plus comments/usages in `CoverageContractReservedTests.cs` and `ArchitectureCoverageInventoryTests.cs`). No CLI, public API, Testing adapter, or Unity code calls it directly — those all go through the validation/baseline application seam already. This means the static class can be deleted outright rather than kept as a compatibility facade.

The seven static helpers the factory delegates to (`ArchitectureContractLoader`, `ArchitectureBaselineLoader`, `ArchitectureBaselineMerger`, `ArchitectureRepositoryRootLocator`, `ConditionSetResolver`, `ArchitectureProjectDiscovery`, `ArchitectureAssemblyResolver`) are each used directly elsewhere (`ArchLinterNet.Unity.AsmdefValidator`, `LayerTemplateExpander`, and their own dedicated unit tests), so converting or removing those statics is out of scope — only `ArchitectureRunnerFactory` itself is being replaced.

## Goals / Non-Goals

**Goals:**
- Replace the static `ArchitectureRunnerFactory` pipeline with composed, constructor-injected instance services.
- Give each setup concern named in issue #136 a focused service or an equivalent seam.
- Make at least one setup dependency fakeable in a focused unit test without touching the file system or loading real assemblies.
- Preserve `LoadDocument`/`BuildRunner` behavior exactly (same inputs, same `ArchitectureRunnerSetup` output, same exceptions/diagnostics).

**Non-Goals:**
- Changing project discovery, condition-set, baseline, or assembly-resolution semantics (issue non-goal).
- Converting the underlying static helpers (`ArchitectureContractLoader`, etc.) to instance services — they have independent call sites outside the runner-setup path.
- Normalizing contract-family execution (`ArchitectureContractExecutor`) — that is #137.
- Shrinking `ArchitectureContractRunner`/session state — that is #138.

## Decisions

### One wrapper service per existing static helper, not one service per "issue bullet"
The issue text lists 8 concerns ("policy/document loading," "baseline loading and merge," ... "analysis context creation," "validation session creation"). Six of these map 1:1 onto an existing static helper class and get a thin wrapper interface + implementation that simply delegates to that helper (plus whatever branching logic currently lives inline in `ArchitectureRunnerFactory` for that step, e.g. the project-coverage bypass logic moves into `IArchitectureAssemblyResolutionService`). This keeps each wrapper meaningful (it owns real branching/IO) rather than a pure pass-through with no test value.

**Alternative considered:** wrap every static call 1:1 with zero extra logic moved in, leaving all branching in the orchestrator. Rejected because the project-coverage bypass logic and discovery-result merge-back logic are exactly the kind of decision logic a test would want to exercise against a fake discovery/resolution result — leaving them in the orchestrator would make the orchestrator itself untestable without fakes for *all* six dependencies at once.

### "Analysis context creation" and "validation session creation" become private methods, not DI services
These two issue-named concerns have no backing static helper today — they're a single `new ArchitectureAnalysisContext(...)` record construction and a single `new ArchitectureContractRunner(...)` constructor call, with no I/O and no branching. Per `docs/internal/core-architecture-blueprint.md`'s explicit guidance that factories/services should model genuine runtime decisions and not become generic ceremony, these stay as well-named private methods on `ArchitectureRunnerSetupService`. This satisfies the issue's "each setup concern has a focused service **or equivalent seam**" wording without introducing a DI interface that would only ever have one implementation and no fake.

**Alternative considered:** `IArchitectureAnalysisContextFactory` / `IArchitectureRunnerFactory` (instance) as separate interfaces. Rejected as ceremony — no test scenario benefits from faking a record constructor call.

### `IArchitectureRunnerSetupService` lives in `Execution/`, composes the six wrappers via constructor injection
This mirrors where `ArchitectureRunnerFactory` already lives and matches the existing layering rule `core-application-seam-layering` (`cli → core_validation → core_execution → core_model`): `Core.Execution` may depend downward on `Core.Resolution`/`Core.Discovery`/`Core.Contracts`, and `Core.Validation` already depends on `Core.Execution`. No new namespace layer is introduced.

### Static `ArchitectureRunnerFactory` is deleted, not kept as a facade
Unlike `ArchitectureValidationService`/`ArchitectureBaselineService` (which kept static facades in #134 because the CLI, public API, and Testing adapter called them directly), `ArchitectureRunnerFactory` has no such external callers. Keeping a static facade here would just reintroduce the same untestable static surface this issue exists to remove. The 3 affected test files are updated in this change to construct `ArchitectureRunnerSetupService` directly with real wrapper instances.

### Wrapper services registered as singletons in `ServiceCollectionExtensions`
Consistent with the existing `IArchitectureValidationApplicationService`/`IArchitectureBaselineApplicationService` registrations. None of the wrapper services hold per-run mutable state (state lives in the returned `ArchitectureRunnerSetup`/`ArchitectureContractRunner`, not in the services), so singleton lifetime is safe.

## Risks / Trade-offs

- [Risk] Constructor signature changes to `ArchitectureValidationApplicationService`/`ArchitectureBaselineApplicationService` break any direct (non-DI) instantiation. → Mitigation: repository-wide search already confirms only the composition root and a couple of integration tests construct these directly; those call sites are updated in this change to either resolve through `AddArchLinterNetCore()` or pass real wrapper instances explicitly.
- [Risk] Moving the project-coverage bypass branching out of the inline `BuildRunner` body into `IArchitectureAssemblyResolutionService` could subtly change behavior if the move is not exact. → Mitigation: existing tests in `ArchitectureRunnerFactoryDiscoveryTests.cs` and `CoverageContractReservedTests.cs` already cover this branching end-to-end; they are kept (only their setup call-site changes) and must continue to pass unmodified in assertions.
- [Trade-off] Six new small interfaces add file/type count for what is currently ~160 lines in one file. Accepted because each wrapper is independently fakeable, which is the issue's explicit acceptance criterion, and each wrapper's namespace placement keeps the layering rules satisfied without new layers.

## Migration Plan

1. Add the six wrapper interfaces/classes (no behavior change yet — `ArchitectureRunnerFactory` still exists and is unused by them).
2. Add `IArchitectureRunnerSetupService`/`ArchitectureRunnerSetupService`, composing the six wrappers, replicating `ArchitectureRunnerFactory`'s current logic exactly.
3. Register all seven new services in `ServiceCollectionExtensions.AddArchLinterNetCore()`.
4. Switch `ArchitectureValidationApplicationService`/`ArchitectureBaselineApplicationService` to constructor-inject `IArchitectureRunnerSetupService`.
5. Update the 3 affected test files to construct `ArchitectureRunnerSetupService` with real wrapper instances instead of calling `ArchitectureRunnerFactory` statics.
6. Add a new focused unit test with a fake `IArchitectureAssemblyResolutionService` (or `IArchitectureProjectDiscoveryService`) injected into `ArchitectureRunnerSetupService`.
7. Delete `ArchitectureRunnerFactory.cs`.
8. Run `make fmt` and `task acceptance:fresh`; fix any fallout.

No runtime rollback strategy is needed beyond reverting the commit/branch — this is a behavior-preserving, compile-time refactor with no data migration or deployment step.

## Open Questions

None — all decisions above were made during the explore phase based on confirmed call-site greps and existing architecture documentation.
