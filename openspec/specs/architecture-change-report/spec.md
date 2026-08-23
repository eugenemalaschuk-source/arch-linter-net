# architecture-change-report Specification

## Purpose
Provide a deterministic, machine-readable architecture delta between complete analysis snapshots for branch and pull-request workflows.
## Requirements
### Requirement: Complete architecture analysis can be persisted as a change snapshot
The system SHALL provide a versioned `architecture-change-snapshot/v2` artifact that is built from a complete authoritative architecture analysis and contains stable entries for observed namespaces, projects, assemblies, semantic roles and contexts, dependency edges, coverage blind spots, normalized findings, and baseline-debt identities. Core SHALL own the deterministic projection of those authoritative analysis facts and canonical identities into the snapshot contract; CLI hosts SHALL delegate that projection while retaining command orchestration and I/O responsibilities. The artifact SHALL identify its analysis mode and condition-set scope and SHALL use deterministic ordering. A snapshot document missing any required authority metadata or a required entries, findings, or baseline-debt collection SHALL be rejected rather than treated as an empty collection.

#### Scenario: Snapshot retains complete analysis facts
- **WHEN** a user creates a snapshot for a policy in the supported analysis mode
- **THEN** the artifact contains its versioned mode and condition-set scope metadata and sorted architecture surfaces
- **AND THEN** it does not claim to represent only changed files or projects

#### Scenario: Surface kinds remain distinct
- **WHEN** a namespace and assembly share the same textual name
- **THEN** the snapshot assigns them identities in distinct surface kinds

#### Scenario: CLI delegates canonical projection to Core
- **WHEN** a CLI host creates a snapshot from validation, graph, and frozen-baseline results
- **THEN** Core produces the canonical snapshot entries, findings, and baseline-debt identities
- **AND THEN** the CLI host retains option validation, runtime orchestration, artifact I/O, and error presentation

### Requirement: Architecture snapshots are compared by stable identity
The system SHALL compare a base and current `architecture-change-snapshot/v2` artifact by typed, stable identity and SHALL report added and removed namespaces, projects, assemblies, semantic roles, contexts, dependency edges, and coverage blind spots. The result SHALL be deterministically ordered and SHALL reject incompatible, incomplete, or unsupported snapshot inputs.

#### Scenario: New dependency edge is reported
- **WHEN** the current snapshot contains a dependency edge absent from the base snapshot
- **THEN** the report lists that edge as added with its source and target identities

#### Scenario: Removed namespace is reported
- **WHEN** a base namespace entry does not occur in the current snapshot
- **THEN** the report lists that namespace as removed

#### Scenario: Incompatible input fails closed
- **WHEN** either input lacks a supported snapshot version, required collections or authority metadata, or the snapshots use different analysis modes or condition-set scopes
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

