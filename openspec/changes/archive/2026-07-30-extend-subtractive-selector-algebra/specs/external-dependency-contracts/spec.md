## ADDED Requirements

### Requirement: External contracts support bounded layer subtraction
The system SHALL allow external-dependency layer sources resolved by the configured source model to be subtracted before external reference evaluation.

#### Scenario: Excluded layer is not checked
- **WHEN** an external contract excludes a resolved layer source
- **THEN** the system SHALL not produce an external-dependency finding for it

