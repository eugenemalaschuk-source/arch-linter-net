## MODIFIED Requirements

### Requirement: Markdown coverage report generation
A native `coverage report` CLI command SHALL convert strict JSON's
`coverage_summary` and coverage findings into a Markdown report containing
overall status (pass/fail) and covered/excluded/uncovered/stale/unknown counts.

#### Scenario: Zero findings produces a clean pass report
- **WHEN** the CLI report command receives strict JSON with empty
  `coverage_findings` and all `coverage_summary` counts at zero
- **THEN** the generated Markdown reports a passing status and all counts as
  `0`

#### Scenario: Failed gate is reflected in the report
- **WHEN** the strict JSON has `passed: false`
- **THEN** the generated Markdown reports a failing status
