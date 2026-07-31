## ADDED Requirements

### Requirement: Framework contracts support bounded source subtraction
The system SHALL allow framework-reference sources resolved by the configured source model to be subtracted before framework evaluation.

#### Scenario: Excluded source is not checked
- **WHEN** a framework contract excludes a resolved source
- **THEN** the system SHALL not produce a framework finding for it

