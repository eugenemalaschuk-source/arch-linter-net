## ADDED Requirements

### Requirement: Release pipeline publishes validated immutable candidates
The manual release workflow SHALL calculate version, build final metadata,
pack and manifest candidate packages once, validate the downloaded candidate set
through Checkpoint B, and publish only the same digest-verified artifacts. It
SHALL install the pinned OpenSpec CLI and run `openspec validate --all --strict`
before evidence aggregation.

#### Scenario: Strict OpenSpec validation fails
- **WHEN** the strict OpenSpec gate fails or its pinned executable is unavailable
- **THEN** evidence aggregation and publication do not run
