## Context

`ArchitectureContractRunner` is constructed once per validation run by `ArchitectureRunnerFactory.BuildRunner`, wrapping an `ArchitectureAnalysisContext` (repository root + resolved target assemblies). The per-contract check methods in `ArchitectureContractRunner.Checking.cs` call two stateless scanners directly, with no memoization:

- `ArchitectureTypeScanner.FindTypesInLayer`/`FindTypesInNamespace`/`GetLoadableTypes` — re-enumerates `assembly.GetTypes()` for every target assembly on every call.
- `ArchitectureReferenceScanner.GetReferencedTypes(type)` — walks a type's interfaces, base types, fields, properties, methods, and constructors via reflection on every call.

Multiple contract families (`dependency`, `layer`, `cycle`, and others) repeatedly scan the same types and references within a single validation run, since nothing caches results across contract checks. The handler registry introduced in #82 (`IArchitectureContractHandler`, `ArchitectureContractHandlerRegistry`) already isolates the `dependency`, `layer`, and `cycle` families behind a seam that doesn't touch the runner's other check methods, making them the lowest-risk migration targets.

## Goals / Non-Goals

**Goals:**
- Provide a per-run `ArchitectureAnalysisSession` that handlers can use to look up types and references without re-invoking scanners for data already scanned this run.
- Cache the full loadable-type set and per-type reference lookups lazily (computed on first access, not eagerly at session creation).
- Migrate the `dependency`, `layer`, and `cycle` contract families to use the session.
- Preserve identical validation output (violations, cycles, ordering) before and after.

**Non-Goals:**
- Parallel execution of contract checks.
- Performance tuning as a measured, standalone deliverable (this change improves cache locality as a side effect of the abstraction, but does not benchmark or tune it).
- Project-aware Roslyn analysis.
- A dependency graph export command.
- Migrating `allow_only`, `method_body`, `asmdef`, `independence`, `protected`, `external`, or `acyclic_sibling` checks — they keep calling scanners directly.

## Decisions

**Session owns lazy indexes, not the runner directly.** `ArchitectureAnalysisSession` is a new type holding `ArchitectureTypeIndex` and `ArchitectureReferenceGraph` instances, constructed once in `ArchitectureRunnerFactory.BuildRunner` from the same `ArchitectureAnalysisContext` already built there, and passed into `ArchitectureContractRunner`'s constructor alongside the existing context. This keeps `ArchitectureAnalysisContext` as the plain data holder it is today and adds caching behavior in a separate type, rather than growing the context with mutable state.

**Type index caches the full type set once, not per-layer.** `ArchitectureTypeIndex` lazily computes `IReadOnlyList<Type>` for all loadable types across target assemblies on first access (`Lazy<T>`), then filters that cached list per layer query. Caching per-layer-query results was considered and rejected: layers are matched by glob/prefix patterns evaluated per call, and the set of distinct layer queries in a run is not bounded or reusable enough to justify a second cache layer — the expensive part is `assembly.GetTypes()` reflection, not the filter predicate.

**Reference graph caches per-type, built on demand.** `ArchitectureReferenceGraph` holds a `Dictionary<Type, IReadOnlyList<Type>>` populated lazily: the first call for a given `Type` invokes `ArchitectureReferenceScanner.GetReferencedTypes` and stores the materialized result; subsequent calls for the same type return the cached list. This directly targets the redundant reflection walks across contract checks within one run, without needing to precompute the full reference graph for types that are never queried.

**Layer index is a thin helper over the type index, not a third cache.** Rather than a fourth independent cache, the "layer index" the issue asks for is implemented as `ArchitectureTypeIndex` methods (`FindTypesInLayer`, `FindTypesInNamespace`) that filter the memoized full type list using the existing `ArchitectureLayerResolver` matching logic. This avoids duplicating layer-matching rules in a new place.

**Only `dependency`, `layer`, `cycle` migrate in this change.** These three already route through `IArchitectureContractHandler` via `ArchitectureContractHandlerRegistry`, so migrating their corresponding `ArchitectureContractRunner.Checking.cs` methods (`CheckContract`, `CheckLayerContract`, `CheckCycleContract`) to read from `_session` instead of calling `ArchitectureTypeScanner`/`ArchitectureReferenceScanner` statically is a contained, mechanical substitution. The other seven check methods are left untouched per the issue's explicit transition language ("keep scanner classes available internally where needed during transition").

**No behavior change.** The session's lookups return the exact same `Type[]`/`IEnumerable<Type>` data the static scanners would, just memoized. Existing contract-family tests are the primary regression guard; no new violation-detection logic is introduced.

## Risks / Trade-offs

- [Risk] Caching the full type list eagerly on first use could increase peak memory for very large assemblies compared to streaming per-call enumeration. → Mitigation: the cache is built lazily (only on first lookup, not at session construction), and it replaces N re-enumerations per run with 1, which is a net memory/CPU win for any run with more than one contract check (already the common case).
- [Risk] `Dictionary<Type, IReadOnlyList<Type>>` keyed by `Type` could behave unexpectedly across multiple `Assembly` reloads (e.g. `Type` identity differs across `AssemblyLoadContext`s). → Mitigation: the session is scoped to one validation run and one set of resolved assemblies (matching today's `ArchitectureAnalysisContext` lifetime); it is never reused across runs or reloaded assemblies.
- [Risk] Migrating only 3 of 10 handler families leaves an inconsistent mix of session-based and direct-scanner code paths. → Mitigation: this mirrors the existing registry-vs-direct-call split already present from #82, and is explicitly scoped by the issue as a transition step, not the end state.

## Migration Plan

1. Add `ArchitectureAnalysisSession`, `ArchitectureTypeIndex`, `ArchitectureReferenceGraph` under `src/ArchLinterNet.Core/Execution/`.
2. Wire session construction into `ArchitectureRunnerFactory.BuildRunner` and `ArchitectureContractRunner`'s constructor.
3. Migrate `CheckContract`, `CheckLayerContract`, `CheckCycleContract` to use the session.
4. Run existing dependency/layer/cycle contract tests unchanged to confirm no behavior drift.
5. Run full acceptance gate (`task acceptance:fresh`).

No rollback concerns beyond reverting the commit — no data migration, no schema change, no public API change.

## Open Questions

None — scope and approach were confirmed against the issue's explicit acceptance criteria and non-goals during exploration.
