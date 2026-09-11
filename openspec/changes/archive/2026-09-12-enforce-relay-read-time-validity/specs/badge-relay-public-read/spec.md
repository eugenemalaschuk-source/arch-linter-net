## Purpose

Defines the fail-closed public read and fixed rendering contract for a Relay
publication whose current validity is bounded by canonical evidence.

## ADDED Requirements

### Requirement: Relay reads enforce publication validity at request time
The Relay SHALL evaluate the trusted UTC clock before serving a public GET,
HEAD, or conditional request. It SHALL serve the exact stored canonical ready
JSON only when the state is ready and `now < valid_until`. Missing, malformed,
unsupported, corrupt, expired, revoked, recovery-required, or storage-uncertain
state SHALL produce only the approved fixed unavailable representation and
SHALL NOT extend a lease, revalidate a source, or reuse the previous ready
payload.

#### Scenario: A stopped publisher cannot leave a ready origin response
- **WHEN** a ready publication receives no further publish, renewal, or
  scheduler activity and the trusted clock reaches its validity boundary
- **THEN** a subsequent public GET returns the approved unavailable response
- **AND** it does not return the previous ready bytes

#### Scenario: Conditional and head reads cannot preserve expired readiness
- **WHEN** an expired or revoked publication receives a matching
  `If-None-Match` conditional request or a HEAD request
- **THEN** the Relay evaluates expiry before ETag handling
- **AND** it does not return a ready 304 or ready headers

### Requirement: Relay public representations preserve disclosure boundaries
The Relay SHALL return byte-identical stored canonical JSON for a ready
compatibility response and SHALL return only the approved fixed unavailable
JSON when non-ready. Its default SVG response SHALL be rendered from a fixed
local template using bounded escaped approved values, contain a readable
absolute UTC validity boundary for a ready freshness profile, and SHALL NOT
contain payload-derived markup, scripts, event handlers, foreign objects,
links, external assets, hidden metadata, or private provenance.

#### Scenario: Canonical ready JSON remains byte-identical
- **WHEN** a valid ready publication is requested through the JSON
  compatibility route
- **THEN** its response body exactly equals the stored canonical payload bytes
- **AND** the Relay does not add freshness or receipt fields to that body

#### Scenario: Adversarial input cannot become SVG behavior or disclosure
- **WHEN** a payload, alias, or stored state contains markup-like, oversized,
  Unicode, or private-data injection input
- **THEN** the Relay rejects or fails closed according to state validation
- **AND** the public SVG remains fixed-template-only and safe

### Requirement: Relay cache validators are bounded by current state
The Relay SHALL set `Content-Type`, `X-Content-Type-Options: nosniff`, and
cache directives according to the selected representation. A ready response
SHALL use `public, max-age=N, must-revalidate` where `N` does not exceed the
remaining validity in whole seconds; non-ready and uncertain responses SHALL
use `no-store`. The ready ETag SHALL incorporate the publication generation,
state, profile, exact canonical bytes, and validity boundary so an identical
headline with a new lease has a distinct validator.

#### Scenario: New validity receipt changes the ready validator
- **WHEN** the same exact headline bytes are published in a later generation
  with a new valid lease
- **THEN** the later ready response has a different ETag
- **AND** its cache lifetime is bounded by the new remaining validity

#### Scenario: A storage failure never serves cached-ready origin bytes
- **WHEN** the Relay cannot determine registered state or publication validity
- **THEN** it returns a fixed unavailable response with `Cache-Control: no-store`
- **AND** it emits no previous ready ETag or ready cache directive
