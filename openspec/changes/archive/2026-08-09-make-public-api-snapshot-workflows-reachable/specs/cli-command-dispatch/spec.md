## ADDED Requirements

### Requirement: Public API operations expose explicit build preparation options

The CLI SHALL expose `--ensure-built` and `--no-restore` on every `public-api`
subcommand that captures a live surface (`capture`, `diff`, `update`, and
`migrate`) and SHALL forward their values to the corresponding typed Core
request. CLI help SHALL describe the supported receipt-backed workflow.

#### Scenario: Public API command accepts ensure-built
- **WHEN** a user invokes any live-surface public API subcommand with
  `--ensure-built`
- **THEN** parsing SHALL succeed and the subcommand handler SHALL forward an
  explicit ensure-built preparation request to Core

#### Scenario: Public API command documents preparation
- **WHEN** a user requests public-API command help
- **THEN** the help text SHALL identify `--ensure-built` as the supported way to
  prepare receipt-backed artifacts and SHALL describe `--no-restore`
