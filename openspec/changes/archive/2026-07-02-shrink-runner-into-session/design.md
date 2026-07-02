## Context

`ArchitectureContractRunner` (461 lines) plus `.Checking.cs` (596), `.Coverage.cs` (791), `.PolicyConsistency.cs` (580) hold every contract-family algorithm and all per-run mutable state. `ArchitectureAnalysisSession` (internal, `ArchitectureAnalysisSession.cs`) already exists and owns `Context`, a lazy `TypeIndex`, a lazy `ReferenceGraph`, and a cached coverage inventory — introduced by the `analysis-session-indexes` change specifically so contract handlers could share resolved-assembly/reflection lookups. It is the natural home for the rest of the per-run state and algorithms #138 asks to extract; introducing a third, differently-named session type would duplicate that concept for no reason.

Handlers already exist for every family (`ArchitectureContractHandlers.cs`, from #137) but each is a one-line pass-through to `runner.CheckXxxContract(...)`. The runner is constructed once per validation run by `ArchitectureRunnerSetupService.BuildRunner()` and consumed by `ArchitectureValidationApplicationService`, `ArchitectureBaselineApplicationService`, `ArchitectureContractExecutor`, and roughly 30 test files that construct it directly and call its `CheckXxxContract` methods as their test seam.

## Goals / Non-Goals

**Goals:**
- Move every contract-family algorithm body out of the runner and onto `ArchitectureAnalysisSession`.
- Move all per-run mutable state (unmatched-ignore tracking, baseline candidates, rule-input coverage deferral, catalog, document, contract selection) onto the session.
- Change the handler execution contract so handlers receive the session, not the runner.
- Preserve `ArchitectureContractRunner`'s existing public constructor and method signatures so no caller (application services or tests) needs to change.

**Non-Goals:**
- Removing or renaming `ArchitectureContractRunner` itself.
- Changing any diagnostics/violation output, baseline candidate shape, or unmatched-ignore semantics.
- Adding new contract families or changing YAML/contract schema.
- Rewriting the ~30 tests that call `runner.CheckXxxContract(...)` directly — they keep working unchanged against the thin facade.
- Removing per-run caches (type index, reference graph, coverage inventory) — these are preserved as-is on the session.

## Decisions

**Extend `ArchitectureAnalysisSession` rather than introduce a new session type.** It already represents "shared per-run state" per its own spec (`analysis-session-indexes`) and is already threaded through the runner as `internal ArchitectureAnalysisSession Session`. Adding a second session/context abstraction alongside it would just create two competing places to look for per-run state — the opposite of what #138 asks for.

**Keep `ArchitectureContractRunner` as a thin facade instead of deleting it or rewriting its callers.** The alternative — removing `CheckXxxContract` methods from the runner and pointing all ~30 direct-call test files and both application services at the session instead — was considered and rejected: it inflates this PR's diff by rewriting test files that don't need to change to satisfy the issue's acceptance criteria, and increases the chance of accidentally changing test behavior during a mechanical rename. The issue's acceptance criteria say "remove or reduce runner APIs... **where no longer needed**" — these APIs are still needed as the established test seam and applicationservice call sites, so keeping them as one-line delegations satisfies both "runner is no longer the primary home of algorithms" (true — the bodies live on the session) and "reduce...where no longer needed" (nothing here is unneeded).

**Handler signature changes from `(ArchitectureContractRunner runner, ...)` to `(ArchitectureAnalysisSession session, ...)`.** This is the one intentionally breaking change (internal-only interface, no external consumers) — it's the mechanism that makes "handlers receive explicit inputs and dependencies" concrete instead of continuing to hand handlers the god object.

**`ArchitectureContractExecutor.Execute` takes the session instead of the runner.** Its only runner uses today (`PrepareRuleInputCoverageDeferral`, `Catalog`, `BuildCoverageSummary`, passing itself into the handler registry) all move to the session, so passing the runner through was only ever needed to reach these. Application-service call sites change from `ArchitectureContractExecutor.Execute(runner, ...)` to `ArchitectureContractExecutor.Execute(runner.Session, ...)` — the runner keeps its `internal Session` property for exactly this purpose.

**`ArchitectureAnalysisSession` (and its `TypeIndex`/`ReferenceGraph`/`ArchitectureCoverageInventory` supporting types) become C#-`public` instead of staying `internal` as originally planned.** `IArchitectureContractHandler` and `ArchitectureContractHandlerRegistry` must stay public C# types because they're constructor-injected into the already-public `ArchitectureValidationApplicationService`/`ArchitectureBaselineApplicationService`, and `ArchitectureContractHandlerRegistry`'s public constructor must stay reflectable by `services.AddSingleton<ArchitectureContractHandlerRegistry>()` (DI's parameterless-generic registration only locates *public* constructors — an internal constructor here breaks the CLI at runtime, confirmed by trying it). Since the handler interface's `Execute` method is therefore public, its `ArchitectureAnalysisSession` parameter (and everything the session's public members return) must be at least as accessible by C#'s own accessibility rules. A factory-lambda registration (`services.AddSingleton(sp => new ArchitectureContractHandlerRegistry(...))`) was tried to keep the constructor `internal`, but it introduces a compiler-generated closure class in `ArchLinterNet.Core.Composition` that directly `newobj`s an `Execution`-namespace type — a real new architecture-boundary reference the repo's own `ProtectedContractTests` fixture (mirroring the real self-policy's intent) correctly flags. Promoting the session and its supporting types to `public` C# accessibility avoids that closure entirely and costs nothing architecturally: this codebase's actual boundary enforcement is the namespace-scoped contracts in `architecture/dependencies.arch.yml`, not the C# `internal` keyword, and no external assembly (Cli/Unity/Testing) references these newly-public types.

## Risks / Trade-offs

- [Large mechanical diff moving ~2,000 lines between files] → Move method bodies verbatim (field renames only: `_document` → session's own field, `_catalog` → session's own field, etc.); no logic changes. Rely on the full existing test suite (unchanged) to catch any accidental behavior drift.
- [Two types now both look like "the runner": `ArchitectureContractRunner` facade and `ArchitectureAnalysisSession` doing the real work] → Document this clearly in code comments on both types; the facade's sole job is documented as "public API stability for existing callers," not orchestration.
- [Session gains a lot of responsibility, risking becoming the next god object] → Out of scope for #138 to prevent — the issue explicitly scopes this to moving state out of the runner into *a* session/context; further decomposition (e.g. per-family checker classes owning their own state) is left to future issues in the #132 backlog if needed.

## Migration Plan

Single behavior-preserving PR, no data migration. Land on a feature branch, verify via `make fmt` and `task acceptance:fresh` (existing test suite, unchanged assertions), then merge. No rollback concerns beyond a normal revert — no persisted state or schema changes are involved.
