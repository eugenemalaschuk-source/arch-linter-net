## ADDED Requirements

### Requirement: Coverage exposes solution-derived project set resolution
The shared coverage inventory SHALL expose deterministic resolved project-set and project-metadata
union evidence, including authored contract identity and selector provenance, for solution-derived
project source sets.

#### Scenario: Coverage records every authored source-set expansion identity
- **WHEN** rule-input coverage selects an authored directional assembly contract ID
- **THEN** coverage selects every derived instance and retains their authored and resolved-source
  identities
