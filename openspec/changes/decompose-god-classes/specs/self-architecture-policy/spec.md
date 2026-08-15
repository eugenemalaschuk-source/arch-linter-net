## ADDED Requirements

### Requirement: Production types are not handwritten partial aggregates

The repository self-policy SHALL first measure source declaration counts for production `src` types
in audit mode and, after migration, SHALL strictly require every governed production type to have at
most one handwritten source declaration. The strict rule SHALL not govern test fixtures that model
C# partial-type semantics.

#### Scenario: A new production partial aggregate fails the strict gate

- **WHEN** a production type is split across two handwritten source files after the strict rule is
  enabled
- **THEN** `make lint-architecture` fails with the type name and both declaration paths

#### Scenario: A partial-language test fixture remains analyzable

- **WHEN** a test fixture deliberately declares one type across multiple source files
- **THEN** the production declaration-count rule does not report that fixture
- **AND** the source-file index continues to expose its ambiguity semantics for its dedicated tests

### Requirement: Direct CLI command modules are independent

The repository self-policy SHALL strictly forbid direct first-party namespace dependencies between
distinct immediate command modules under `ArchLinterNet.Cli.Commands`. The policy SHALL retain a
reviewed inventory of command layers, so a newly introduced command cannot silently bypass the
boundary. The generic `Abstractions`, `Models`, and `Exceptions` conventions SHALL remain
recursive and apply within each command without per-command duplication.

#### Scenario: A command references a sibling command implementation

- **WHEN** a type in `ArchLinterNet.Cli.Commands.Baseline` references a type in
  `ArchLinterNet.Cli.Commands.PublicApi`
- **THEN** `make lint-architecture` fails with the source and target command modules

#### Scenario: A command uses a top-level CLI abstraction

- **WHEN** a command references a type in `ArchLinterNet.Cli.Abstractions`
- **THEN** the command-independence contract does not report a violation

#### Scenario: A direct command folder is added

- **WHEN** a new direct folder with a command module is added below `ArchLinterNet.Cli.Commands`
- **THEN** the self-policy inventory regression fails until the command is added to the reviewed
  independence contract
