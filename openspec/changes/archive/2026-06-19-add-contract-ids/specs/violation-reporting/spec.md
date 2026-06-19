## ADDED Requirements

### Requirement: Human output includes contract ID
The human-readable violation formatter SHALL prefix each violation line with the contract ID in square brackets when available.

#### Scenario: Violation with explicit ID
- **WHEN** a contract with `id: my-rule` produces a violation
- **THEN** the human output format is `[my-rule] SourceType -> ForbiddenNamespace: refs`

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
