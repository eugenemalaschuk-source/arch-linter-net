## MODIFIED Requirements

### Requirement: Public disclosure profiles are closed byte contracts
The system SHALL define `headline-only/v1` and `headline-plus-freshness/v1`
profiles. A profile SHALL accept only the exact UTF-8 JSON bytes emitted by the
supported canonical CLI producer, including its fixed key order, fixed label,
default JSON escaping, exact Health-owned message/color dictionary, bounded
non-negative counts or the exact unknown marker, and a fixed maximum size.
`headline-only/v1` SHALL retain the existing `System.Text.Json` default escaped
wire representation, including `\\u00B7` for the headline separator;
`headline-plus-freshness/v1` SHALL use the same representation while adding
only `verified_at` and `valid_until`. Each profile SHALL have a
schema-constrained golden representation that records its exact canonical bytes
and SHA-256 digest. A profile SHALL reject duplicate keys, extra fields,
arbitrary text, URLs, headers, errors, SVG metadata, source identity, SHA,
PR/run data, or alternative whitespace/escapes. A transport SHALL reject
invalid bytes rather than sanitize or rewrite them after digest verification.
Color SHALL be determined by Health independently of Gate: `healthy` is
`brightgreen`, `debt` is `yellow`, `degrading` is `orange`, `failing` is `red`,
and `unassessable` is `lightgrey`.

#### Scenario: Canonical ready bytes are accepted
- **WHEN** a producer supplies bytes emitted by the canonical projector for an
  approved disclosure profile
- **THEN** profile validation accepts the exact bytes and their digest
- **AND** the transport does not derive Gate, Health, counts, or color itself

#### Scenario: Existing escaped producer bytes remain compatible
- **WHEN** a `headline-only/v1` payload contains the CLI producer's default
  `\\u00B7` escape sequence
- **THEN** profile validation accepts its exact bytes and digest
- **AND** the literal non-escaped variant is not substituted before verification

#### Scenario: Freshness profile has its own exact-byte representation
- **WHEN** a `headline-plus-freshness/v1` producer supplies its approved
  `verified_at` and `valid_until` values
- **THEN** validation checks the profile's fixed field order, default escaping,
  exact bytes, and recorded digest
- **AND** it does not treat an otherwise unconstrained request envelope as the
  byte contract

#### Scenario: Covert disclosure is rejected
- **WHEN** a payload includes an extra key, duplicate key, noncanonical escape,
  source URL, header value, or arbitrary message text
- **THEN** profile validation rejects it before any public write

#### Scenario: Gate cannot recolor Health
- **WHEN** a payload has `FAIL · HEALTHY`, `FAIL · DEBT`, or `PASS · FAILING`
- **THEN** profile validation accepts only the color assigned to its Health
- **AND** it rejects every other color even when it is in the color enum

### Requirement: Promotion is authenticated and context-bound
The Relay SHALL accept a publication operation only from an approved registry
entry bound to immutable repository and owner identifiers, exact `push` event,
exact `refs/heads/main` ref, approved exact workflow path and workflow commit
SHA, disclosure profile, and destination. It SHALL verify protected GitHub OIDC
JOSE header `alg` and `kid` before trusting the signed JWT claims. The `kid`
SHALL select a key only from the fixed GitHub issuer JWKS endpoint; an unknown
key MAY cause one bounded refresh of that same endpoint and then SHALL be
rejected. Configuration SHALL not pin allowed key IDs or current/next rotation
state. It SHALL verify issuer, audience, signature, `nbf`, `iat`, `exp`, token
size, and bounded clock skew before writes. It SHALL bind a one-time challenge,
token identifier, idempotency key, digest, target generation, and bounded
deadline before a publisher context recheck. Repository names, workflow names,
run IDs, arbitrary URLs, and SHA sorting SHALL not establish authorization or
ordering.

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
- **THEN** the Relay refreshes that fixed endpoint at most once before rejecting
  an unresolved key
- **AND** no configured key-ID allow-list or current/next key snapshot controls
  acceptance
