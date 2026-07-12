## ADDED Requirements

### Requirement: Port-boundary diagnostics preserve seam evidence
Every port-boundary finding SHALL identify the source and target types, their
resolved role/metadata, evidence kind, expected seam, actual forbidden edge or
binding mismatch, and a safe remediation hint. Human and JSON output SHALL
preserve this information in deterministic form.

#### Scenario: JSON output distinguishes a direct edge from a binding mismatch
- **WHEN** JSON output contains both a forbidden direct reference and an
  adapter-to-port mismatch
- **THEN** each finding SHALL identify its evidence kind and expected seam so
  an AI consumer can distinguish the remediation
