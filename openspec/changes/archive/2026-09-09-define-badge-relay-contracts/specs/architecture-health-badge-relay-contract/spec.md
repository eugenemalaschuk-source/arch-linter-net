## Purpose

Defines a private-repository badge-publication contract that makes disclosure,
identity, freshness, and recovery independently testable without changing the
canonical Architecture Health evaluator.

## ADDED Requirements

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
profiles. A profile SHALL accept only canonical UTF-8 JSON bytes with fixed key
order, fixed label, exact state/message/color dictionary, bounded non-negative
counts or the exact unknown marker, and a fixed maximum size. `headline-only/v1`
SHALL disclose only the four headline values and fixed rendering fields;
`headline-plus-freshness/v1` SHALL additionally disclose only `verified_at` and
`valid_until`. A profile SHALL reject duplicate keys, extra fields, arbitrary
text, URLs, headers, errors, SVG metadata, source identity, SHA, PR/run data,
or uncanonicalized whitespace/escapes. A transport SHALL reject invalid bytes
rather than sanitize or rewrite them after digest verification.

#### Scenario: Canonical ready bytes are accepted
- **WHEN** a producer supplies bytes emitted by the canonical projector for an
  approved disclosure profile
- **THEN** profile validation accepts the exact bytes and their digest
- **AND** the transport does not derive Gate, Health, counts, or color itself

#### Scenario: Covert disclosure is rejected
- **WHEN** a payload includes an extra key, duplicate key, noncanonical escape,
  source URL, header value, or arbitrary message text
- **THEN** profile validation rejects it before any public write

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
The Relay SHALL accept a publication operation only from an approved registry
entry bound to immutable repository and owner identifiers, permitted event/ref,
approved exact workflow path and workflow commit SHA, disclosure profile, and
destination. It SHALL verify GitHub OIDC issuer, audience, supported algorithm,
key identifier, signature, time claims with bounded skew, and token size before
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

### Requirement: Lifecycle changes have revocation precedence and recovery is authoritative
The Relay SHALL implement register, prepare, publish, renew, invalidate, read,
revoke, and recover operations with atomic generation and revocation-epoch
checks. Revoke, delete, visibility/consent loss, ownership transfer, workflow
pin rotation, and uninstall SHALL take precedence over delayed ready data.
Tombstoned aliases SHALL not be reassigned to another tenant without an
explicit safe registration procedure. Recovery after backup restore SHALL
require an approved current publisher proof and SHALL not revive a previous
ready payload or manufacture a current `main` result.

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
as a strict freshness guarantee. Cache headers, ETag/304, and HEAD responses
SHALL be bounded so an origin never represents expired ready data as current;
already cached browser, Camo, or offline copies SHALL not be claimed revocable.
The versioned Relay bundle, configuration, profiles, publisher, and released CLI
SHALL declare a compatibility matrix and be distributed only through the
existing release authority.

#### Scenario: Cached renderer is distinguished from origin truth
- **WHEN** a cached image remains visible after `valid_until`
- **THEN** documentation identifies it as an uncontrollable cached copy
- **AND** a new origin read returns no ready representation after expiry

#### Scenario: Unknown plan or bundle is not accepted implicitly
- **WHEN** a configuration names an unknown schema, disclosure profile, relay
  bundle, or compatibility plan
- **THEN** setup and publication reject it before registering or updating a
  public destination
