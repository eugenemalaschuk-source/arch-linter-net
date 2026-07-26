## MODIFIED Requirements

### Requirement: Command execution is instance-based
Every top-level CLI command (`validate`, `graph`, `explain`, `baseline`, `public-api`) SHALL execute through instance handler classes that receive runtime services through constructors. The CLI SHALL NOT depend on a static `CliEngine`, static service locator, or static command `Run(...)` methods as its primary execution seam.

#### Scenario: Command handlers can be resolved from composition
- **WHEN** the CLI service collection is built in a test
- **THEN** a test can resolve the CLI host, command definitions, and command handlers from that container without touching process-global state

#### Scenario: Handler behavior can be tested with fakes
- **WHEN** a command handler is constructed with fake console, filesystem, and runtime services
- **THEN** the handler can be executed and asserted without spawning a process or relying on static singletons

## ADDED Requirements

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
