## Why

The repository already publishes CI and Codecov signals, but it does not yet surface SonarCloud as a first-class quality signal in either pull requests or the README. Issue #246 now requires SonarCloud to participate in PR review with new-code quality gating and reviewer-visible decoration, not just main-branch dashboard badges.

## What Changes

- Add SonarCloud analysis to the existing GitHub Actions validation workflow for `main` pushes and `pull_request` events.
- Configure the workflow to wait for the SonarCloud quality gate and fail the job when the gate fails.
- Ensure pull request runs provide reviewer-visible SonarCloud feedback and a direct path to the SonarCloud PR analysis.
- Feed .NET test coverage into SonarCloud from the existing coverage collection path while keeping Codecov as the README coverage source of truth.
- Expand the README badge section and CI integration guide to document SonarCloud badges, PR analysis behavior, required secrets/variables, and the recommended GitHub required-check setup.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `github-actions-ci`: CI now includes SonarCloud branch and pull-request analysis, quality-gate enforcement, and README quality-signal coverage for SonarCloud.

## Impact

- Affected systems: `.github/workflows/ci.yml`, `README.md`, `docs/guides/ci-integration.md`, and coverage collection targets used by CI.
- External integration: SonarCloud project `eugenemalaschuk-source_arch-linter-net` in organization `eugenemalaschuk-source`.
- Operational dependency: GitHub Actions secret `SONAR_TOKEN`, plus optional repository variables for public SonarCloud identifiers.
