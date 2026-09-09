## Why

Review #5152868750 found that the new executable Relay contract diverges from
the existing CLI's serialized bytes and has incomplete security and cache
boundaries.  Those defects could make downstream implementations accept the
wrong producer bytes, authenticate JWTs incorrectly, or reuse stale metadata.

## What Changes

- Preserve the current CLI's escaped `System.Text.Json` wire bytes and digest
  compatibility for `headline-only/v1`.
- Specify protected JOSE headers separately from JWT claims, including `nbf`,
  and make all single-failure OIDC vectors valid in every other dimension.
- Unify cache-control and ETag rules around complete generation/state/validity
  representations, and complete the Health-owned color dictionary.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `architecture-health-badge-relay-contract`: Corrects byte serialization,
  OIDC verification, caching, ETag, and color invariants.

## Impact

Updates the internal ADR, synthetic schema, golden bytes/digests, and vectors.
It changes no shipped CLI behavior, production runtime, public API, deployment,
or external account setting.
