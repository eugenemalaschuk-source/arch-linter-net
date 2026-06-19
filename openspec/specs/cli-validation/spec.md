## ADDED Requirements

### Requirement: CLI accepts --policy flag
The CLI SHALL accept a `--policy` (or `-p`) flag specifying the path to a YAML architecture contract file. If omitted, the default SHALL be `architecture/dependencies.arch.yml`.

#### Scenario: Default policy path
- **WHEN** the CLI is invoked with no arguments
- **THEN** it SHALL attempt to load `architecture/dependencies.arch.yml` from the current directory

#### Scenario: Custom policy path via --policy
- **WHEN** the CLI is invoked with `--policy /tmp/my-policy.yml`
- **THEN** it SHALL load the policy from `/tmp/my-policy.yml`

#### Scenario: Custom policy path via -p
- **WHEN** the CLI is invoked with `-p my-policy.yml`
- **THEN** it SHALL load the policy from `my-policy.yml`

### Requirement: CLI supports strict and audit modes
The CLI SHALL accept a `--mode` (or `-m`) flag with values `strict` or `audit`. In strict mode, only strict contracts SHALL be checked and violations SHALL return a non-zero exit code. In audit mode, audit contracts SHALL be checked and violations SHALL be reported as diagnostics.

#### Scenario: Strict mode via --mode strict
- **WHEN** the CLI is invoked with `--mode strict`
- **THEN** strict contracts SHALL be validated and violations SHALL produce exit code 1

#### Scenario: Audit mode via --mode audit
- **WHEN** the CLI is invoked with `--mode audit`
- **THEN** audit contracts SHALL be validated and violations SHALL produce exit code 1

#### Scenario: --strict shortcut
- **WHEN** the CLI is invoked with `--strict`
- **THEN** the behavior SHALL be identical to `--mode strict`

#### Scenario: --audit shortcut
- **WHEN** the CLI is invoked with `--audit`
- **THEN** the behavior SHALL be identical to `--mode audit`

### Requirement: CLI supports human and JSON output formats
The CLI SHALL accept a `--format` (or `-f`) flag with values `human` or `json`. Human format SHALL produce readable terminal output. JSON format SHALL produce structured JSON suitable for CI artifact capture.

#### Scenario: Human output format
- **WHEN** the CLI is invoked with `--format human`
- **THEN** output SHALL be human-readable text with violation details per line

#### Scenario: JSON output format
- **WHEN** the CLI is invoked with `--format json`
- **THEN** output SHALL be a JSON object with `passed`, `mode`, `violations`, and `cycles` fields

#### Scenario: --json shortcut
- **WHEN** the CLI is invoked with `--json`
- **THEN** the behavior SHALL be identical to `--format json`

### Requirement: CLI returns correct exit codes
The CLI SHALL return exit code 0 when all contracts pass, exit code 1 when any contract fails, and exit code 2 on runtime errors (invalid arguments, missing file, policy parse error).

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

### Requirement: CLI supports --help and --version
The CLI SHALL print usage information on `--help` or `-h`, and version on `--version` or `-v`. Both SHALL return exit code 0.

#### Scenario: --help
- **WHEN** the CLI is invoked with `--help`
- **THEN** usage information SHALL be printed to stdout and exit code SHALL be 0

#### Scenario: --version
- **WHEN** the CLI is invoked with `--version`
- **THEN** version string SHALL be printed to stdout and exit code SHALL be 0

### Requirement: CLI is installable as .NET local tool
The CLI project SHALL be configured with `<PackAsTool>true</PackAsTool>` and `<ToolCommandName>arch-linter-net</ToolCommandName>`. The repository SHALL include a `.config/dotnet-tools.json` manifest for local tool restore.

#### Scenario: dotnet tool restore
- **WHEN** a consumer runs `dotnet tool restore` in the repository root
- **THEN** the CLI SHALL be available as `dotnet arch-linter-net`

#### Scenario: Local tool invocation
- **WHEN** a consumer runs `dotnet arch-linter-net --help`
- **THEN** the tool SHALL respond identically to the `dotnet run` invocation

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
