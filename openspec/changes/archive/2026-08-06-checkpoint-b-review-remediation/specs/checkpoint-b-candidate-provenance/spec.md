## ADDED Requirements

### Requirement: Cancellation proof starts after validation enters work
Checkpoint B SHALL use a deterministic barrier reached from within Testing API
validation, and a deterministic observation that the CLI has read its selected
target artifact, before requesting cancellation. It SHALL prove cancellation
produces no final report output, no temporary output, and no cache entry
reusable as success.

#### Scenario: Validation is interrupted in flight
- **WHEN** the Testing API has entered its cancellation barrier or the CLI has
  read its selected target artifact, and the caller cancels
- **THEN** the CLI and Testing API report cancellation and no successful output or cache state remains
