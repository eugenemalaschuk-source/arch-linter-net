# CLI Validation Specification

## Purpose
Defines the CLI's validation command: policy/mode/format flags, exit codes, help/version output, and packaging as a .NET tool.

## Requirements

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

The CLI SHALL accept a `--format` (or `-f`) flag with values `human`, `json`, or `sarif`. The `--format` flag SHALL select which format is written to stdout. The CLI SHALL additionally accept a repeatable `--report <format>=<destination>` flag to route additional formats to other destinations. Human format SHALL produce readable terminal output. JSON format SHALL produce structured JSON suitable for CI artifact capture. SARIF format SHALL produce a SARIF 2.1.0 document suitable for code-scanning viewers.

#### Scenario: Human output format
- **WHEN** the CLI is invoked with `--format human`
- **THEN** output SHALL be human-readable text with violation details per line

#### Scenario: JSON output format
- **WHEN** the CLI is invoked with `--format json`
- **THEN** output SHALL be a JSON object with `passed`, `mode`, `violations`, and `cycles` fields

#### Scenario: --json shortcut
- **WHEN** the CLI is invoked with `--json`
- **THEN** the behavior SHALL be identical to `--format json`

#### Scenario: SARIF output format
- **WHEN** the CLI is invoked with `--format sarif`
- **THEN** output SHALL be a valid SARIF 2.1.0 document representing the run's violations and cycles

#### Scenario: Invalid format still rejected
- **WHEN** the CLI is invoked with `--format xml`
- **THEN** exit code 2 SHALL be returned with an error message listing the valid values `human`, `json`, and `sarif`

#### Scenario: --format json with --report sarif
- **WHEN** the CLI is invoked with `--format json --report sarif=report.sarif`
- **THEN** JSON SHALL appear on stdout AND a SARIF document SHALL be written to `report.sarif`

### Requirement: CLI returns correct exit codes
The CLI SHALL return exit code 0 when all contracts pass, exit code 1 when any contract fails, and exit code 2 on runtime errors (invalid arguments, missing file, policy parse error). An unrecognised top-level token or subcommand SHALL be treated as invalid input rather than successful command help. The CLI SHALL perform this invalid-input validation across the complete argument vector before returning successful root help or version output.

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

#### Scenario: Help followed by unknown command input
- **WHEN** the CLI is invoked with `--help debt`
- **THEN** it SHALL return exit code 2, write a stderr diagnostic naming `debt`, and not write successful help output

#### Scenario: Version followed by unknown command input
- **WHEN** the CLI is invoked with `--version debt`
- **THEN** it SHALL return exit code 2, write a stderr diagnostic naming `debt`, and not write successful version output

#### Scenario: Help followed by unknown option input
- **WHEN** the CLI is invoked with `--help --bogus-flag`
- **THEN** it SHALL return exit code 2, write a stderr diagnostic naming `--bogus-flag`, and not write successful help output

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

### Requirement: Policy command dispatch and help
The CLI SHALL expose a `policy check` command with `--policy` and documented human, JSON, and SARIF output options. Its help text SHALL state that it performs no build or architecture compliance validation.

#### Scenario: User inspects help
- **WHEN** a user runs `arch-linter-net policy check --help`
- **THEN** the available options and assembly-free boundary are displayed deterministically

### Requirement: Validation JSON errors are authoritative

When a `validate` invocation has selected `--format json`, every owned configuration, policy, report-routing, or build-state-preflight termination path SHALL retain its existing single structured JSON document on stdout. The command SHALL retain its established exit code, and human-format behavior SHALL remain unchanged.

#### Scenario: Validation build-state failure is parseable JSON
- **WHEN** `validate --format json` is blocked by an owned build-state preflight failure
- **THEN** stdout parses as one JSON error document and the command retains its existing runtime-error exit code

### Requirement: Authoritative assessment completion preserves CLI exit categories
When a CLI command executes an authoritative governance assessment, it SHALL
map trusted completion `fail` to exit code `1` and valid-but-unassessable
completion to exit code `2`. Completion `pass` SHALL map to exit code `0` only
when the ordinary validation outcome also passed; an ordinary failed outcome
SHALL exit `1` even when transport-only completion evidence says `pass`. Exit
code `2` for unassessable evidence SHALL expose a stable typed completion status
and reason that distinguishes it from invalid invocation, invalid
policy/configuration, runtime, cancellation, and output-routing failures.
Non-gating/read-only commands SHALL not represent unassessable evidence as a
successful clean governance result.

#### Scenario: Unassessable authoritative validation exits two
- **WHEN** a valid authoritative validation request has a required control
  whose evidence is unexpectedly empty or missing
- **THEN** the command exits `2` and exposes the unassessable completion status
  and canonical reason instead of exit `0` or an ordinary violation exit `1`

#### Scenario: Completion pass cannot hide ordinary failure
- **WHEN** a valid authoritative assessment has transport-only completion
  evidence of `pass` but ordinary validation has a blocking violation
- **THEN** the command exits `1`, not `0`

#### Scenario: Completed formats retain machine-readable completion evidence
- **WHEN** a valid authoritative assessment completes with `unassessable`
- **THEN** its Human, JSON, and SARIF result formats expose equivalent stable
  completion status and reason evidence without emitting a synthetic ordinary
  architecture finding

#### Scenario: Invalid CLI token remains distinct
- **WHEN** a CLI invocation contains an unknown or malformed command token
- **THEN** the command exits `2` through its invalid-arguments path and does
  not claim valid-but-unassessable architecture evidence
