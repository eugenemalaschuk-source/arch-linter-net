## ADDED Requirements

### Requirement: Layout conventions support subtractive file matching
The system SHALL allow a layout convention to subtract compatible file matchers from its included candidate files before evaluating CEL and layout expectations.

#### Scenario: Excluded file does not produce a layout finding
- **WHEN** a file matches both the configured include and exclude matchers
- **THEN** no layout convention finding SHALL be produced for that file

