## Why

The Architecture Health badge publication path must remain fail-closed even when
the CLI cannot build, an old workflow run is replayed, or a publication write
is interrupted. Its canonical Health evidence must be validated as the exact
Core-shaped envelope before it can become a reader-visible badge.

## What Changes

- Commit and verify a byte-for-byte CLI-generated unassessable payload so the
  privileged publisher can replace the public endpoint without restore, build,
  or execution.
- Reject stale push/re-run events at the publication write boundary, atomically
  publish payload and metadata with compare-and-swap ref update, and preserve
  no partial endpoint state.
- Resolve the effective `main` branch ruleset requirements rather than scanning
  unrelated repository rulesets.
- Require the complete canonical report-evidence envelope and exact inner/top
  level Health agreement before the CLI projects a ready payload.
- Add workflow and CLI regression fixtures for each rejection and fallback path.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-policy-badge`: strengthen fail-closed public publication,
  freshness, and atomicity guarantees.
- `architecture-policy-badge-cli`: require exact canonical report-evidence
  shape before projecting an assessable Architecture Health badge.
- `github-actions-ci`: define the trusted fallback receipt, effective rules
  proof, and atomic publication behavior.

## Impact

Updates the Architecture Health badge projector, its NUnit coverage, the PR
producer and trusted-main publisher workflows, release workflow fixtures, and
the corresponding public contracts. No public .NET API is added or changed.
