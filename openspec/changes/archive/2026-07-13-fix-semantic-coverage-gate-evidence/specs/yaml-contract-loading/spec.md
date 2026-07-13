## ADDED Requirements

### Requirement: Semantic exclusion metadata must be an object
The loader SHALL reject `metadata: null` in a semantic-role coverage exclusion before execution.

#### Scenario: Null exclusion metadata is rejected
- **WHEN** a semantic-role coverage exclusion declares `metadata: null`
- **THEN** loading fails with a null-metadata diagnostic
