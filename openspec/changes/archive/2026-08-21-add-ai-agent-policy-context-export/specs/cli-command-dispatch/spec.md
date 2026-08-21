## ADDED Requirements

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
