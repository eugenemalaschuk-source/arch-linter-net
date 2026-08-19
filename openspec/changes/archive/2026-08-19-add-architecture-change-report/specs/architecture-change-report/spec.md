## ADDED Requirements

### Requirement: Complete architecture analysis can be persisted as a change snapshot
The system SHALL provide a versioned `architecture-change-snapshot/v1` artifact that is built from a complete authoritative architecture analysis and contains stable entries for observed namespaces, projects, assemblies, semantic roles and contexts, dependency edges, coverage blind spots, normalized findings, and baseline-debt identities. The artifact SHALL identify its analysis mode, policy inputs, and scope and SHALL use deterministic ordering.

#### Scenario: Snapshot retains complete analysis facts
- **WHEN** a user creates a snapshot for a policy in the supported analysis mode
- **THEN** the artifact contains its versioned authority metadata and sorted architecture surfaces
- **AND THEN** it does not claim to represent only changed files or projects

#### Scenario: Same-name assembly entities remain distinct
- **WHEN** two observed entities have the same type or namespace name in different assemblies
- **THEN** the snapshot assigns them different stable identities

### Requirement: Architecture snapshots are compared by stable identity
The system SHALL compare a base and current `architecture-change-snapshot/v1` artifact by typed, stable identity and SHALL report added and removed namespaces, projects, assemblies, semantic roles, contexts, dependency edges, and coverage blind spots. The result SHALL be deterministically ordered and SHALL reject incompatible or unsupported snapshot inputs.

#### Scenario: New dependency edge is reported
- **WHEN** the current snapshot contains a dependency edge absent from the base snapshot
- **THEN** the report lists that edge as added with its source and target identities

#### Scenario: Removed namespace is reported
- **WHEN** a base namespace entry does not occur in the current snapshot
- **THEN** the report lists that namespace as removed

#### Scenario: Incompatible input fails closed
- **WHEN** either input lacks a supported snapshot version or has incompatible analysis scope
- **THEN** report creation fails with an actionable input error

### Requirement: New drift is distinct from known baseline debt
The report SHALL classify current normalized findings and coverage blind spots independently of surface changes. A finding absent from the base SHALL be reported as new; an identity present in both snapshots SHALL be reported as existing; and a current baseline-debt identity SHALL be reported separately from new findings.

#### Scenario: Existing baseline debt is not reported as new drift
- **WHEN** the current snapshot contains a finding or baseline-debt identity also present in the base
- **THEN** the report does not classify that identity as a new violation

#### Scenario: New coverage blind spot is visible
- **WHEN** a current snapshot contains an uncovered coverage item absent from the base snapshot
- **THEN** the report lists it as a new coverage blind spot

### Requirement: CLI emits human and JSON architecture change reports
The CLI SHALL expose `change snapshot` to write a complete snapshot and `change report` to compare `--base` and `--current` snapshots. `change report` SHALL support deterministic `human` and `json` output, leave existing validation behavior unchanged when it is not invoked, and return success for a completed report regardless of whether the report contains drift.

#### Scenario: JSON output is usable by CI
- **WHEN** `change report --format json` completes
- **THEN** stdout contains exactly one valid JSON document with ordered delta and debt sections

#### Scenario: Report does not perform partial analysis
- **WHEN** a user invokes `change report` with two snapshot paths
- **THEN** the command compares only the supplied complete snapshot artifacts
- **AND THEN** it does not select or analyze a changed-file or changed-project subset
