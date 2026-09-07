## Why

The primary Architecture Health badge currently presents Health, ignore debt, and
effective-rule count, but hides the independent canonical Gate. Its publication
receipt also lacks enough bound provenance to let a reader distinguish a current
publication from stale renderer/cache transport, or independently verify the
producer and publisher that promoted it.

## What Changes

- Project the canonical Gate alongside the independent canonical Health category,
  ignore-debt total, and effective-rule count in the `badge architecture-health`
  compact headline without deriving any value from CI status, badge color, or
  another displayed value.
- Preserve Health-owned color selection and the existing `0` / `1` / `2` Gate
  exit-code contract; render incomplete evidence as an explicit unassessable
  payload with unknown Gate and counters.
- Strengthen the static publication receipt with compatible, deterministic
  provenance for the repository, analyzed PR/base/head/merged commit and tree
  identities, producer run and attempt, publisher run and attempt, payload
  digest, and publication time.
- Keep the publisher fail-closed and atomic: stale, missing, corrupt, ambiguous,
  or out-of-order evidence must never be represented as a current healthy
  publication.
- Link the README badge to the receipt and document how to verify raw endpoint
  freshness separately from Shields, README, and Camo rendering delay.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-policy-badge`: expose the independent Gate in the primary public
  badge and make the receipt sufficient to verify a current publication.
- `architecture-policy-badge-cli`: project canonical Gate and Health together in
  the deterministic Architecture Health badge payload.
- `github-actions-ci`: retain least-privilege, static-only publication while
  publishing the expanded, atomic provenance receipt.

## Impact

This affects the CLI badge projector and regression tests, the badge-only GitHub
Actions publisher and its transport-contract tests, the README badge navigation,
CI integration guidance, and the corresponding OpenSpec contracts. It changes no
policy, waiver, finding-baseline, Health/Gate evaluation, legacy
`badge architecture-policy`, or public .NET API semantics.
