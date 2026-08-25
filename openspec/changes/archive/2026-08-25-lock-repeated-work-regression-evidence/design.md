## Context

The existing `ArchitectureSessionMetadataIndexesTests` and
`PublicApiSurfaceMaterializationTests` prove #652 and #653 independently.
Their direct session seams are the correct deterministic evidence point:
internal counters do not belong in the versioned `analysis-profile/v1` schema.
See `proposal.md` for motivation.

## Goals / Non-Goals

**Goals:**

- Combine all four metadata-family paths and repeated public-API evaluation in
  one small, anonymized, immutable-session fixture.
- Make the fan-out explicit (24 projects and 16 repeated contract checks), so
  a return to per-contract reconstruction fails deterministically.
- Lock the consumer-visible result projection independently of unstable timing
  or allocation measurements.

**Non-Goals:**

- New runtime indexes, profiling fields, policy syntax, CLI options, public
  APIs, persisted state, or a large-solution benchmark matrix.
- Hardware-sensitive duration or allocation thresholds.
- Work on additional hot paths; any such observation is deferred to #19/#461.

## Decisions

### 1. Use an in-memory session fixture rather than a new on-disk solution

The fixture will construct discovered project metadata and an
`ArchitectureAnalysisSession` directly, matching the tests that introduced
the covered indexes. It will use a real already-loaded test assembly for the
two public-API contracts. This keeps the fixture synthetic, small, and
deterministic while still exercising the consumer shape that causes contract
fan-out.

**Alternative considered:** extend the manual `analysis-profile/v1` benchmark
harness. Rejected because its process timing and profile outputs intentionally
do not expose the internal counters needed for a release-blocking regression
assertion, and it would duplicate #502's broad benchmark scope.

### 2. Assert work counts and canonical projection, not elapsed time

The test will assert one project-metadata index, one assembly-name index, and
one exported public-API surface materialization for the session. It will also
compare the ordered canonical finding projection and pass/fail mode outcomes
from two equivalent executions. This proves result stability without making
machine-specific speed a contract.

**Alternative considered:** write a wall-clock or allocation assertion.
Rejected because those are useful only as optional, environment-sensitive
observations and are explicitly not a release contract for #654.

### 3. Record the evidence boundary in internal documentation

The internal analysis-profile dictionary will point readers to the focused
deterministic fixture and explain that it complements, rather than expands,
the existing manually-run `analysis-profile/v1` harnesses.

## Risks / Trade-offs

- **[Risk]** Direct session construction could omit a consumer-visible result
  surface. **Mitigation:** assert the same ordered canonical projection and
  strict/audit pass/fail outputs through the existing runner/testing seam.
- **[Risk]** The fixture becomes a second benchmark framework. **Mitigation:**
  keep one fixed fan-out shape, no timing loop, generated artifacts, or
  performance baselines.
- **[Risk]** A new unrelated hot path appears while implementing it.
  **Mitigation:** record no code expansion; route it to #19/#461 as required
  by the issue.

## Migration Plan

No migration is needed. The change is tests and internal documentation only;
rollback is a normal revert with no persisted or public compatibility impact.
