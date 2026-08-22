## ADDED Requirements

### Requirement: Testing consumers can assert normalized remediation metadata
The Testing adapter SHALL expose each finding's optional typed remediation hint through its existing normalized `Findings` collection, without requiring a caller to parse Human, JSON, or SARIF output.

#### Scenario: Test assertion reads a declared port hint
- **WHEN** a Testing validation result contains a port-boundary finding with an approved expected seam
- **THEN** a caller can inspect the finding's remediation category, canonical identity, evidence, expected seam, and review requirement directly
