## ADDED Requirements

### Requirement: Package contracts support bounded source subtraction
The system SHALL allow package dependency sources resolved by the configured source model to be subtracted before package evaluation.

#### Scenario: Excluded source is not checked
- **WHEN** a package contract excludes a resolved source
- **THEN** the system SHALL not produce a package finding for it

