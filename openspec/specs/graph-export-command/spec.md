# graph-export-command Specification

## Purpose
TBD - created by archiving change add-dependency-graph-export-and-explain. Update Purpose after archive.
## Requirements
### Requirement: `graph` CLI verb exports the dependency graph
The system SHALL provide a `graph` CLI verb that builds the dependency graph for a given policy and prints it in the requested format. The command SHALL accept `--policy`, `--mode`, `--level` (`namespace` default, `type`, `assembly`), `--format` (`json` default, `dot`, `mermaid`), `--condition-set`, and `--contract` options, mirroring the option names already used by the `validate` command where applicable.

#### Scenario: Default invocation exports namespace-level JSON
- **WHEN** a user runs `arch-linter-net graph --policy <path>` with no `--level`/`--format`
- **THEN** the command prints a JSON document with `namespace`-level nodes and edges

#### Scenario: Explicit level and format selection
- **WHEN** a user runs `arch-linter-net graph --policy <path> --level type --format dot`
- **THEN** the command prints a Graphviz `digraph` document built from type-level nodes and edges

### Requirement: `graph` command is not pass/fail
The `graph` command SHALL exit `0` when the graph is built and printed successfully, regardless of whether the underlying policy has contract violations. It SHALL exit `2` only on a runtime error (invalid arguments, unreadable policy file, invalid `--level`/`--format` value).

#### Scenario: Successful export with violations present
- **WHEN** the policy has one or more contract violations but the graph is built and printed successfully
- **THEN** the `graph` command exits with code `0`

#### Scenario: Invalid level value
- **WHEN** a user runs `arch-linter-net graph --level bogus`
- **THEN** the command prints an error to stderr and exits with code `2`

### Requirement: `graph` command does not change `validate`/`baseline` behavior
Adding the `graph` verb SHALL NOT alter the exit codes, output, or option parsing of the existing no-verb `validate` command or the `baseline` verb.

#### Scenario: Validate command unaffected
- **WHEN** `arch-linter-net` is invoked with the same arguments as before this change (no `graph`/`explain` verb)
- **THEN** its output and exit code are unchanged from prior behavior

### Requirement: JSON export format
The `--format json` output SHALL be a structured document containing a `nodes` array (each with `id` and `kind`) and an `edges` array (each with `source`, `target`, `sourceKind`, `targetKind`, and `contractIds`), matching the deterministic ordering defined by the dependency graph model.

#### Scenario: JSON output is well-formed and ordered
- **WHEN** `arch-linter-net graph --format json` is run
- **THEN** the output parses as JSON with `nodes` and `edges` arrays in the deterministic order defined by the graph model

### Requirement: DOT export format
The `--format dot` output SHALL be a valid Graphviz `digraph` document where each edge is rendered as `"source" -> "target"`, annotated with a `label` containing the edge's contract IDs (joined) when present.

#### Scenario: Violating edge includes a label
- **WHEN** an edge has `ContractIds` containing `"no-infra-in-domain"`
- **THEN** the DOT output for that edge includes `label="no-infra-in-domain"`

### Requirement: Mermaid export format
The `--format mermaid` output SHALL be a valid `graph TD` Mermaid document representing the same nodes and edges as the JSON export.

#### Scenario: Mermaid output renders edges
- **WHEN** `arch-linter-net graph --format mermaid` is run
- **THEN** the output begins with `graph TD` and contains one line per edge connecting source and target node identifiers

