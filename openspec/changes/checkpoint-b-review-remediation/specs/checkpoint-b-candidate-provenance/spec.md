## ADDED Requirements

### Requirement: Cancellation proof starts after validation enters work
Checkpoint B SHALL use a deterministic barrier reached from within validation
before requesting cancellation. It SHALL prove cancellation produces no final
report output, no temporary output, and no cache entry reusable as success.

#### Scenario: Validation is interrupted in flight
- **WHEN** the candidate has entered the cancellation barrier and the caller cancels
- **THEN** the CLI and Testing API report cancellation and no successful output or cache state remains
