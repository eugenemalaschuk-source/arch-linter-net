# architecture-health-badge-relay-contract Specification

## Purpose
Defines a private-repository badge-publication contract that makes disclosure,
identity, freshness, and recovery independently testable without changing the
canonical Architecture Health evaluator.

## Requirements

### Requirement: Supported publication modes preserve one semantic authority
The system SHALL support `none`, public `github-raw`, and adopter-owned `relay`
publication modes. `none` SHALL make no external Architecture Health disclosure;
`github-raw` SHALL remain a static public snapshot and SHALL reject a private
repository with an actionable visibility error; `relay` SHALL be the sole
turnkey private mode. All modes SHALL transport, rather than recompute, the
four canonical headline values: Gate, Health, explicit-ignore count, and
effective-rule count.

#### Scenario: Private repository selects no transport
- **WHEN** a private repository uses `none`
- **THEN** its required checks, reports, and private artifacts remain available
- **AND** no external Badge Relay destination is contacted

#### Scenario: Private repository selects raw snapshot transport
- **WHEN** a private repository configures `github-raw`
- **THEN** setup rejects the configuration with a visibility diagnostic
- **AND** it does not claim request-time expiry or expose a tokenized raw URL

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

### Requirement: Freshness is bounded independently from canonical headline state
The four canonical headline values SHALL remain unchanged by publication state.
`ready`, `expired`, `unavailable`, and `revoked` SHALL express only whether a
current confirmation exists. A Relay origin SHALL never serve expired ready
data. A ready lease SHALL be at most 60 minutes, renewals SHALL be optional at
no more than once per 30 minutes, and each renewal SHALL be capped by the
canonical semantic validity horizon. Unknown, expired, or mismatched semantic
validity SHALL forbid renewal. Reads, retries, and unchanged source trees SHALL
not extend a lease.

#### Scenario: A stopped publisher expires safely
- **WHEN** no publisher or cron runs after a ready lease reaches `valid_until`
- **THEN** a Relay read returns the profile's explicit expired or unavailable
  rendering rather than the last ready payload
- **AND** it preserves no false claim about the prior Gate or Health

#### Scenario: Same tree cannot extend expired governance evidence
- **WHEN** a tree is unchanged but its waiver, evaluation date, or required
  external evidence has passed its semantic validity horizon
- **THEN** a renewal is rejected
- **AND** the transport does not re-evaluate waiver semantics

### Requirement: Promotion is authenticated and context-bound

The Relay SHALL accept an initial publication only from an approved registry
entry bound to immutable repository and owner identifiers, the exact `push`
event, exact configured branch ref, approved exact workflow path and workflow
commit SHA, disclosure profile, and destination. A metadata-only scheduled
renewal MAY use the `schedule` event only when the same registry entry
explicitly allowlists it; renewal SHALL preserve the same immutable identity,
ref, workflow binding, audience, and validity constraints, and a `schedule`
token SHALL be accepted only on the `renew` operation while all other mutation
operations require `push`. The Relay SHALL
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

### Requirement: Lifecycle changes have revocation precedence and recovery is authoritative
The Relay SHALL implement register, prepare, publish, renew, invalidate, read,
revoke, and recover operations with atomic generation and revocation-epoch
checks. Revoke, delete, visibility/consent loss, ownership transfer, workflow
pin rotation, and uninstall SHALL take precedence over delayed ready data.
Tombstoned aliases SHALL not be reassigned to another tenant without an
explicit safe registration procedure. Recovery after backup restore SHALL
require an approved current publisher proof and SHALL not revive a previous
ready payload or manufacture a current `main` result.

The public HTTP `publish` and `renew` routes SHALL reject caller-supplied
trusted-context proof. After OIDC validation and independent canonical-digest
recomputation, the Worker MAY derive a narrow typed handoff from the pinned
publisher identity and invoke the same commit seam; an internal verifier may
also pass a typed current publisher proof after the exact digest and challenge
state have been checked. No proof validity flag or proof object from request
JSON may authorize a write.

#### Scenario: Restore cannot resurrect revoked data
- **WHEN** storage is restored to a state before a revocation
- **THEN** a previously issued ready payload remains unavailable until an
  approved current proof commits after the restored revocation epoch
- **AND** the public read surface exposes neither history nor private receipts

#### Scenario: Force-push and ABA do not select a winner by SHA
- **WHEN** a branch returns to a previously observed commit or tree after an
  intervening context change
- **THEN** the Relay requires a fresh challenge and current context proof
- **AND** it does not infer recency by commit SHA or run ID ordering

### Requirement: Rendering and release compatibility are explicit and bounded
The Relay SHALL return no HTML, script, link, redirect, external resource, or
unapproved response metadata derived from a payload. Its default SVG SHALL show
an inseparable human-readable absolute `valid_until` timestamp. Bare JSON and
Shields-compatible rendering SHALL be documented as snapshot compatibility, not
as a strict freshness guarantee. A ready origin response SHALL use
`Cache-Control: public, max-age=N, must-revalidate`, where `N` is no greater
than remaining lease; an expired, unavailable, revoked, or uncertain response
SHALL use `Cache-Control: no-store`. ETag/304 and HEAD responses SHALL be
bounded so an origin never represents expired ready data as current. A ready
ETag SHALL identify the complete representation, including profile, payload
bytes, generation, state, and `valid_until`, rather than payload bytes alone.
Already cached browser, Camo, or offline copies SHALL not be claimed revocable.
The versioned Relay bundle, configuration, profiles, publisher, and released CLI
SHALL declare a compatibility matrix and be distributed only through the
existing release authority.

#### Scenario: Cached renderer is distinguished from origin truth
- **WHEN** a cached image remains visible after `valid_until`
- **THEN** documentation identifies it as an uncontrollable cached copy
- **AND** a new origin read returns no ready representation after expiry

#### Scenario: Renewed identical headline receives a new validator
- **WHEN** a publisher renews an identical headline payload with a new
  generation or `valid_until`
- **THEN** the ready ETag changes with the complete representation
- **AND** a conditional request cannot receive a stale 304 for the old lease

#### Scenario: Unknown plan or bundle is not accepted implicitly
- **WHEN** a configuration names an unknown schema, disclosure profile, relay
  bundle, or compatibility plan
- **THEN** setup and publication reject it before registering or updating a
  public destination

### Requirement: Product and transport share executable disclosure conformance
The supported product SHALL expose one canonical validator for each approved
public disclosure profile and SHALL publish its golden positive vectors from
the actual canonical projector. A transport consumer SHALL validate the profile
and exact bytes only; it SHALL not calculate Gate, Health, counts, color, or a
semantic reuse horizon. Rejected bytes SHALL remain rejected and SHALL never be
repaired, sanitized, or reserialized after digest verification.

#### Scenario: Product vectors are accepted by a transport validator
- **WHEN** the canonical projector emits a supported profile representation
- **THEN** its exact UTF-8 bytes and digest pass the corresponding closed-profile
  validator
- **AND** the representation contains no transport-derived Architecture Health facts

#### Scenario: A malicious representation is not repaired
- **WHEN** a representation contains duplicate keys, an extra field, an
  alternative escape or whitespace form, an oversized value, or arbitrary text
- **THEN** profile validation rejects the supplied bytes
- **AND** a consumer cannot publish a modified replacement under that digest

### Requirement: Closed validators accept every shipped canonical representation
The canonical validator SHALL accept each exact canonical representation shipped
with the product, including the unavailable headline marker
`UNASSESSABLE · ? ignores · ? rules` with `lightgrey`. It SHALL reject any
semantic headline count not expressed as `0` or an unpadded decimal from `1`
through `9999`, including leading-zero and five-or-more-digit forms.

#### Scenario: Shipped unavailable bytes are accepted unchanged
- **WHEN** a transport verifies the canonical unavailable fixture's exact UTF-8
  bytes under its declared profile
- **THEN** validation succeeds and returns the fixture's SHA-256 digest

#### Scenario: Schema-external count forms are rejected
- **WHEN** a payload uses `0001` or `10000` for either public headline count
- **THEN** validation rejects the raw bytes
- **AND** no digest is accepted for publication

### Requirement: Reference runtime preserves transport-only authority boundaries
The reference `badge-relay/v1` runtime SHALL implement private registry,
OIDC-authorized publication, and atomic per-alias state without importing the
Core or CLI evaluator, accessing source repositories, or accepting a GitHub
App, PAT, arbitrary artifact URL, or callback URL. It SHALL retain only the
minimum private publication envelope and hashes required for replay protection;
raw JWTs, source identity, source names, SHAs, PR/run data, and detailed errors
SHALL not appear in public responses or public storage routes.

#### Scenario: Two registered repositories remain isolated
- **WHEN** two synthetic immutable repository identities are registered to
  distinct aliases
- **THEN** either publisher is unable to prepare, renew, invalidate, or revoke
  the other alias
- **AND** no public route reveals the other identity or receipt
