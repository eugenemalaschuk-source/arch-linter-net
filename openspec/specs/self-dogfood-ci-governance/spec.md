# self-dogfood-ci-governance Specification

## Purpose
Ensure the repository's own architecture governance lane uses one verified candidate and records
safe parallel projection evidence without weakening any authoritative validation or reporting gate.

## Requirements

### Requirement: The self-governance producer binds all projections to one candidate

The pull-request architecture producer SHALL restore and build one candidate checkout once, publish
receipt-backed evidence for the resulting outputs without another build or restore, record an
immutable source/tree/tool identity, and fail closed when the expected candidate output or identity
cannot be verified. Every strict, public-API, coverage, Health, change, and report-producing
projection SHALL verify and consume that same candidate identity through a non-building path.

#### Scenario: A complete candidate is shared by all projections

- **WHEN** the architecture producer reaches its projection phase
- **THEN** the projections use the same checked-out PR head, verified build outputs, policy digest,
  and CLI tool identity
- **AND** the producer's receipt-publication step performs no build or restore
- **AND** no projection performs an implicit rebuild or restore

#### Scenario: Missing or stale candidate state fails closed

- **WHEN** the expected candidate assembly, build identity, source SHA, tree SHA, or policy digest
  is missing or mismatched
- **THEN** the producer reports a preparation failure
- **AND** it does not publish a canonical report or badge manifest claiming successful evidence

### Requirement: Independent read-only projections fan out after preparation

After the shared candidate preparation, the producer SHALL schedule the strict policy gate,
reviewed public-API verification, architecture coverage, and Health/current change evidence as
independent projections. Each projection SHALL own its logs and generated files, and the producer
SHALL retain canonical exit semantics and fail-closed aggregation.

The base/reference checkout SHALL likewise be explicitly prepared and receipt-verified before its
snapshot projection; the base snapshot and every candidate projection SHALL then use ordinary
receipt-verifying preparation only.

#### Scenario: Independent projections overlap safely

- **WHEN** candidate preparation succeeds
- **THEN** independent projections run concurrently on the standard hosted runner
- **AND** their logs and outputs do not race or overwrite one another
- **AND** a failed producer is not converted into a successful aggregate result

#### Scenario: Coverage failure remains visible while report evidence is retained

- **WHEN** strict coverage finds a violation but Health/change/report inputs are valid
- **THEN** the producer retains the valid report artifacts
- **AND** the dependent architecture gate fails for the strict coverage result

### Requirement: Current evidence and rendering do not duplicate analysis

The Health/current projection SHALL produce the current architecture change snapshot from the same
Health analysis session. Badge, PR-report, and manifest steps SHALL consume already-produced
canonical JSON/Markdown and SHALL NOT invoke architecture analysis commands.

#### Scenario: Health and current change share one analysis

- **WHEN** the producer creates current Health and change evidence
- **THEN** it invokes Health with the current change-snapshot output path
- **AND** the resulting Health and change documents are validated before rendering

#### Scenario: Rendering consumes canonical artifacts only

- **WHEN** the producer renders the PR report, badge payload, or publication manifest
- **THEN** it reads the validated producer artifacts
- **AND** it does not run strict, audit, Health, snapshot, or other architecture analysis

### Requirement: The producer records comparable timing and DAG evidence

Each successful producer run SHALL emit machine-readable evidence containing the candidate identity,
runner/source context, projection dependency classification, projection outcomes and durations,
base/reference preparation, and the command/process accounting needed to compare at least three
successful runs with the pinned 204 s median and the <=60 s target. The evidence SHALL distinguish
governance span from the sum of projection durations, SHALL start the headline governance span at
the earliest explicit candidate/base preparation boundary, and SHALL record PASS or GAP against the
target. Comparisons against the pinned baseline SHALL use that same preparation-inclusive boundary
rather than a post-build projection-only span.

#### Scenario: A run records performance evidence without changing authority

- **WHEN** the producer completes its projections
- **THEN** it uploads the timing/DAG evidence with the architecture artifacts
- **AND** the evidence is descriptive and cannot turn a failed authority into a pass

#### Scenario: A comparison identifies a residual gap

- **WHEN** comparable evidence remains above 60 s
- **THEN** the evidence records the phase-level attribution and GAP status
- **AND** it does not authorize or perform a Core optimization
