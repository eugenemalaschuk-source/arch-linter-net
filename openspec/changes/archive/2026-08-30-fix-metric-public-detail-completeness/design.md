## Context

See [proposal.md](proposal.md). Exact public API detail construction is shared with legacy snapshots,
which intentionally preserves best-effort fallback behavior. The metrics path already owns an
internal aggregate completeness bit.

## Goals / Non-Goals

**Goals:**

- Report every swallowed exact-detail reflection failure to the metrics completeness accumulator.
- Preserve the existing default behavior for legacy callers that do not supply an accumulator.

**Non-Goals:**

- Change exact snapshot text or turn unavailable detail metadata into a validation finding.
- Rescan public surface data for metrics.

## Decisions

### Use an optional failure callback on detail helpers

The detail helper entry points accept an optional callback and pass it to every nested best-effort
reflection branch. Scanner materialization supplies `MarkIncomplete`; existing callers omit the
callback and therefore preserve both their API shape and fallback output. A callback avoids a
second reflection pass or a parallel result model.

## Risks / Trade-offs

- [A helper adds a new fallback branch without signalling it] → route the callback through each
  local catch and cover an unavailable custom-attribute detail path.
- [Legacy snapshots differ] → the callback is observational only and leaves generated entries
  unchanged.

## Migration Plan

1. Thread the optional callback through exact-detail construction.
2. Verify a complete type universe with unavailable detail metadata is unassessable for metrics.
3. Run focused tests and quality checks; rollback is a normal code revert.
