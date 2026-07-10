## MODIFIED Requirements

### Requirement: Pull request validation workflow
ArchLinterNet SHALL provide a GitHub Actions CI workflow that validates pull requests and pushes with the repository acceptance gate and SonarCloud analysis without producing official release packages.

#### Scenario: Pull request validation runs with SonarCloud analysis
- **WHEN** a pull request targets the repository
- **THEN** the CI workflow restores packages and runs `make acceptance`
- **AND** it runs SonarCloud pull-request analysis for trusted repository pull requests

#### Scenario: Main branch push validation runs with SonarCloud analysis
- **WHEN** code is pushed to the `main` branch
- **THEN** the CI workflow restores packages and runs `make acceptance`
- **AND** it runs SonarCloud branch analysis for `main`

### Requirement: README quality signal badge
The repository README SHALL display the CI status badge, the dynamic Codecov coverage badge, and live SonarCloud quality badges, and it SHALL explain that CI includes both the architecture coverage gate and SonarCloud quality-gate analysis.

#### Scenario: Quality badges and explanation are present
- **WHEN** a reader views the README
- **THEN** it shows a CI status badge sourced from `ci.yml`
- **AND** it keeps the dynamic Codecov coverage badge
- **AND** it shows live SonarCloud badges for the configured SonarCloud project
- **AND** it links to documentation explaining that the CI workflow also runs SonarCloud quality-gate analysis

## ADDED Requirements

### Requirement: SonarCloud analysis runs in the existing CI workflow
SonarCloud analysis SHALL run inside the existing `ci.yml` `validate` job so the repository reuses the same checkout metadata, restore, build, and test execution that powers the acceptance gate.

#### Scenario: SonarCloud reuses the existing validation path
- **WHEN** the `ci.yml` `validate` job runs on a trusted push or pull request
- **THEN** it performs SonarCloud analysis inside the same job as `make acceptance`
- **AND** it does not introduce a second standalone restore/build/test pipeline only for SonarCloud

#### Scenario: Pull request analysis has enough Git metadata
- **WHEN** SonarCloud analyzes a pull request
- **THEN** the workflow checks out the repository with full history
- **AND** SonarCloud receives enough branch comparison metadata to evaluate the pull request against its base branch

### Requirement: SonarCloud quality gate is enforced for trusted runs
Trusted repository pushes and pull requests SHALL fail the CI workflow when the SonarCloud quality gate fails or when required SonarCloud configuration is missing.

#### Scenario: Quality gate failure fails the workflow
- **WHEN** SonarCloud finishes analysis for a trusted push or trusted pull request
- **THEN** the workflow waits for the SonarCloud quality gate result
- **AND** the job fails if the quality gate fails

#### Scenario: Missing configuration fails closed with an actionable diagnostic
- **WHEN** a trusted push or trusted pull request does not have the required SonarCloud configuration
- **THEN** the workflow fails
- **AND** it prints an explicit diagnostic naming the missing secret or variable

#### Scenario: Fork pull request cannot access repository secrets
- **WHEN** a pull request comes from a fork where GitHub does not expose repository secrets
- **THEN** the workflow explains that SonarCloud analysis is skipped for that run
- **AND** it does not falsely report a successful SonarCloud quality gate for that fork analysis

### Requirement: Pull requests expose reviewer-visible SonarCloud results
Trusted pull requests SHALL expose SonarCloud results to reviewers through GitHub PR decoration and a direct path to the SonarCloud pull-request analysis.

#### Scenario: Pull request shows SonarCloud quality-gate feedback
- **WHEN** SonarCloud analyzes a trusted pull request
- **THEN** GitHub shows the Sonar-created pull-request status/check for that analysis
- **AND** reviewers can navigate directly to the SonarCloud pull-request analysis

#### Scenario: Pull request gate is evaluated on new code
- **WHEN** SonarCloud evaluates a trusted pull request
- **THEN** the quality gate is applied to the new code introduced by that pull request
- **AND** the repository does not require the entire historical codebase to be clean before the pull request can merge
