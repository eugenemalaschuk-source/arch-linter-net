## Context

`ArchitectureAnalysisSnapshot` currently owns an eagerly built runner. Its cache lookup is consequently authorized only after CLR loading, and its evidence is derived from that already materialized context. The fix must retain the snapshot's shared strict/audit semantics while moving artifact authorization ahead of materialization.

## Goals / Non-Goals

**Goals:**

- Construct an immutable metadata-only preparation plan before cache lookup.
- Materialize a runner once only when at least one evaluated mode misses.
- Make artifact selection current-run-owned, identity checked, and fail closed.
- Make cache avoidance and active parallel scanning observable in checked-in evidence.

**Non-Goals:**

- Change contract-finding semantics, cache schema ownership, or turn measured timings into a cross-machine performance promise.

## Decisions

1. A preparation plan owns the composed document, repository/project/build-state data, selected artifact paths, metadata reference closure, and captured identities. It is independent of the cache entry, so the entry can only validate a plan and never select inputs.
2. Snapshot creation performs preparation but holds no runner. `Evaluate(mode)` authorizes a per-mode lookup first; a miss asks the plan to materialize one runner and subsequent misses use it. This preserves combined-mode sharing while allowing a full warm hit to load zero assemblies.
3. Materialization consumes captured bytes or rechecks their digest immediately before isolated loading. A mismatch rejects/restarts planning rather than publishing under the original authorization.
4. Any unsupported dynamic MSBuild input, including an ancestor build file's transitive import, makes the plan cache-ineligible. Correctness is preferred over reuse.
5. The benchmark fixture uses a fact-index contract spanning at least four assemblies. Assertions require active work counters, not merely a bounded upper limit.

## Risks / Trade-offs

- [Metadata closure differs from a CLR resolution] → retain the existing CLR materializer as the sole execution path and fail closed if planning cannot prove the same closure.
- [Build output changes between preparation and load] → compare each planned digest at materialization and restart/reject typedly.
- [Mode A hits while mode B misses] → keep cache authorization per mode but the materialized runner per snapshot.
- [Evidence changes are hardware-sensitive] → store raw samples plus source/package/config identity and comparison deltas only.

## Migration Plan

The plan is internal. Existing cache entries are naturally invalidated if their key/artifact manifests do not match the stronger authorization. Regenerate the release evidence after Core validation passes; the old report remains historical baseline input.

## Open Questions

None; preparation incompleteness is intentionally fail-closed.
