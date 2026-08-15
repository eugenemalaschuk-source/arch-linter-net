## ADDED Requirements

### Requirement: Badge payload is available from the standard CLI
The repository's architecture-policy workflow SHALL use the native
`badge architecture-policy` CLI command as the strict-policy gate and payload
producer. The command SHALL be usable by other repositories without copying a
Python script.

#### Scenario: Workflow produces the payload through CLI
- **WHEN** the dedicated architecture-policy workflow runs
- **THEN** it invokes `badge architecture-policy` with the repository policy
- **AND** the workflow status and the command's payload represent the same
  strict-policy outcome
