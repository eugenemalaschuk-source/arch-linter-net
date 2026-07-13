## ADDED Requirements

### Requirement: Semantic coverage includes metadata extraction failures
Semantic-role coverage summaries SHALL include every role-index metadata extraction failure as deterministic unknown evidence with its subject, metadata key, and failure reason.

#### Scenario: Metadata failure is visible in JSON and human summaries
- **WHEN** semantic classification cannot extract configured metadata for a type
- **THEN** the selected semantic coverage summary SHALL include the failure in its unknown evidence bucket
