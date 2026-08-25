## Context

`ArchitectureAnalysisSession` already scopes lazy type, reference, role, and source-file facts to
one immutable validation snapshot. Four metadata hot paths bypass that model: package checks group
all discovered projects on every contract, framework evaluation linearly finds a source project,
assembly dependency checks rebuild an assembly-name dictionary, and project metadata checks rebuild
a normalized-path dictionary. The session is immutable after preparation, so each lookup is a
projection of stable facts rather than independent state.

## Goals / Non-Goals

**Goals:**

- Materialize assembly-name and discovered-project projections at most once per analysis session.
- Preserve the current duplicate-resolution behavior: the first retained assembly or discovered
  project in discovery order remains authoritative.
- Make package, framework-source, assembly-dependency, and project-metadata paths use O(1)
  lookups after their index is materialized.
- Add deterministic internal counters and a synthetic fan-out regression test without altering
  profile, CLI, or finding output contracts.

**Non-Goals:**

- Persisting or sharing indexes across processes, snapshots, cache entries, or build states.
- Changing framework-reference evaluation caching or invoking its evaluator differently.
- Caching exported public API surfaces (owned by #653), broad graph/type indexes, or changing
  public APIs, schemas, diagnostics, baselines, or output ordering.

## Decisions

### Session-owned lazy projections are the only index authority

Introduce one internal metadata-index component created by `ArchitectureAnalysisSession` and used
through its existing fact/checker context boundary. It owns two `Lazy` projections: assembly name
to retained `Assembly`, and discovered-project metadata containing assembly-name to project,
normalized project path to project, and assembly-name to package references. Its values are
read-only projections over `ArchitectureAnalysisContext`; it does not copy, mutate, or become a
second authority for project or assembly metadata.

A single project-metadata materialization builds all three project projections in one pass. This
keeps the covered work bounded by O(P) per session rather than one O(P) pass per contract or per
projection. An alternative of separate dictionaries in each checker was rejected because it
retains duplicated ownership and makes the same regression easy to reintroduce.

### Preserve existing winner and normalization semantics exactly

The index keeps the first value encountered for duplicate assembly names and normalized project
paths, matching the existing `GroupBy(...).First()` and `FirstOrDefault` behavior. It uses the
existing `ProjectPathNormalizer` and existing ordinal comparers, so lookup equivalence does not
change canonical findings, identities, or deterministic ordering.

### Use narrow fact operations, not exposed dictionaries

`ArchitectureAnalysisFactService` and `ArchitectureCheckerContext` provide named lookup operations
needed by family checkers. Package and project-metadata checkers receive only their required lookup;
framework source ownership uses the same fact service; assembly dependency checks receive a direct
assembly lookup. This prevents checkers from rebuilding dictionaries and maintains the #452
checker/session ownership boundary. Existing public-API surface code remains out of scope for #653.

### Instrument internally without changing profile/output schemas

Session profiling counters record assembly-index and project-metadata-index materialization. Tests
assert these counters directly through existing Core test visibility. They are deliberately not
added to public snapshot/profile/JSON types in this issue because the performance behavior is
proved by focused deterministic tests and output semantics must remain unchanged.

## Risks / Trade-offs

- [All project projections materialize when only one is first requested] → The one O(P) pass is
  smaller and safer than repeated per-contract passes, and the session lifetime already owns the
  immutable discovery inventory.
- [Duplicate-key handling changes silently] → Use first-wins insertion and regression cases for
  duplicate assembly names/normalized paths where the existing behavior matters.
- [A checker bypasses the new boundary later] → Remove the local rebuild helpers and add focused
  source/behavior tests that exercise multi-contract fan-out counters.
- [Index work leaks into persisted cache semantics] → Keep all fields process-local and omit them
  from cache/profile identity and serialized output.

## Migration Plan

1. Add the session metadata-index component and internal materialization counters.
2. Add narrow fact/context lookup operations and migrate the four covered checker/service paths.
3. Add multi-project/multi-contract parity and counter tests; run focused Core suites.
4. Synchronize and archive the OpenSpec change after final validation.

Rollback is a normal code revert: no policy document, persisted state, or public data migration is
created.

## Open Questions

None. The parent story and #652 explicitly defer every broader reuse or persistent-state decision.
