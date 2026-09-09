## Context

The Relay is a contract-only change. Its initial executable fixtures mixed a
provider-owned GitHub JWKS rotation model with adopter-configured key snapshots,
and the second disclosure profile did not have a strict golden representation.

## Decisions

- The issuer and JWKS URI are fixed trust anchors. `kid` selects from the
  endpoint's fetched set, and one bounded refresh handles normal provider
  rotation. Fixture configuration carries no key IDs or rotation schedule.
- The reference publisher event/ref is exactly `push` and `refs/heads/main`;
  the schema enforces the ADR rather than a broader future policy.
- `canonical-freshness.json` is a first-class representation accepted by the
  fixture schema. It fixes key order, default escapes, fields, timestamps, and
  the digest in an executable golden input.

## Risks / Trade-offs

- Provider JWKS unavailability during a required refresh rejects publication;
  this is intentional fail-closed behavior.
- A future event/ref or profile must be a versioned reviewed contract update,
  rather than an untracked broadening of this fixture set.
