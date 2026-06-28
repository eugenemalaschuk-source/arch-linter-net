# GitHub Actions CI Specification

## Purpose
Defines the GitHub Actions workflows for pull request validation and the separated CI/release pipeline.
## Requirements
### Requirement: Pull request validation workflow
ArchLinterNet SHALL provide a GitHub Actions CI workflow that validates pull requests and pushes with the repository acceptance gate without producing official release packages.

#### Scenario: Pull request validation runs
- **WHEN** a pull request targets the repository
- **THEN** the CI workflow restores packages and runs `make acceptance`

#### Scenario: Main branch push validation runs
- **WHEN** code is pushed to the `main` branch
- **THEN** the CI workflow restores packages and runs `make acceptance`

### Requirement: CI release separation
The CI workflow SHALL NOT perform official release packaging, publication, tagging, or GitHub Release creation.

#### Scenario: CI does not pack release packages
- **WHEN** the CI workflow runs for a pull request or push
- **THEN** it does not run `dotnet pack` for official versioned package artifacts

#### Scenario: CI does not use publishing identity
- **WHEN** the CI workflow runs for a pull request or push
- **THEN** it does not request publishing identity tokens or package publication credentials

#### Scenario: CI does not publish or release
- **WHEN** the CI workflow runs for a pull request or push
- **THEN** it does not publish packages, create tags, or create GitHub Releases

### Requirement: README quality signal badges
The repository README SHALL display a badges section distinguishing build status from architecture coverage status.

#### Scenario: Badges section is present
- **WHEN** a reader views the README
- **THEN** it shows a build status badge sourced from `ci.yml`
- **AND** it shows a separate architecture coverage badge sourced from the `architecture-coverage` workflow

### Requirement: Architecture coverage workflow is independent of the existing CI workflow
The `architecture-coverage` workflow SHALL NOT modify or replace the existing `ci.yml` pull request validation workflow.

#### Scenario: ci.yml is untouched
- **WHEN** the architecture-coverage-ci-reporting capability is added
- **THEN** `.github/workflows/ci.yml` has no diff

