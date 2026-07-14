## ADDED Requirements

### Requirement: Policy loading failures have format-aware output
The CLI SHALL format typed policy import and validation failures according to
the selected output format. Human output SHALL include policy source and root
context; JSON output SHALL emit an `architecture_policy_error` object with
`policy_location`, `related_policy_locations`, and `import_chain`; SARIF output
SHALL emit a result with policy physical and related locations when available.

#### Scenario: JSON policy validation failure
- **WHEN** the CLI is invoked with JSON output and an imported effective-policy
  value fails validation
- **THEN** stdout contains an `architecture_policy_error` object with the
  fragment policy location and ordered import chain

#### Scenario: SARIF policy import failure
- **WHEN** the CLI is invoked with SARIF output and a typed import failure has
  a source location
- **THEN** stdout contains a SARIF result whose physical location identifies
  that policy source
