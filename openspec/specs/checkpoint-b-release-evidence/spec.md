# checkpoint-b-release-evidence Specification

## Purpose
Define the release-blocking Checkpoint B contract: validate one immutable NuGet
candidate set on every required platform, preserve verifiable synthetic-adopter
evidence, and authorize publication only for the digest-verified files that
were tested.
## Requirements
### Requirement: Checkpoint B consumes packed candidate artifacts
The repository SHALL provide a deterministic NUnit Checkpoint B entrypoint that
consumes a supplied immutable candidate manifest, validates package metadata, dependency graph,
embedded resources, content files, versions, and digests, and consumes the CLI
and applicable Core, CEL, and Testing packages from an isolated local feed. The
entrypoint SHALL reuse the synthetic adoption corpus and SHALL NOT use a
`ProjectReference` as evidence for an external consumer scenario.

#### Scenario: Candidate is installed from the isolated feed
- **WHEN** the Checkpoint B entrypoint runs for a candidate version
- **THEN** every external-consumer scenario loads the manifest-verified candidate
  packages from the isolated feed and records their identities and digests

### Requirement: Final adopter matrix is executable and release-blocking
Checkpoint B SHALL execute the synthetic greenfield, conventional multi-project,
same-named multi-host, legacy-import migration, clean-checkout, direct CLI,
generic CI-neutral, and `ArchLinterNet.Testing` acceptance scenarios. It SHALL
also execute non-TTY, offline packaged-schema, sequential/default-parallel,
cache disabled/population/hit/corruption, and cancellation/publication
interruption scenarios where their owning capability is available. Any failed
scenario SHALL produce a failed Checkpoint B result and SHALL block release
authorization.

#### Scenario: Matrix detects a failed invariant
- **WHEN** any scenario reports different canonical results, unsafe publication,
  missing packaged schema, or non-zero external-consumer failure
- **THEN** the evidence marks Checkpoint B failed and does not authorize 0.5.1

### Requirement: Release evidence is deterministic, synthetic, and explicit
The repository SHALL produce a deterministic immutable workflow-artifact
Checkpoint B evidence summary containing the tested commit, candidate package identities and digests,
scenario inventory and results, observed platform/runtime/shell matrix, support
exclusions and rationale, performance-evidence reference, OpenSpec,
self-architecture, package, and documentation gate results, and an explicit
pass-or-fail authorization statement. The summary SHALL state that all
identities are synthetic and SHALL NOT contain private adopter identities.

#### Scenario: Evidence authorizes the candidate
- **WHEN** every required scenario and gate succeeds
- **THEN** the summary explicitly records that Checkpoint B passed and that the
  tested candidate is authorized for 0.5.1 publication

### Requirement: Checkpoint B records a complete executable scenario oracle
Every required Checkpoint B fixture SHALL declare its expected exit category,
canonical findings or identities, completion status, and permitted diagnostics.
The matrix SHALL include clean checkout, direct CLI, CI-neutral wrapper, Testing
API, POSIX and PowerShell entrypoints, non-TTY output, documented command
examples, sequential/default parallelism, cache miss/population/verified
hit/corruption, and in-flight cancellation/publication interruption.

#### Scenario: A mode fails identically
- **WHEN** every execution mode returns the same incorrect result
- **THEN** Checkpoint B fails because the result does not match the fixture oracle

### Requirement: Evidence is schema-backed and cannot self-authorize
The aggregation job SHALL validate exactly one evidence record for every required
platform, its observed architecture and shell, required scenario inventory,
candidate package manifest, and independently produced repository-gate results.
It SHALL emit an immutable GitHub Actions workflow artifact containing the
candidate-manifest digest and workflow-run reference; it SHALL NOT hard-code
successful gates or authorization. This artifact is the authoritative release
record and is retained according to the repository artifact-retention policy;
generated evidence is not checked into the source tree.

#### Scenario: Evidence is incomplete
- **WHEN** a platform record, required scenario, gate result, or manifest digest
  is absent, duplicated, mismatched, or invalid
- **THEN** aggregation fails and no authorization statement is emitted

### Requirement: Checkpoint B evidence has executable, duplicate-free scenario outcomes
Every Checkpoint B scenario record SHALL be returned by the oracle that executed
the scenario. The aggregator SHALL reject a platform record with a duplicate,
missing, or unexpected scenario ID before authorization.

#### Scenario: A scenario is duplicated
- **WHEN** a platform evidence record contains two entries with the same scenario ID
- **THEN** aggregation fails and no release authorization is emitted

