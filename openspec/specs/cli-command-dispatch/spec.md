# cli-command-dispatch Specification

## Purpose
Keep `ArchLinterNet.Cli` on a composed adapter architecture: `Program.cs` stays a thin bootstrap, command parsing is centralized in one CLI host, and each command executes through instance handlers with explicit dependencies instead of static globals.
## Requirements
### Requirement: Program.cs is entry point and composition bootstrap only
`ArchLinterNet.Cli`'s `Program.Main` SHALL contain only process entry point and service composition logic; it SHALL NOT contain option parsing, engine invocation, or output-formatting logic for any command.

#### Scenario: Program.cs contains no command implementation
- **WHEN** `src/ArchLinterNet.Cli/Program.cs` is inspected
- **THEN** its only executable logic is creating the CLI service graph and delegating command execution to a composed host

### Requirement: CLI parsing is centralized in a composed host
The CLI SHALL build its command tree through a dedicated host/factory layer that owns parser configuration for `validate`, `baseline`, `graph`, and `explain`, rather than hand-rolled switch loops spread across static command classes.

#### Scenario: Adding a new command does not require new parsing logic in Program.cs
- **WHEN** a new top-level CLI command is added
- **THEN** its parser wiring is added in the CLI host/factory layer
- **AND** `Program.cs` remains unchanged apart from bootstrap if no new services are needed

### Requirement: Command execution is instance-based
Every top-level CLI command (`validate`, `graph`, `explain`, `baseline`) SHALL execute through instance handler classes that receive runtime services through constructors. The CLI SHALL NOT depend on a static `CliEngine`, static service locator, or static command `Run(...)` methods as its primary execution seam.

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
