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
The manual release workflow SHALL calculate version, build final metadata, pack
and manifest the complete paired primary-package and symbol-package candidate
set once, validate the downloaded candidate set through Checkpoint B, and
publish or attach only the same manifest-selected digest-verified subjects. Any
manifest-verification step that uses Bash environment-variable syntax SHALL
declare Bash explicitly, including Windows matrix jobs. The workflow SHALL use
exactly one primary-package NuGet push per pair, verify the adjacent
manifest-selected symbol package before the push, and SHALL fail closed when a
primary package already exists; it SHALL NOT use duplicate-success behavior
that can complete while its paired symbol package is absent. It SHALL install
the pinned OpenSpec CLI and run `openspec validate --all --strict` before
evidence aggregation.

#### Scenario: Strict OpenSpec validation fails
- **WHEN** the strict OpenSpec gate fails or its pinned executable is unavailable
- **THEN** evidence aggregation and publication do not run

#### Scenario: Publication selects an unexpected package file
- **WHEN** the publication or GitHub Release attachment path selects a package
  or symbol file outside the verified manifest inventory
- **THEN** the workflow fails before publication or attachment

#### Scenario: Windows verifies the downloaded candidate
- **WHEN** a Windows Checkpoint B matrix job verifies its candidate manifest
- **THEN** the step uses Bash and passes the candidate version and source commit
  to the verifier without PowerShell interpolation

#### Scenario: A partial publish is retried
- **WHEN** a primary package already exists but its paired symbol package may be absent
- **THEN** the NuGet push step fails and does not report the release as successfully published

