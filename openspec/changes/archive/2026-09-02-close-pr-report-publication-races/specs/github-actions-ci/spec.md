## ADDED Requirements

### Requirement: Architecture report Health uses a canonical baseline and schema guard

The read-only architecture report producer SHALL pass a valid baseline to the required Health CLI
input. When the current producer worktree lacks `architecture/baseline.arch.yml`, it SHALL create a
canonical empty version-3 baseline only in runner temporary storage and SHALL not commit or upload
that baseline as repository state. After Health runs, the producer SHALL require a parseable
`architecture-health/v1` document before it invokes `report pr` or uploads a report artifact.

#### Scenario: No repository baseline still produces a canonical Health artifact

- **WHEN** the current producer worktree has no `architecture/baseline.arch.yml`
- **THEN** the producer supplies an ephemeral canonical empty baseline to Health
- **AND** it can render the report from a valid `architecture-health/v1` response

#### Scenario: Health command-error JSON does not become a report input

- **WHEN** the Health command emits malformed JSON or a JSON envelope without
  `schema_id: architecture-health/v1`
- **THEN** the producer fails before it invokes `report pr`
- **AND** it does not upload a manifest claiming a canonical report
