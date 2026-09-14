## MODIFIED Requirements

### Requirement: OIDC publisher authentication fails closed

The Relay SHALL accept an initial publisher token only for the exact `push`
event and configured branch ref. A metadata-only renewal token MAY use
`schedule` only when the immutable registry entry explicitly allowlists that
event and reaches only the `renew` operation; all other mutation operations
require `push`. It SHALL still match the registered IDs, ref, exact workflow
path and SHA, audience, subject, and bounded time claims. It SHALL not trust token-
derived URLs, unapproved algorithms, unknown keys after a bounded protection
window and fixed-chain refresh, or missing/mismatched claims. Token and claim
values SHALL not be exposed in public responses, redirects, headers, or
telemetry. Unknown-key refresh work SHALL be coalesced for concurrent misses
and SHALL be suppressed during the protection window after a refresh attempt,
including a failed provider attempt.

#### Scenario: Pin mismatch is rejected before a write

- **WHEN** a signed token has a workflow reference or workflow SHA that
  differs from its registry entry
- **THEN** the Relay rejects the request before state mutation
- **AND** it returns only a redacted authorization failure

#### Scenario: Key outage fails closed

- **WHEN** the fixed JWKS endpoint cannot safely resolve the protected key
- **THEN** the Relay rejects the publisher request with a bounded failure
- **AND** it does not serve or create a new ready state

#### Scenario: Renewal event is allowlisted without weakening identity

- **WHEN** the registry explicitly allows `schedule` and a renewal token
  presents the registered identity and workflow binding
- **THEN** OIDC authentication accepts the event for metadata-only renewal
- **AND** a `schedule` token is rejected when the registry does not explicitly
  allow it or any other claim differs

#### Scenario: Concurrent unknown keys share one refresh

- **WHEN** concurrent invalid publisher requests present protected key IDs that
  are absent from the cached fixed GitHub JWKS set
- **THEN** they await at most one in-flight refresh of that fixed endpoint
- **AND** each unresolved key is rejected without a write

#### Scenario: Repeated unknown keys respect the protection window

- **WHEN** repeated invalid requests present unsupported or random key IDs
  during the fixed protection window after a refresh attempt
- **THEN** the Relay rejects them without another provider fetch
- **AND** after the window a later miss may perform one new fixed-endpoint
  refresh so provider rotation can be discovered

### Requirement: State capacity and storage failures are bounded

The Relay SHALL bound request bodies, tokens, payloads, manifests, outstanding
challenges, retained replay records, per-alias storage, and OIDC refresh
bookkeeping. SQLite Durable Object storage SHALL be the authoritative state
unit; storage, clock, or provider uncertainty SHALL fail closed with redacted
diagnostics. The Relay SHALL not accept arbitrary archive, callback, or network
URL input. OIDC refresh bookkeeping SHALL use only a bounded fixed-endpoint
cache, refresh timestamp, and single in-flight operation; it SHALL not retain
state keyed by an unbounded set of attacker-controlled key identifiers.

#### Scenario: Oversized input is rejected without partial state

- **WHEN** a publisher submits a body, token, payload, or manifest above its
  configured bound
- **THEN** the Relay rejects it before mutation
- **AND** it leaves no challenge, payload, or partial receipt in storage

#### Scenario: Many distinct invalid key IDs do not grow protection state

- **WHEN** requests present many distinct invalid protected key IDs
- **THEN** the Relay retains only the bounded fixed-endpoint JWKS state and
  refresh coordination metadata
- **AND** memory use does not grow once the provider key-set bound is reached

