## ADDED Requirements

### Requirement: Checkpoint B consumes packed candidate artifacts
The repository SHALL provide a deterministic NUnit Checkpoint B entrypoint that
packs the 0.5.1 candidate, validates package metadata, dependency graph,
embedded resources, content files, versions, and digests, and consumes the CLI
and applicable Core, CEL, and Testing packages from an isolated local feed. The
entrypoint SHALL reuse the synthetic adoption corpus and SHALL NOT use a
`ProjectReference` as evidence for an external consumer scenario.

#### Scenario: Candidate is installed from the isolated feed
- **WHEN** the Checkpoint B entrypoint runs for a candidate version
- **THEN** every external-consumer scenario loads the freshly packed candidate
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
The repository SHALL produce a checked-in deterministic Checkpoint B evidence
summary containing the tested commit, candidate package identities and digests,
scenario inventory and results, observed platform/runtime/shell matrix, support
exclusions and rationale, performance-evidence reference, OpenSpec,
self-architecture, package, and documentation gate results, and an explicit
pass-or-fail authorization statement. The summary SHALL state that all
identities are synthetic and SHALL NOT contain private adopter identities.

#### Scenario: Evidence authorizes the candidate
- **WHEN** every required scenario and gate succeeds
- **THEN** the summary explicitly records that Checkpoint B passed and that the
  tested candidate is authorized for 0.5.1 publication

