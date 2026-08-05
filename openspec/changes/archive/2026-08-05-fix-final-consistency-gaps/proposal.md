## Why

PR #431 review found that the final consistency audit overstates its result:
the snapshot contract retains pre-cache lazy-materialization contradictions and
the post-optimization evidence omits the package identity that its contract
requires. These gaps must be reconciled in their owning capabilities before
#411 can hand off to Checkpoint B.

## What Changes

- Align `analysis-snapshot` requirements and counters with metadata-only
  preparation and cache-authorized lazy CLR materialization.
- Require the post-optimization harness and checked-in evidence to retain the
  concrete CLI package ID, version, and SHA-256 digest alongside the executed
  binary and source identity.
- Correct the stale diagnostic-spec purpose and #354 status wording, and update
  the final audit only after the owning corrections are complete.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `analysis-snapshot`: Reconcile snapshot setup and counter semantics with the
  cache-before-materialization contract.
- `analysis-profile`: Make the package identity required by final evidence
  concrete and reproducible.

## Impact

OpenSpec requirements, the explicit benchmark harness and generated evidence,
the final consistency audit, and the parent-story status wording are affected.
No validation finding, cache, or release behavior changes.
