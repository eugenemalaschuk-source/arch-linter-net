## MODIFIED Requirements

### Requirement: CLI returns correct exit codes
The CLI SHALL return exit code 0 when all contracts pass, exit code 1 when any contract fails, and exit code 2 on runtime errors (invalid arguments, missing file, policy parse error). An unrecognised top-level token or subcommand SHALL be treated as invalid input rather than successful command help.

#### Scenario: All contracts pass
- **WHEN** the CLI validates a policy with no violations
- **THEN** exit code SHALL be 0

#### Scenario: Violations found
- **WHEN** the CLI validates a policy with known violations in strict mode
- **THEN** exit code SHALL be 1

#### Scenario: Missing policy file
- **WHEN** the CLI is invoked with `--policy nonexistent.yml`
- **THEN** exit code SHALL be 2 and an error message SHALL be printed to stderr

#### Scenario: Invalid mode
- **WHEN** the CLI is invoked with `--mode invalid`
- **THEN** exit code SHALL be 2 and an error message SHALL be printed to stderr

#### Scenario: Unknown flag
- **WHEN** the CLI is invoked with an unrecognized flag
- **THEN** exit code SHALL be 2 and an error message SHALL be printed to stderr

#### Scenario: Unknown command token
- **WHEN** the CLI is invoked with an unrecognised top-level token or subcommand
- **THEN** it SHALL return exit code 2, write a stderr diagnostic naming that token, and direct the caller to `--help` for usage information
