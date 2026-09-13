## MODIFIED Requirements

### Requirement: OIDC publisher authentication fails closed

The Relay SHALL accept an initial publisher token only for the exact `push`
event and configured branch ref. A metadata-only renewal token MAY use
`schedule` only when the immutable registry entry explicitly allowlists that
event; it SHALL still match the registered IDs, ref, exact workflow path and
SHA, audience, subject, and bounded time claims. It SHALL not trust token-
derived URLs, unapproved algorithms, unknown keys after one fixed-chain
refresh, or missing/mismatched claims. Token and claim values SHALL not be
exposed in public responses, redirects, headers, or telemetry.

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
