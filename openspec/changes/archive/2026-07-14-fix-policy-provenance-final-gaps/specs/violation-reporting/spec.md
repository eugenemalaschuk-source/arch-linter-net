## ADDED Requirements

### Requirement: Policy exception location metadata matches CI diagnostics
The CLI SHALL include source ordinal and per-location import chain in every policy-exception JSON location using the established snake_case schema.

#### Scenario: Imported policy exception
- **WHEN** an imported policy exception is rendered as JSON
- **THEN** its location includes source_ordinal and import_chain
