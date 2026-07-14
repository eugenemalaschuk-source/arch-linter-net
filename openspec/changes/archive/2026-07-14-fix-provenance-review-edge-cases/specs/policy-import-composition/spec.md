## ADDED Requirements

### Requirement: Specialized provenance diagnostics preserve encounter order
The system SHALL order classification path and every other specialized provenance location by source ordinal and encounter ordinal.

#### Scenario: Classification path has double-digit indices
- **WHEN** one source has entries at indices 2 and 10
- **THEN** human and JSON diagnostics retain authored order

### Requirement: Expanded template provenance uses exact source identity
The system SHALL bind each generated layer-template contract to the authored template identified by its stable owner identity.

#### Scenario: Same-name templates have distinct IDs
- **WHEN** root and fragment templates share a name but have distinct explicit IDs
- **THEN** each generated contract reports its own authored source
