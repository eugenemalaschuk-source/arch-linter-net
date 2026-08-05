## ADDED Requirements

### Requirement: Ancestor build-file uncertainty is cache-ineligible
The system SHALL fail closed for ancestor `Directory.Build.props` or `Directory.Build.targets` inputs and their nested imports unless their complete evaluated dependency evidence is captured. A changed nested import SHALL not permit verified cache reuse.

#### Scenario: Nested ancestor import changes
- **WHEN** an ancestor build file imports another file whose identity is not fully captured
- **THEN** the project is not verified cache-eligible
