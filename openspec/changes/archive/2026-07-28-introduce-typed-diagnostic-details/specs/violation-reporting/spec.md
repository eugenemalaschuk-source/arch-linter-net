## ADDED Requirements

### Requirement: JSON emits normalized versioned findings
Machine-readable diagnostic JSON SHALL emit the normalized finding envelope and its `details` discriminator while retaining documented legacy fields as derived compatibility fields during the deprecation window.

#### Scenario: Legacy and typed fields agree
- **WHEN** a dependency violation is rendered as JSON
- **THEN** `source`, `forbidden_namespace`, and `forbidden_references` agree with the normalized finding and the typed details are present without adapter-specific reconstruction

### Requirement: Human output is a normalized projection
Human-readable output SHALL be formatted from normalized findings and MAY change prose without discarding load-bearing evidence.

#### Scenario: Human output preserves composition evidence
- **WHEN** a composition finding has assembly, member, matched API, and occurrence evidence
- **THEN** the human projection identifies that evidence from the normalized details
