## ADDED Requirements

### Requirement: Layer templates support container subtraction
The system SHALL allow template container selectors to subtract declared containers before expansion, preserving deterministic template/container provenance.

#### Scenario: Excluded container is not expanded
- **WHEN** a container matches a template include and exclusion
- **THEN** no concrete layer contract SHALL be created for that container

