## ADDED Requirements

### Requirement: Format violations and cycles as SARIF
The system SHALL format violations and cycles as a SARIF 2.1.0 `run`, with each violation or cycle rendered as a `result` referencing a `rule` keyed by contract ID.

#### Scenario: SARIF violation result
- **WHEN** a contract named `"my-contract"` with `id: my-rule` produces a violation
- **THEN** the SARIF output contains a result with `ruleId == "my-rule"` and a `message.text` describing the violation

#### Scenario: SARIF cycle result
- **WHEN** a cycle contract with `id: cycle-check` detects a cycle
- **THEN** the SARIF output contains a result with `ruleId == "cycle-check"` and a `message.text` containing the cycle path

### Requirement: Existing human and JSON formatting are unaffected
Adding SARIF formatting SHALL NOT change the output of `FormatViolationsForHumans`, `FormatCyclesForHumans`, or `FormatResultForCiArtifacts`.

#### Scenario: Human and JSON output unchanged
- **WHEN** the same violations and cycles are formatted for human and JSON output before and after the SARIF formatter is added
- **THEN** the human and JSON output is byte-identical
