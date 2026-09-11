## Why

Private repositories need a bounded-freshness Architecture Health badge without
making their source or publisher credentials public. The accepted Relay
contract requires a deployable adopter-owned authorization and storage boundary
that rejects invalid publishers and stale writers before public state changes.

## What Changes

- Add the `badge-relay/v1` Cloudflare Worker reference runtime, backed by a
  SQLite Durable Object that is the serial authority for each opaque alias.
- Verify GitHub Actions OIDC JWTs through the fixed GitHub issuer and JWKS
  chain, then bind immutable repository/owner IDs, event/ref, workflow path and
  workflow SHA to a private registry entry before a publication can prepare.
- Implement bounded, one-use challenges and transactional generation and
  revocation-epoch compare-and-set publication state, including idempotency,
  invalidation, revocation, and recovery barriers.
- Add local Durable Object integration tests for authorization failures,
  replay/race behavior, expiry, resource limits, and redacted failures.

## Capabilities

### New Capabilities

- `architecture-health-badge-relay-runtime`: adopter-owned OIDC-authorized
  Relay registration and atomic publication-state storage for a canonical badge
  payload.

### Modified Capabilities

- `architecture-health-badge-relay-contract`: specify the executable runtime
  guarantees for registration, OIDC verification, challenges, atomic state,
  and fail-closed resource handling.

## Impact

Adds an isolated JavaScript/TypeScript Cloudflare Worker bundle and its local
test harness. It does not change Core, CLI, canonical projection, public
README, existing publisher workflow, or release workflow; later child issues
own those integrations.
