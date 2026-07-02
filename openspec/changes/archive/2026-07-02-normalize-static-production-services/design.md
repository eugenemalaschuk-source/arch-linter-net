## Context

Core already has a composition root (`AddArchLinterNetCore()`) and a growing set of constructor-injected services (`ArchitectureRunnerSetupService`, `ArchitectureContractHandlerRegistry`, `ArchitectureValidationApplicationService`, etc. — introduced by #133-#137/#151-#153). Alongside these, 34 production `static class` declarations remain in `src/`, ranging from genuinely pure helpers to orchestrators with real dispatch logic. Two of them (`ArchitectureContractLoader`, `ArchitectureRepositoryRootLocator`) hold hidden global state via `private static readonly Lazy<T>` singleton fields; several others are already "shadow-wrapped" by a DI-registered service that just forwards to the static class body (e.g. `ArchitecturePolicyDocumentLoader` → `ArchitectureContractLoader`). Issue #154 asks for these classified and the behavior-owning ones converted, split across PRs by module.

## Goals / Non-Goals

**Goals:**
- Produce a complete, reviewable classification of every production static class in `src/`, sufficient to satisfy #154's inventory acceptance criterion and seed #142's guardrail list.
- Convert the one orchestrator issue #154 names explicitly (`ArchitectureContractExecutor`) to an instance-based, DI-registered service, proving the conversion pattern end to end (interface, registration, constructor injection at both call sites, test coverage) for later follow-ups to replicate.
- Keep the change behavior-preserving: identical dispatch order, identical `ExecutionResult` shape, identical exceptions.

**Non-Goals:**
- Converting all 14 identified production-service statics in this PR. The issue itself allows per-module PR splitting and explicitly warns against "rewriting all Core modules in one unreviewable PR." The other 13 (loaders with global `Lazy` state, scanners, parsers, resolvers) are documented as known follow-up work, not implemented here.
- Removing or renaming any static class's public API surface (e.g. `ArchitectureContractLoader.Load()`, `ArchitectureValidationService`, `ArchitectureBaselineService` compatibility facades are left untouched).
- Changing `ArchitectureContractExecutor`'s dispatch algorithm or its `ExecutionResult` record shape.

## Decisions

- **New `IArchitectureContractExecutor` interface with a single `Execute(...)` method matching the static method's current signature.** Keeps the change mechanical: callers change from `ArchitectureContractExecutor.Execute(...)` to `_executor.Execute(...)`, no logic changes. Alternative considered: fold the executor's logic directly into `ArchitectureContractHandlerRegistry` — rejected because the executor's family-iteration/coverage-summary responsibility is distinct from the registry's per-family handler dispatch responsibility, and merging them would be a bigger, riskier diff than the issue calls for.
- **Register `IArchitectureContractExecutor` as an `AddSingleton` in `ServiceCollectionExtensions`, alongside the existing handler/service registrations.** `ArchitectureContractExecutor` has no fields/state (all state is a method parameter `ArchitectureAnalysisSession session`), so it is safe as a singleton, consistent with the existing registrations of stateless services like `ArchitectureContractHandlerRegistry`.
- **`ArchitectureValidationApplicationService` and `ArchitectureBaselineApplicationService` take `IArchitectureContractExecutor` as a new primary-constructor parameter.** Both are already constructor-DI classes registered as singletons in the composition root, so DI resolution "just works" without further registration changes.
- **`IArchitectureContractExecutor` and `ArchitectureContractExecutor` become `public`.** The class was `internal static class` before, but it is now a constructor parameter of `ArchitectureValidationApplicationService`/`ArchitectureBaselineApplicationService`, both of which are `public` classes — C# requires a public member's parameter/return types to be at least as accessible as the member itself (CS0051), and `ExecutionResult` is a nested type of `ArchitectureContractExecutor`, so the whole type had to become public. This slightly grows public API surface versus a purely `internal` conversion, but matches the existing pattern of other DI-registered Core services (e.g. `ArchitectureContractHandlerRegistry` is already public for the same reason).
- **Test call sites in `ArchitectureContractHandlerRegistryTests.cs` construct `new ArchitectureContractExecutor()` directly** rather than going through the DI container, matching how the same test file already constructs `ArchitectureContractHandlerRegistry` and handler instances directly with fakes.
- **Inventory lives in `docs/internal/static-class-inventory.md`**, next to the existing `docs/internal/core-architecture-blueprint.md`, matching the doc placement convention for internal architecture documentation already used in this repo.

## Risks / Trade-offs

- [Partial conversion leaves #154's "no behavior-owning Core service remains static" criterion unmet repo-wide] → Mitigated by explicitly scoping and documenting this PR as 1-of-N in both the proposal and the PR description, with the inventory doc listing the remaining conversion candidates so follow-up work is trackable rather than silently deferred.
- [Missing a call site when switching `ArchitectureContractExecutor` from static to instance] → Mitigated by `grep`-verified enumeration of every reference (2 production call sites, 4 test call sites) before editing, and by the build/test suite failing to compile if any are missed.
- [Singleton registration wrongly assumes the executor is stateless] → Verified by reading the full class: the only fields are `private const string` family-name constants; all mutable state lives in the `session`/`handlerRegistry` parameters passed per call.

## Open Questions

None — internal/public visibility, registration lifetime, and PR scope were resolved above.
