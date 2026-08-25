## Context

See `proposal.md` for motivation. Public-API contract evaluation currently asks `ArchitecturePublicApiSurfaceScanner` for a full surface for every contract. Selector filtering separately re-enumerates exported types, and the capture/diff/update/migrate path repeats the same work through its session service. The existing analysis session already owns analogous lazy, per-run indexes.

## Goals / Non-Goals

**Goals:**

- Materialize each loaded assembly's full exported API facts once per session.
- Reuse the materialized exported type set when a selector needs membership.
- Prove deterministic materialization-count and semantic-equivalence invariants without timing-based assertions.

**Non-Goals:**

- Persistent, cross-process, or prepared-state reuse.
- New selectors, altered snapshot formats, profile-schema changes, or parallel contract execution.
- Any redesign of comparison, ignore, escape-safety, finding identity, or output logic.

## Decisions

### 1. The index is private to `ArchitectureAnalysisSession` and keys on assembly object identity

The index owns a dictionary whose keys use reference identity for resolved `Assembly` instances. This exactly matches the current session's immutable resolved-artifact lifetime and cannot confuse two loaded artifacts that happen to share a simple assembly name. A fresh session has a fresh index by construction.

**Alternative considered:** key by simple assembly name or an artifact path/fingerprint. Rejected because name collisions and resolution context can make these aliases ambiguous, while no cross-session reuse is in scope.

### 2. Cache an immutable paired surface: entries and exported types

The scanner will expose one internal materialization that first establishes the exported type universe, then derives the normalized entries from that universe. The index retains read-only copies of both. Selector predicates run over the stored exported types and contract-specific filters operate over the stored entries, so selector safety does not perform a second reflection enumeration.

**Alternative considered:** cache only normalized entries and keep calling the exported-type scanner for selectors. Rejected because it leaves the selector path as repeated reflection, violating the issue's one underlying scan invariant.

### 3. Keep contract-specific work outside the shared index

Each contract will still build its predicate, filter membership, evaluate its declaration/snapshot and comparison mode, match ignores, and produce findings independently. Capture/diff/update/migrate will use the same lookup when they already share the session, but will not gain a persistent lifecycle.

**Alternative considered:** cache selector results or snapshot deltas by contract. Rejected because selector configuration and comparison semantics are contract-specific, and this issue only authorizes sharing deterministic base facts.

### 4. Expose only an internal deterministic test counter

The index's materialization count is available to Core friend tests. `analysis-profile/v1`, its JSON schema, and reviewed public models stay unchanged: no existing output schema has a reserved field for this counter, and adding one would expand an externally versioned format beyond the issue's needed proof.

## Risks / Trade-offs

- **[Risk]** Retaining both entries and exported `Type` objects can increase peak memory for a single-contract session. → **Mitigation:** materialization remains lazy; one retained surface replaces repeated reflection work whenever multiple contracts share an artifact.
- **[Risk]** Refactoring the scanner can accidentally alter normalized ordering or defensive reflection behavior. → **Mitigation:** preserve its type/member traversal and keep existing semantic tests alongside new equivalence scenarios.
- **[Risk]** Capture invokes selector-safety after gathering entries. → **Mitigation:** route both paths through the same session index and assert one materialization.

## Migration Plan

No user migration or artifact rewrite is required. The change is an internal optimization and can be rolled back by removing the index; policies, snapshots, and command syntax remain compatible.
