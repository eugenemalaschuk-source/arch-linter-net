## ADDED Requirements

### Requirement: Policy command dispatch and help
The CLI SHALL expose a `policy check` command with `--policy` and documented human, JSON, and SARIF output options. Its help text SHALL state that it performs no build or architecture compliance validation.

#### Scenario: User inspects help
- **WHEN** a user runs `arch-linter-net policy check --help`
- **THEN** the available options and assembly-free boundary are displayed deterministically
