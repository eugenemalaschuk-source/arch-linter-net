## MODIFIED Requirements

### Requirement: Promotion is authenticated and context-bound

The Relay SHALL accept an initial publication only from an approved registry
entry bound to immutable repository and owner identifiers, the exact `push`
event, exact configured branch ref, approved exact workflow path and workflow
commit SHA, disclosure profile, and destination. A metadata-only scheduled
renewal MAY use the `schedule` event only when the same registry entry
explicitly allowlists it; renewal SHALL preserve the same immutable identity,
ref, workflow binding, audience, and validity constraints. The Relay SHALL
verify protected GitHub OIDC JOSE header `alg` and `kid` before trusting the
signed JWT claims. The `kid` SHALL select a key only from the fixed GitHub
issuer JWKS endpoint; an unknown key MAY cause one bounded refresh of that
same endpoint and then SHALL be rejected. Configuration SHALL not pin allowed
key IDs or current/next rotation state. It SHALL verify issuer, audience,
signature, `nbf`, `iat`, `exp`, token size, and bounded clock skew before
writes. It SHALL bind a one-time challenge, token identifier, idempotency key,
digest, target generation, and bounded deadline before a publisher context
recheck. Repository names, workflow names, run IDs, arbitrary URLs, and SHA
sorting SHALL not establish authorization or ordering.

#### Scenario: A stale writer loses after its challenge

- **WHEN** an older writer receives a challenge and the destination changes to
  unavailable, revoked, or a newer generation before its context recheck
- **THEN** its conditional commit is rejected
- **AND** delivery retry cannot move the original challenge deadline

#### Scenario: Replayed identity cannot renew a lease

- **WHEN** a token identifier or idempotency key is replayed with a different
  digest, state, or generation
- **THEN** the Relay rejects the request without changing the destination
- **AND** it records no public source identity or token material

#### Scenario: JWT header and claims are verified in their proper boundaries

- **WHEN** a token has a valid protected JOSE `alg`/`kid` header and valid
  issuer, audience, `nbf`, `iat`, and `exp` claims
- **THEN** the Relay verifies each at its protocol-defined boundary
- **AND** a vector that changes one header or claim remains valid in all other
  header, claim, time, and registry dimensions

#### Scenario: Provider JWKS rotation stays in the fixed trust chain

- **WHEN** a protected `kid` is absent from the cached fixed GitHub JWKS set
- **THEN** the Relay refreshes that fixed endpoint at most once before
  rejecting an unresolved key
- **AND** no configured key-ID allow-list or current/next key snapshot controls
  acceptance

#### Scenario: Scheduled renewal is explicitly allowlisted

- **WHEN** a scheduled metadata-only renewal presents a valid OIDC token for a
  registry entry that lists `push` and `schedule`
- **THEN** the Relay accepts the event only if every immutable identity,
  audience, ref, workflow, and validity check also matches
- **AND** an entry that lists only `push` rejects the same scheduled token
