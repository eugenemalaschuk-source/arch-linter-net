# cli-command-dispatch Specification

## Purpose
Keep `ArchLinterNet.Cli` on a composed adapter architecture: `Program.cs` stays a thin bootstrap, command parsing is centralized in one CLI host, command registration is module-based rather than hard-coded in switchboards, and abstractions remain separated from infrastructure implementations.
## Requirements
### Requirement: Program.cs is entry point and composition bootstrap only
`ArchLinterNet.Cli`'s `Program.Main` SHALL contain only process entry point and service composition logic; it SHALL NOT contain option parsing, engine invocation, or output-formatting logic for any command.

#### Scenario: Program.cs contains no command implementation
- **WHEN** `src/ArchLinterNet.Cli/Program.cs` is inspected
- **THEN** its only executable logic is creating the CLI service graph and delegating command execution to a composed host

### Requirement: CLI parsing is centralized in a composed host
The CLI SHALL build its command tree through a dedicated host/factory layer rather than hand-rolled switch loops spread across static command classes.

#### Scenario: Adding a new command does not require new parsing logic in Program.cs
- **WHEN** a new top-level CLI command is added
- **THEN** its parser wiring is added through a command module
- **AND** `Program.cs` remains unchanged apart from bootstrap if no new services are needed

### Requirement: Top-level commands are registered through modules
Top-level CLI commands SHALL be contributed through per-command modules implementing shared command-module abstractions, and the CLI host/factory SHALL compose those modules as a collection instead of naming each command in a hard-coded registration list.

#### Scenario: Adding a new top-level command does not require editing a central command list
- **WHEN** a new top-level CLI command module is added under `ArchLinterNet.Cli.Commands`
- **THEN** the CLI composition can discover and compose that module without editing `Program.cs`, `CliRootCommandFactory`, or another hard-coded top-level command registry

### Requirement: Command execution is instance-based
Every top-level CLI command (`validate`, `graph`, `explain`, `baseline`, `public-api`) SHALL execute through instance handler classes that receive runtime services through constructors. The CLI SHALL NOT depend on a static `CliEngine`, static service locator, or static command `Run(...)` methods as its primary execution seam.

#### Scenario: Command handlers can be resolved from composition
- **WHEN** the CLI service collection is built in a test
- **THEN** a test can resolve the CLI host, command definitions, and command handlers from that container without touching process-global state

#### Scenario: Handler behavior can be tested with fakes
- **WHEN** a command handler is constructed with fake console, filesystem, and runtime services
- **THEN** the handler can be executed and asserted without spawning a process or relying on static singletons

### Requirement: The baseline subcommand family remains one command surface
The `baseline` command SHALL continue to expose `generate`, `update`, `prune`, `diff`, and `verify` as one command family, even though each subcommand's execution is handled by its own instance handler.

#### Scenario: Baseline subcommands dispatch through dedicated handlers
- **WHEN** `arch-linter-net baseline` is invoked with any of `generate`, `update`, `prune`, `diff`, or `verify`
- **THEN** the CLI host dispatches to the matching baseline subcommand handler without relying on static command methods

### Requirement: Baseline subcommands are extension modules
The `baseline` command family SHALL compose its subcommands through dedicated subcommand modules rather than one monolithic definition file that hard-codes every subcommand's registration, help text wiring, and option graph in one place.

#### Scenario: Adding a baseline subcommand does not require editing a giant baseline switchboard
- **WHEN** a new `baseline <subcommand>` capability is added
- **THEN** it is introduced as a new baseline subcommand module
- **AND** existing baseline subcommand modules do not need to be edited just to register the new one

### Requirement: Command extraction preserves existing CLI behavior exactly
Reorganizing the CLI into a composed host plus instance handlers SHALL NOT change any command's accepted arguments, output (human, JSON, or SARIF), or exit codes.

#### Scenario: Existing CLI integration tests pass unchanged
- **WHEN** the existing `ArchLinterNet.Cli.Tests` black-box process-invocation integration test suite is run against the instance-based CLI architecture
- **THEN** every test SHALL pass without modification to its expected output or exit-code assertions

### Requirement: The command namespace stays covered by the existing self-policy without new rules
`ArchLinterNet.Cli`, its command-handler namespaces, and its CLI composition layer SHALL remain classified under the existing architecture-policy `cli` layer and SHALL remain subject to the existing `cli-must-use-validation-application-seam` rule, without requiring a new `architecture/dependencies.arch.yml` layer or rule entry.

#### Scenario: Self-policy lint passes without a policy edit
- **WHEN** `make lint-architecture` (the `self-architecture-policy` strict test) runs after the CLI host, composition layer, and instance handlers are introduced
- **THEN** it passes with `architecture/dependencies.arch.yml` unchanged

### Requirement: CLI abstractions are separated from implementations
CLI abstractions for console, filesystem, runtime, and command-module contracts SHALL live separately from their infrastructure implementations so contributors do not reintroduce mixed abstraction/implementation files in the same location.

#### Scenario: Abstractions do not share a folder with infrastructure implementations
- **WHEN** the CLI project structure is inspected
- **THEN** abstraction files are stored separately from concrete infrastructure files such as console, filesystem, runtime, and host implementations

### Requirement: The public-api subcommand family is one command surface
The CLI SHALL expose a `public-api` top-level command with `capture`, `diff`, `update`, and `migrate` subcommands, registered through a command module like every other top-level command, with each subcommand executed by its own instance handler.

#### Scenario: public-api subcommands are reachable
- **WHEN** `arch-linter-net public-api capture`, `diff`, `update`, or `migrate` is parsed
- **THEN** the corresponding subcommand handler SHALL be invoked

#### Scenario: public-api is discovered as a top-level module
- **WHEN** the CLI root command is composed
- **THEN** the `public-api` command SHALL appear without any hard-coded top-level command list being edited

#### Scenario: Unknown public-api subcommand reports usage
- **WHEN** an unrecognized `public-api` subcommand or option is supplied
- **THEN** the CLI SHALL return exit code 2 and print a usage hint naming `arch-linter-net public-api --help`

### Requirement: Schema discovery is a composed command module
The CLI SHALL expose offline schema registry discovery through a top-level command module and instance handler that receive the packaged-schema registry through the existing Core composition seam. The command SHALL not read repository-relative schema paths or perform network, restore, build, or target-assembly operations.

#### Scenario: Schema command is composed as a module
- **WHEN** the CLI root command is built
- **THEN** the schema discovery command is registered by a command module without a hard-coded top-level command list

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

### Requirement: The policy context command is an instance-based policy subcommand
The CLI SHALL expose `arch-linter-net policy context` under the existing
`policy` command family. The subcommand SHALL dispatch through an instance
handler and the composed Core runtime seam; it SHALL not add another executable
or a static command path.

#### Scenario: Policy context is reachable through the existing executable
- **WHEN** a user invokes `arch-linter-net policy context --policy <path>`
- **THEN** the CLI exports the selected effective policy context without adding
  a top-level executable or changing normal validation dispatch

### Requirement: The policy context command supports deterministic JSON and Markdown
The `policy context` command SHALL support `--format json` and
`--format markdown`, defaulting to Markdown. It SHALL write exactly one
rendered context document to standard output and return exit code 0 on success;
invalid arguments or policy-loading failures SHALL return exit code 2 using the
existing human/JSON diagnostic conventions.

#### Scenario: JSON command output is consumable by automation
- **WHEN** a user invokes `arch-linter-net policy context --format json`
- **THEN** standard output is one parseable versioned JSON context document

#### Scenario: Help distinguishes context export from validation
- **WHEN** a user requests `arch-linter-net policy context --help`
- **THEN** the help text states that the command summarizes effective policy
  context and does not validate projects, assemblies, or architecture results

### Requirement: Policy weakening comparison is an instance-based policy subcommand

The CLI SHALL expose `arch-linter-net policy weakening` through the existing
policy command module and an instance handler.  It SHALL accept explicit base
and current policy-context artifact paths and `human`, `json`, or `sarif`
format selection; it SHALL not read or simulate a historical policy from the
current working tree.

#### Scenario: Explicit contexts produce a guardrail result
- **WHEN** a caller invokes `policy weakening` with valid base and current
  context artifacts
- **THEN** the instance handler renders the normalized Core comparison result
  and returns a failing exit code only for configured error-severity findings

#### Scenario: Invalid artifact fails closed
- **WHEN** either supplied context artifact is missing, malformed, incomplete,
  or incompatible
- **THEN** the command returns exit code 2 with existing human/JSON error
  conventions and does not report a clean result

### Requirement: Architecture debt gate is an instance-based command module
The CLI SHALL expose `arch-linter-net gate` through a dedicated composed command module and instance handler. It SHALL require explicit policy and baseline paths, accept an optional complete pair of base/current policy-context artifacts, and support the existing full-analysis build-state selectors plus Human, JSON, and SARIF output.

#### Scenario: Gate is reachable without changing validation commands
- **WHEN** a caller invokes `arch-linter-net gate --policy policy.yml --baseline baseline.yml`
- **THEN** the composed CLI invokes the gate handler and existing `validate`, `baseline`, and `policy weakening` command behavior remains unchanged

#### Scenario: Incomplete weakening inputs are rejected
- **WHEN** a caller supplies only `--base-context` or only `--current-context`
- **THEN** the command returns exit code 2 with an actionable invalid-input diagnostic

