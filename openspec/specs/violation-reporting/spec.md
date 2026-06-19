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

## ADDED Requirements

### Requirement: Human output includes contract ID
The human-readable violation formatter SHALL prefix each violation line with the contract ID in square brackets when available.

#### Scenario: Violation with explicit ID
- **WHEN** a contract with `id: my-rule` produces a violation
- **THEN** the human output format is `[my-rule] [My Contract] SourceType -> ForbiddenNamespace: refs`

#### Scenario: Violation with fallback ID
- **WHEN** a contract has no explicit `id` and produces a violation
- **THEN** the human output includes `[<normalized-name>]` prefix using the fallback ID

### Requirement: JSON output includes contract_id
The JSON formatter SHALL include a `contract_id` field alongside the existing `contract` field.

#### Scenario: JSON violation with ID
- **WHEN** a contract with `id: my-rule` produces a violation
- **THEN** the JSON violation object contains `"contract_id": "my-rule"`

#### Scenario: JSON cycle with ID
- **WHEN** a cycle contract with `id: cycle-check` detects a cycle
- **THEN** the JSON cycle object contains `"contract_id": "cycle-check"`
