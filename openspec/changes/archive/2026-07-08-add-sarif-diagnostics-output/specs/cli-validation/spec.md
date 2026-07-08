## MODIFIED Requirements

### Requirement: CLI supports human and JSON output formats
The CLI SHALL accept a `--format` (or `-f`) flag with values `human`, `json`, or `sarif`. Human format SHALL produce readable terminal output. JSON format SHALL produce structured JSON suitable for CI artifact capture. SARIF format SHALL produce a SARIF 2.1.0 document suitable for code-scanning viewers.

#### Scenario: Human output format
- **WHEN** the CLI is invoked with `--format human`
- **THEN** output SHALL be human-readable text with violation details per line

#### Scenario: JSON output format
- **WHEN** the CLI is invoked with `--format json`
- **THEN** output SHALL be a JSON object with `passed`, `mode`, `violations`, and `cycles` fields

#### Scenario: --json shortcut
- **WHEN** the CLI is invoked with `--json`
- **THEN** the behavior SHALL be identical to `--format json`

#### Scenario: SARIF output format
- **WHEN** the CLI is invoked with `--format sarif`
- **THEN** output SHALL be a valid SARIF 2.1.0 document representing the run's violations and cycles

#### Scenario: Invalid format still rejected
- **WHEN** the CLI is invoked with `--format xml`
- **THEN** exit code 2 SHALL be returned with an error message listing the valid values `human`, `json`, and `sarif`
