## ADDED Requirements

### Requirement: CLI can write a current change snapshot from Health's complete analysis
The CLI SHALL support an optional `health --change-snapshot <path>` output.
When supplied, Health SHALL write a complete
`architecture-change-snapshot/v2` for the current candidate from the same
immutable analysis session that produced the Health validation and debt-gate
receipts. The output SHALL use the canonical Core projector and serializer
and SHALL be written only after validation, graph projections, and required
baseline comparison succeed.

#### Scenario: Health publishes a shared current snapshot
- **WHEN** `health --change-snapshot <path>` completes with an assessable
  Health result
- **THEN** the path contains a complete deserializable current change snapshot
- **AND** normal Health output and exit category remain canonical

#### Scenario: Shared snapshot publication does not create false green
- **WHEN** the shared projection fails, is cancelled, or cannot verify required
  baseline candidates
- **THEN** the command returns a non-success runtime/preparation result
- **AND** it does not publish a partial or synthetic clean snapshot

#### Scenario: Standalone change snapshot remains compatible
- **WHEN** `change snapshot` is invoked without the Health composite option
- **THEN** its option validation, build-state preparation, projection semantics,
  serialized schema, and output behavior remain unchanged
