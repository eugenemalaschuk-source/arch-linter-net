## ADDED Requirements

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
