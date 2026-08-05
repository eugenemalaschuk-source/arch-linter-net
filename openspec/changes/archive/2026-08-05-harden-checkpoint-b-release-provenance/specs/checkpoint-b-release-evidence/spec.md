## ADDED Requirements

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
