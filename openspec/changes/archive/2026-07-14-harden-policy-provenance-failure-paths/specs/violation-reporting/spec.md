## ADDED Requirements

### Requirement: Root and raw policy failures use format-aware typed output
CLI JSON and SARIF output SHALL recognize typed root parsing and raw composed
YAML validation failures as policy diagnostics. These failures SHALL not fall
through to the generic runtime-error output path.

#### Scenario: Malformed root renders as JSON policy error
- **WHEN** JSON validation loads a malformed selected root
- **THEN** stdout contains an `architecture_policy_error` with a root-role
  `policy_location`

#### Scenario: Imported raw failure renders as SARIF policy error
- **WHEN** SARIF validation loads an imported raw YAML value rejected before
  deserialization
- **THEN** the SARIF result identifies the fragment policy location

