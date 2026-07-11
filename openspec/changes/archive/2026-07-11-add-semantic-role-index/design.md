## Context

`ArchitectureAttributeRoleExtractor.Extract(Type)` (#109) already computes a type's resolved role, metadata, classification source, and per-type conflict/failure facts. Nothing retains that result: `ArchitectureAnalysisSession.CheckClassificationFacts()` builds a fresh extractor and iterates every type on each call, discarding `Role`/`Source`/`Metadata` and keeping only `Conflicts`/`MetadataFailures`. `ArchitectureAnalysisSession` already hosts two lazily-scoped, per-run caches (`ArchitectureTypeIndex`, `ArchitectureReferenceGraph`) built once in the constructor and reused for the life of the session — this change adds a third, following the same shape.

## Goals / Non-Goals

**Goals:**
- One extraction pass per run, cached, exposed as a queryable index.
- Explainable per-type role descriptor: role, metadata, classification source, evidence.
- Deterministic conflict/evidence-failure diagnostics sourced from the same cached pass (replacing the current re-run-per-call behavior).
- JSON output for discovered role descriptors, matching the existing `classification_conflicts` convention.
- Forward-compatible descriptor shape: `ArchitectureClassificationSource` already distinguishes `TypeAttribute`/`AssemblyAttribute`; the index's public shape must not preclude future sources (YAML override, inheritance, namespace, path) from #107's design without a breaking change.

**Non-Goals:**
- No new classification sources (YAML override, inheritance, namespace, path) — only the two existing sources from #109 are wired into the index.
- No selector syntax, layer `role:` matching, or coverage integration — those are tracked separately per #106/#107.
- No change to `classification.attributes`/`classification.assembly_attributes` YAML schema.

## Decisions

**Reuse `ArchitectureTypeClassificationResult` as the per-type descriptor rather than inventing a parallel type.** It already carries `Role`, `Source`, `Metadata`, `Conflicts`, `MetadataFailures`. The index stores one per classified type. Alternative considered: a new `ArchitectureRoleDescriptor` record — rejected because it would duplicate the existing shape with no added value; the extractor's own result already satisfies the acceptance criteria ("role, metadata, classification source, and evidence").

**`ArchitectureRoleIndex` owns a single `Lazy<T>`-computed pass over `TypeIndex.AllTypes()`, not one lazy dictionary entry per type.** The extractor's conflict/failure detection is inherently a whole-type-universe pass (assembly-level candidates are cached across types, same-tier conflicts compare across the full attribute list), so per-type laziness would not save extractor work — only a single pass is cheap to reuse. Alternative considered: per-type `Lazy<ArchitectureTypeClassificationResult>` entries mirroring `ArchitectureReferenceGraph`'s per-key memoization — rejected because the extractor's assembly-candidate caching and conflict detection already amortize work across the whole pass; splitting it up would add complexity without a performance benefit, since the very first lookup would need to run the equivalent of the full pass anyway to detect same-tier conflicts.

**`ArchitectureAnalysisSession.CheckClassificationFacts()` becomes a thin read of `RoleIndex.Conflicts`/`RoleIndex.MetadataFailures`.** This removes the current re-run-per-call cost and is the only behavior change to the `attribute-role-extraction` capability — output values are unchanged, only sourced from the cached index.

**Empty-classification fast path.** When `Document.Classification` has no `attributes`/`assembly_attributes` entries, `ArchitectureRoleIndex` short-circuits to empty collections without invoking the extractor per type, matching the existing "predates this capability" behavior guarantee.

## Risks / Trade-offs

- [Risk] Making the index eager-in-first-access rather than fully per-type-lazy means the first lookup pays the full-run cost, even if only one type's role is queried. → Mitigation: this matches `ArchitectureTypeIndex`'s existing `AllTypes()` behavior (also a single eager-on-first-access array build), and `CheckClassificationFacts()` already paid this cost every call before this change — net cost strictly decreases.
- [Risk] Reusing `ArchitectureTypeClassificationResult` (a mutable class from #109) as the index's stored value could let callers mutate cached state. → Mitigation: the index exposes lookups as read-only views (e.g. `IReadOnlyDictionary`/`TryGetDescriptor` returning the stored instance); no index API mutates it. If mutation risk is judged unacceptable in review, callers should treat the returned descriptor as immutable by convention, matching how `Conflicts`/`MetadataFailures` are already treated as read-only downstream.

## Migration Plan

Additive for consumers: `ArchitectureAnalysisSession.RoleIndex` is a new property; `CheckClassificationFacts()` keeps its existing signature and return shape. No caller-visible breaking change. Rollback is a straightforward revert since no YAML/policy schema changes.
