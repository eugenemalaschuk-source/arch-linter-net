## ADDED Requirements

### Requirement: Policy location JSON has one optional-field shape
The system SHALL omit optional policy location fields when values are absent in both ordinary and exception JSON output.

#### Scenario: Root policy exception has no import metadata
- **WHEN** a root policy exception has no contract or import fields
- **THEN** its JSON location omits those fields rather than serializing null
