## ADDED Requirements

### Requirement: Checkpoint B scenario shards merge into the canonical platform evidence contract

Checkpoint B MAY execute its required scenario inventory as isolated deterministic scenario shards to reduce wall-clock latency. Shard artifacts are intermediate execution evidence and SHALL NOT independently authorize release.

Before final release aggregation, the repository SHALL merge exactly the required shard set into one canonical `checkpoint-b-platform-evidence/v1` record per platform. The merge SHALL verify that all shards are bound to the same candidate version, source commit, manifest digest, package inventory, observed platform/runtime/architecture, and shell adapter. It SHALL reject a missing, duplicate, unexpected, or overlapping shard/scenario inventory and SHALL require the union of scenario IDs to equal the authoritative required scenario inventory exactly.

#### Scenario: A scenario is lost between shards

- **WHEN** the union of Checkpoint B shard scenario IDs omits any authoritative required scenario
- **THEN** platform evidence merge fails
- **AND** no canonical platform record is emitted for release authorization

#### Scenario: Two shards execute the same scenario ID

- **WHEN** two shard records contain the same scenario ID
- **THEN** platform evidence merge fails as an overlap
- **AND** final release aggregation cannot authorize the candidate

#### Scenario: Shards disagree on candidate provenance

- **WHEN** any shard reports a different source commit, candidate version, manifest digest, or package inventory
- **THEN** platform evidence merge fails before canonical evidence is produced

#### Scenario: Consumer policy shape remains canonical platform evidence

- **WHEN** the consumer-cleanup policy contracts-and-shape shard completes
- **THEN** it reports the typed consumer policy-shape counters
- **AND** the platform merge requires exactly that shard to supply the counters copied into the canonical platform record

### Requirement: Checkpoint B subprocess cancellation bounds the process tree

Checkpoint B subprocess execution SHALL observe the NUnit cancellation token while waiting for child processes. When cancellation or the test timeout fires, the gate SHALL terminate the complete descendant process tree before propagating cancellation so timed-out `dotnet`, shell, MSBuild, or synthetic-consumer processes cannot continue mutating temporary state after the test has ended.

#### Scenario: A child process owns a long-running descendant

- **WHEN** Checkpoint B cancellation fires while a subprocess tree is still running
- **THEN** the direct subprocess and its descendants terminate
- **AND** the test returns cancellation rather than waiting for the original child duration
