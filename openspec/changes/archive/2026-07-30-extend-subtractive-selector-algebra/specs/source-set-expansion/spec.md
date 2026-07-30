## ADDED Requirements

### Requirement: Source expansion supports bounded subtraction
The system SHALL allow compatible source-scoped contracts to subtract explicit sources or resolved source sets after ordered source expansion, without adding inputs beyond the declared source universe.

#### Scenario: Excluded expanded source creates no instance
- **WHEN** a source is resolved by an included source set and an exclusion
- **THEN** expansion SHALL not create an instance for that source and SHALL retain authored provenance for the exclusion evidence

