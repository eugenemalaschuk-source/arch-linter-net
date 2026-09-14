## Why

The Relay currently refreshes the fixed GitHub JWKS endpoint for every token whose
`kid` is absent from the in-memory cache. Repeated invalid OIDC traffic can
therefore cause unbounded provider work, while concurrent misses can create a
refresh burst. The existing fail-closed trust boundary and legitimate key
rotation path need an explicit bounded-work contract.

## What Changes

- Bound refresh attempts for unknown signing keys with a short fixed protection
  window tied to the fixed GitHub JWKS endpoint.
- Coalesce concurrent cache misses into one in-flight JWKS refresh.
- Keep refresh and miss bookkeeping bounded independently of attacker-controlled
  `kid` values; do not add an unbounded negative cache.
- Permit a later refresh after the protection window so legitimate GitHub key
  rotation remains recoverable without restart or deployment.
- Preserve fixed endpoint enforcement, exact OIDC checks, fail-closed provider
  outage behavior, and redacted diagnostics.
- Add focused regressions for cache hits, repeated/concurrent misses, rotation,
  provider outage, and bounded state.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-health-badge-relay-runtime`: require bounded and coalesced fixed-
  endpoint JWKS refresh work for unknown signing keys while retaining fail-closed
  authentication and bounded state.
- `architecture-health-badge-relay-contract`: strengthen the OIDC `kid`/JWKS
  rotation contract from one refresh per request to a bounded protection window
  with eventual refresh eligibility.

## Impact

- `relay/src/security.ts` gains bounded refresh state and single-flight
  coordination; the fixed JWKS URL and JOSE validation remain unchanged.
- Relay integration/security tests gain adversarial repeated and concurrent miss
  coverage.
- The two Relay OpenSpec capabilities above receive synchronized requirement and
  scenario updates.
- No package dependency, public payload, registry schema, issuer, audience,
  algorithm, repository/workflow pin, or publication-state change is intended.
