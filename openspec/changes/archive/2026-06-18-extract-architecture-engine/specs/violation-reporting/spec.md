## ADDED Requirements

### Requirement: Format violations for human-readable output
The system SHALL format violation lists as sorted bullet lines with format `- {SourceType} -> {ForbiddenNamespace}: {refs}`.

#### Scenario: Multiple violations
- **WHEN** a contract produces 3 violations
- **THEN** the formatted output contains 3 bullet lines sorted by `SourceType` then `ForbiddenNamespace`

#### Scenario: Empty violations
- **WHEN** a contract produces 0 violations
- **THEN** the formatted output is an empty string

### Requirement: Format violations for CI JSON artifacts
The system SHALL format violations as a JSON object with `kind = "architecture_violations"`, `contract` name, and `violations` array containing `source`, `forbidden_namespace`, and `forbidden_references`.

#### Scenario: JSON output structure
- **WHEN** a contract named `"my-contract"` produces 2 violations
- **THEN** the JSON contains `{"kind":"architecture_violations","contract":"my-contract","violations":[...]}`

### Requirement: Format cycles for human-readable output
The system SHALL format cycle lists as sorted bullet lines with format `- {cycle_path}`.

#### Scenario: Multiple cycles
- **WHEN** cycle detection finds 2 cycles
- **THEN** the formatted output contains 2 bullet lines sorted alphabetically

### Requirement: Format cycles for CI JSON artifacts
The system SHALL format cycles as a JSON object with `kind = "architecture_cycles"`, `contract` name, and `cycles` array of path strings.

#### Scenario: JSON cycle output
- **WHEN** a contract produces 1 cycle
- **THEN** the JSON contains `{"kind":"architecture_cycles","contract":"...","cycles":["A -> B -> A"]}`
