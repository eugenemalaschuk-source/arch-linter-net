## MODIFIED Requirements

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
