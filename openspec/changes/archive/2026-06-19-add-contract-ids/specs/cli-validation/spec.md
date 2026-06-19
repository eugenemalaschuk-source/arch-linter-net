## ADDED Requirements

### Requirement: CLI accepts --contract flag for selective contract execution
The CLI SHALL accept a `--contract` flag that specifies one or more contract IDs to execute. The flag MAY be specified multiple times. When specified, only contracts with matching IDs SHALL be validated.

#### Scenario: Single --contract
- **WHEN** the CLI is invoked with `--contract my-rule`
- **THEN** only the contract with ID `my-rule` is validated

#### Scenario: Multiple --contract flags
- **WHEN** the CLI is invoked with `--contract rule-a --contract rule-b`
- **THEN** contracts with IDs `rule-a` and `rule-b` are both validated

#### Scenario: --contract with no matching contract
- **WHEN** the CLI is invoked with `--contract nonexistent`
- **THEN** exit code 2 is returned with a message listing unknown IDs and available IDs

#### Scenario: --contract combined with --mode
- **WHEN** the CLI is invoked with `--mode strict --contract core-rule`
- **THEN** only the strict contract with ID `core-rule` is validated, respecting the mode

#### Scenario: --contract with --mode audit
- **WHEN** the CLI is invoked with `--mode audit --contract audit-rule`
- **THEN** only the audit contract with ID `audit-rule` is validated
