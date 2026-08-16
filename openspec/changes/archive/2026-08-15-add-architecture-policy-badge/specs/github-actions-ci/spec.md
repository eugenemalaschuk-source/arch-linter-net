## MODIFIED Requirements

### Requirement: README quality signal badge
The repository README SHALL display the CI status badge, the dynamic Codecov
coverage badge, a dynamic ArchLinterNet architecture-policy badge sourced from
the dedicated strict-self-policy workflow on `main`, and live SonarCloud quality
badges. It SHALL explain that the architecture-policy badge proves a strict
self-policy pass, while architecture coverage and test coverage remain separate
signals.

#### Scenario: Quality badges and explanation are present
- **WHEN** a reader views the README
- **THEN** it shows a CI status badge sourced from `ci.yml`
- **AND** it keeps the dynamic Codecov coverage badge
- **AND** it shows a dynamic architecture-policy badge sourced from the
  dedicated workflow's `main` status
- **AND** it shows live SonarCloud badges for the configured SonarCloud project
- **AND** it links to documentation explaining the distinct CI, architecture
  policy, architecture coverage, and SonarCloud quality signals

## ADDED Requirements

### Requirement: Dedicated architecture-policy workflow
GitHub Actions SHALL run a dedicated architecture-policy workflow for pull
requests and pushes to `main`. Its strict self-policy job SHALL run the
authoritative read-only `make lint-architecture` target and SHALL not publish
packages, releases, GitHub Pages content, or repository commits.

#### Scenario: Main push refreshes strict-policy status
- **WHEN** code is pushed to `main`
- **THEN** the dedicated workflow restores the required dependencies and runs
  `make lint-architecture`
- **AND** its workflow conclusion is the public architecture-policy badge
  source

#### Scenario: Pull request validates strict policy without publication
- **WHEN** the dedicated workflow runs for a pull request
- **THEN** it executes the same strict self-policy target
- **AND** it uses only read permission for repository contents
- **AND** it does not publish packages, releases, Pages content, or repository
  commits
