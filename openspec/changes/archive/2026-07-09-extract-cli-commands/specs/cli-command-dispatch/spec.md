## ADDED Requirements

### Requirement: Program.cs performs argument-based dispatch only
`ArchLinterNet.Cli`'s `Program.Main` SHALL contain only argument-based dispatch to a command class's `Run` method; it SHALL NOT contain option parsing, engine invocation, or output-formatting logic for any command.

#### Scenario: Program.cs contains no command implementation
- **WHEN** `src/ArchLinterNet.Cli/Program.cs` is inspected
- **THEN** its only executable logic is inspecting `args[0]` and delegating to `ValidateCommand.Run`, `GraphCommand.Run`, `ExplainCommand.Run`, or `BaselineCommand.Run`

### Requirement: Each CLI command is implemented by a dedicated class under ArchLinterNet.Cli.Commands
Every top-level CLI command (`validate`, `graph`, `explain`, `baseline`) SHALL be implemented by its own class in the `ArchLinterNet.Cli.Commands` namespace, exposing a `Run(string[] args)` entry point, rather than as a method on `Program`.

#### Scenario: Adding a new command does not touch Program.cs's existing command logic
- **WHEN** a new top-level CLI command is added
- **THEN** its implementation is a new class under `ArchLinterNet.Cli.Commands`
- **AND** `Program.cs` is only extended with one new dispatch branch calling that class's `Run`

#### Scenario: The baseline subcommand family is one command unit
- **WHEN** `arch-linter-net baseline` is invoked with any of `generate`, `update`, `prune`, `diff`, or `verify`
- **THEN** `BaselineCommand.Run` dispatches to the matching subcommand handler within that same class

### Requirement: Command extraction preserves existing CLI behavior exactly
Reorganizing command implementations into `ArchLinterNet.Cli.Commands` classes SHALL NOT change any command's accepted arguments, output (human, JSON, or SARIF), or exit codes.

#### Scenario: Existing CLI integration tests pass unchanged
- **WHEN** the existing `ArchLinterNet.Cli.Tests` black-box process-invocation integration test suite is run against the extracted command classes
- **THEN** every test SHALL pass without modification to its expected output or exit-code assertions

### Requirement: The command namespace stays covered by the existing self-policy without new rules
`ArchLinterNet.Cli.Commands` SHALL be classified under the same architecture-policy `cli` layer as `ArchLinterNet.Cli` (namespace-prefix match), and SHALL remain subject to the existing `cli-must-use-validation-application-seam` rule, without requiring a new `architecture/dependencies.arch.yml` layer or rule entry.

#### Scenario: Self-policy lint passes without a policy edit
- **WHEN** `make lint-architecture` (the `self-architecture-policy` strict test) runs after `ArchLinterNet.Cli.Commands` is introduced
- **THEN** it passes with `architecture/dependencies.arch.yml` unchanged
