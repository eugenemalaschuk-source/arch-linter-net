# architecture-health-badge-relay-runtime Specification

## Purpose
Provides an adopter-owned Relay runtime that authorizes a trusted publisher and
atomically holds the current private-badge publication state without exposing
the publisher's private identity or provenance.

## Requirements

### Requirement: Registry authority is private and immutable-identity bound
The Relay SHALL register an opaque destination alias only through an
administrator-authenticated operation. A registry entry SHALL bind one alias to
immutable repository and owner IDs, selected disclosure profile, exact event and
ref, exact reusable workflow path and commit SHA, destination, consent, and
generation/epoch state. The public route SHALL neither enumerate aliases nor
expose registry data, and an alias SHALL not be reassigned after a tombstone
without an explicit safe registration procedure.

#### Scenario: Unregistered identity cannot select an expected repository
- **WHEN** a publisher request presents an identity with no matching private
  registry entry
- **THEN** the Relay rejects it before creating a challenge or public state
- **AND** the caller cannot supply another expected repository as authority

#### Scenario: Unknown alias does not create durable state
- **WHEN** a public request addresses an unknown alias
- **THEN** the Relay returns the generic unknown-route result
- **AND** it does not create a Durable Object or registry record for that alias

### Requirement: OIDC publisher authentication fails closed
The Relay SHALL accept a publisher token only after bounded verification of the
fixed GitHub issuer and JWKS endpoint, RS256 protected header, configured
audience, signature, required time claims with contract skew, immutable IDs,
event/ref, exact workflow path and SHA, and configured subject form. It SHALL
not trust token-derived URLs, unapproved algorithms, unknown keys after one
fixed-chain refresh, or missing/mismatched claims. Token and claim values SHALL
not be exposed in public responses, redirects, headers, or telemetry.

#### Scenario: Pin mismatch is rejected before a write
- **WHEN** a signed token has a workflow reference or workflow SHA that differs
  from its registry entry
- **THEN** the Relay rejects the request before state mutation
- **AND** it returns only a redacted authorization failure

#### Scenario: Key outage fails closed
- **WHEN** the fixed JWKS endpoint cannot safely resolve the protected key
- **THEN** the Relay rejects the publisher request with a bounded failure
- **AND** it does not serve or create a new ready state as a fallback

### Requirement: Publication is challenge-bound and atomic
The Relay SHALL bind a prepared publication to a one-use challenge, JTI hash,
idempotency key, exact payload digest, requested state, generation,
revocation epoch, and fixed deadline. Publication, renewal, invalidation, and
revocation SHALL update state through an atomic compare-and-set that prevents a
delayed or older writer from replacing a newer, unavailable, or revoked state.
The same idempotency key or replayed token SHALL not create a new generation or
extend a lease, and a mismatched reuse SHALL be rejected without mutation.

#### Scenario: Revocation wins over a delayed ready publish
- **WHEN** a publisher prepares a ready state and the alias is revoked before
  its commit
- **THEN** the commit fails its epoch or generation check
- **AND** the alias remains revoked with no ready payload

#### Scenario: Retry preserves its original deadline
- **WHEN** delivery is retried after a challenge is issued
- **THEN** the Relay evaluates the original fixed deadline
- **AND** retry does not issue a replacement challenge or extend its deadline

### Requirement: State capacity and storage failures are bounded
The Relay SHALL bound request bodies, tokens, payloads, manifests, outstanding
challenges, retained replay records, and per-alias storage. SQLite Durable
Object storage SHALL be the authoritative state unit; storage, clock, or
provider uncertainty SHALL fail closed with redacted diagnostics. The Relay
SHALL not accept arbitrary archive, callback, or network URL input.

#### Scenario: Oversized input is rejected without partial state
- **WHEN** a publisher submits a body, token, payload, or manifest above its
  configured bound
- **THEN** the Relay rejects it before mutation
- **AND** it leaves no challenge, payload, or partial receipt in storage
