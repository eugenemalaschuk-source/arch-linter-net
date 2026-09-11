## ADDED Requirements

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
