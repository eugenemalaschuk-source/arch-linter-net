## ADDED Requirements

### Requirement: Semantic coverage exclusions reject unknown keys
Before permissive YAML deserialization, the loader SHALL reject an unknown field in a semantic-role coverage exclusion mapping.

#### Scenario: Misspelled semantic exclusion metadata key
- **WHEN** a semantic-role coverage exclusion contains `metdata` instead of `metadata`
- **THEN** policy loading fails with a diagnostic naming the unknown key
- **AND** the exclusion is not interpreted as a role-wide exclusion
