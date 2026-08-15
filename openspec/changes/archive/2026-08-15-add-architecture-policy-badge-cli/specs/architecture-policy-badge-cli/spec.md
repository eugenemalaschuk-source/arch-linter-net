## ADDED Requirements

### Requirement: Native architecture-policy badge command
The CLI SHALL expose `badge architecture-policy`, which SHALL execute complete
strict validation and write a Shields endpoint JSON object to stdout with
`schemaVersion`, `label`, `message`, and `color` fields. The label SHALL be
`architecture policy`; a passing strict result SHALL use `passing` and
`brightgreen`, and a strict-policy failure SHALL use `failing` and `red`.

#### Scenario: Strict policy passes
- **WHEN** a user runs `arch-linter-net badge architecture-policy` for a valid
  policy with no strict findings
- **THEN** stdout is a Shields endpoint JSON object with message `passing` and
  color `brightgreen`
- **AND** the command exits 0

#### Scenario: Strict policy fails
- **WHEN** a user runs the command for a policy with strict findings
- **THEN** stdout is a Shields endpoint JSON object with message `failing` and
  color `red`
- **AND** the command exits 1

#### Scenario: Validation cannot execute
- **WHEN** validation cannot execute because of invalid input or build-state
  failure
- **THEN** stdout is a Shields endpoint JSON object with message `unavailable`
  and color `red`
- **AND** the command exits 2

### Requirement: Native architecture-coverage report command
The CLI SHALL expose `coverage report`, which SHALL read strict CLI JSON and
render the deterministic architecture-coverage Markdown report to stdout or an
explicit output path. It SHALL support changed-files classification,
`--diff-status failed`, and `--max-failure-diagnostics` for the compact
PR-comment form.

#### Scenario: Full report renders from strict JSON
- **WHEN** a user runs `arch-linter-net coverage report --input
  architecture-strict.json`
- **THEN** the command renders the architecture-coverage status, failed-rule
  section when applicable, and covered/excluded/uncovered/stale/unknown totals

#### Scenario: Compact PR report preserves bounded diagnostics
- **WHEN** a user supplies `--max-failure-diagnostics 3`
- **THEN** each failed rule contains at most three representative diagnostics
- **AND** the report identifies omitted diagnostics

#### Scenario: Failed changed-files diff is explicit
- **WHEN** a user supplies `--diff-status failed`
- **THEN** the New-code coverage section reports the diff as unavailable
- **AND** it does not report zero changed first-party files
