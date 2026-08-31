## ADDED Requirements

### Requirement: Imported diagnostics use a typed detail subtype
The diagnostic model SHALL represent a governed imported static-analysis diagnostic as its own
sealed diagnostic subtype and discriminator. The subtype SHALL carry only imported source facts,
the selected fingerprint, and validated evidence provenance; it SHALL NOT claim a native
dependency, layer, or contract-family fact.

#### Scenario: Source evidence remains type-addressable
- **WHEN** a consumer receives a normalized imported finding
- **THEN** it can inspect the typed imported-diagnostic detail and its provenance without parsing
  human output, JSON text, or a foreign SARIF document
