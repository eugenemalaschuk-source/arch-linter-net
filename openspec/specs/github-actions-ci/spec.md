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

### Requirement: README quality signal badge
The repository README SHALL display a CI status badge and document that it covers the architecture coverage quality gate.

#### Scenario: Badge and explanation are present
- **WHEN** a reader views the README
- **THEN** it shows a CI status badge sourced from `ci.yml`
- **AND** it links to documentation explaining that the same badge reflects the architecture coverage gate

### Requirement: Architecture coverage runs in the existing CI workflow
The architecture coverage steps (strict/audit JSON artifacts, report generation, sticky PR comment) SHALL run inside the existing `ci.yml` acceptance job, after `make acceptance`, reusing its already-built solution instead of restoring and building a second time in a separate workflow.

#### Scenario: Coverage steps run after acceptance in the same job
- **WHEN** the `ci.yml` `validate` job runs
- **THEN** the architecture coverage steps execute after the `Acceptance` step in the same job
- **AND** no additional `dotnet restore`/`dotnet build` of the already-built assemblies is performed before invoking the CLI

