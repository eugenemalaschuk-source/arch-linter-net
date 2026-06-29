## ADDED Requirements

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
