## ADDED Requirements

### Requirement: Imported finding projections preserve source and trust provenance
Human, JSON, and SARIF projections of an imported finding SHALL present equivalent policy control,
canonical identity, original source diagnostic, selected fingerprint, and validated evidence
provenance. SARIF output SHALL emit an ordinary ArchLinterNet result at the available original
source location and SHALL NOT recursively embed the input SARIF document.

#### Scenario: JSON and SARIF provide the same drill-down facts
- **WHEN** one imported finding has source and evidence provenance
- **THEN** JSON and the normalized ArchLinterNet property in SARIF expose equivalent source/trust
  facts in deterministic order
