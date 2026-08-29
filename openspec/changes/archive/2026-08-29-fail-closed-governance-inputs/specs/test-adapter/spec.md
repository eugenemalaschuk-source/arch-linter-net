## ADDED Requirements

### Requirement: Testing exposes typed assessment completion
`ArchitectureValidationResult` SHALL expose the typed shared assessment
completion state and its deterministic reason/provenance evidence. `ShouldPass`
for a valid-but-unassessable result SHALL identify assessment insufficiency and
its stable reasons without inventing ordinary architecture violations.

#### Scenario: Test asserts missing required evidence
- **WHEN** a mapped validation outcome is unassessable because required
  evidence is missing
- **THEN** a test can inspect that completion state and reason through
  `ArchitectureValidationResult` without parsing formatted CLI output
