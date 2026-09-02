## ADDED Requirements

### Requirement: Snapshot inputs and producer readiness are independent per tree and verdict

For each base and current architecture-change snapshot independently, the read-only producer SHALL
append `--baseline architecture/baseline.arch.yml` only when that exact worktree contains the file.
It SHALL not reuse a baseline path or bytes from the other tree. The publisher SHALL determine
transport readiness from exactly one successful named architecture report producer job and its
bound artifact protocol; it SHALL NOT use the overall `workflow_run.conclusion` as a
producer-integrity signal.

#### Scenario: Historical base without a baseline still produces a report

- **WHEN** the base worktree lacks `architecture/baseline.arch.yml` and the current worktree has
  or lacks it independently
- **THEN** the base snapshot runs without `--baseline`
- **AND** the current snapshot uses `--baseline` only when its own tree contains the file
- **AND** the producer can render and upload the canonical report artifact

#### Scenario: Valid report publication is independent from overall CI conclusion

- **WHEN** exactly one named architecture report producer job succeeded and its current-head
  artifact passes all transport checks
- **THEN** the publisher may publish the report even when the overall CI run failed because of a
  strict architecture gate or an unrelated job
- **AND** a missing, failed, or cancelled named producer job is rejected as an integrity failure

### Requirement: Publisher behavior has executable event and artifact regression evidence

The repository SHALL execute fixture-driven tests against the publisher's workflow JavaScript with
mocked GitHub events, REST responses, comments, and bounded artifact files. These tests SHALL
exercise first publication, same-comment update on rerun, legacy-marker migration, stale head,
bad PR/head/run binding, bad hash/schema, oversized payload, failed/cancelled producer, and a fork
artifact that remains inert.

#### Scenario: Fork fixture cannot cause code execution

- **WHEN** a fixture models a fork pull request and supplies arbitrary bounded report bytes
- **THEN** the publisher test invokes only the fixed artifact validation and comment APIs
- **AND** it does not require a checkout, evaluate the bytes as code, or add write authority to the
  producer
