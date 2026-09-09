## Why

The second review of the Relay contract found three remaining mismatches between
the ADR and its executable fixtures.  The configured OIDC key model pins
individual `kid` values despite the ADR selecting keys from GitHub's fixed JWKS
endpoint with a bounded refresh; the schema accepts an event the ADR forbids;
and the freshness disclosure profile has no standalone exact-byte golden
representation.

## What Changes

- Make the fixed GitHub issuer/JWKS endpoint and bounded unknown-`kid` refresh
  the sole OIDC key-rotation model; remove fixture-level key snapshots and
  rotation schedules.
- Restrict the executable registry event contract to `push` on
  `refs/heads/main`.
- Add a schema-constrained, digest-checked
  `headline-plus-freshness/v1` canonical representation alongside the existing
  headline-only golden fixtures.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `architecture-health-badge-relay-contract`: Aligns OIDC trust rotation,
  event binding, and both public profile byte contracts.

## Impact

Updates the internal ADR, synthetic fixture schema and vectors, and the
canonical OpenSpec contract. It changes no shipped CLI behavior, runtime code,
public API, deployment, or external account setting.
