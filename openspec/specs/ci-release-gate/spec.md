# CI Release Gate Specification

## Purpose
Ensures the release workflow runs the full acceptance gate before packing and publishing packages, without altering the existing CI workflow.
## Requirements
### Requirement: Release workflow runs acceptance gate before packing packages
The manual release workflow SHALL run the repository acceptance gate before building NuGet packages.

#### Scenario: Acceptance passes
- **WHEN** the release workflow runs with `publish=false`
- **THEN** the `Acceptance` step runs after `Build` and before `Pack Core`
- **AND** NuGet packages are produced only if acceptance succeeds

#### Scenario: Acceptance fails
- **WHEN** the release workflow runs and `make acceptance` exits with non-zero
- **THEN** all subsequent steps are skipped (no pack, no upload, no publish)

### Requirement: CI workflow remains unchanged
The pull request CI workflow SHALL NOT be modified by this change.

#### Scenario: Pull request CI workflow is untouched
- **WHEN** this change is applied
- **THEN** the pull request CI workflow file SHALL have no diff

### Requirement: Release pipeline publishes validated immutable candidates
The manual release workflow SHALL calculate version, build final metadata,
pack and manifest candidate packages once, validate the downloaded candidate set
through Checkpoint B, and publish only the same digest-verified artifacts. It
SHALL install the pinned OpenSpec CLI and run `openspec validate --all --strict`
before evidence aggregation.

#### Scenario: Strict OpenSpec validation fails
- **WHEN** the strict OpenSpec gate fails or its pinned executable is unavailable
- **THEN** evidence aggregation and publication do not run

