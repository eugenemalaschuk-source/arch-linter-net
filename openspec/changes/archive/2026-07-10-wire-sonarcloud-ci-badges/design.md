## Context

The repository already runs a single `ci.yml` validation workflow that restores once, runs `make acceptance`, collects Codecov coverage, and then performs the architecture coverage gate. The updated issue requires SonarCloud to become part of that same validation path for both `main` and `pull_request`, with a failing quality gate on new code and reviewer-visible PR feedback.

The public SonarCloud project already exists and exposes public metadata, including the project key `eugenemalaschuk-source_arch-linter-net` and organization `eugenemalaschuk-source`. SonarCloud currently reports `autoscanEnabled: true`, so the repository docs need to call out that CI-based analysis and coverage import depend on using the CI-driven mode configured by the maintainer in SonarCloud.

## Goals / Non-Goals

**Goals:**
- Add SonarCloud analysis to the existing validation workflow without duplicating the pipeline.
- Make pull requests produce SonarCloud quality-gate feedback against new code.
- Import .NET coverage into SonarCloud from the current test-coverage path while preserving Codecov as the public coverage badge.
- Document the required repository configuration and the recommended GitHub required check.

**Non-Goals:**
- Creating or administering the SonarCloud project from repository code.
- Automating GitHub branch-protection settings.
- Replacing existing acceptance, architecture coverage, or Codecov behavior with SonarCloud-only logic.

## Decisions

### Keep SonarCloud inside the existing `ci.yml` workflow

The repository already treats `ci.yml` as the main validation path and the issue explicitly prefers minimal workflow changes over a second large pipeline. We will insert SonarScanner begin/end steps into the current `validate` job so the same build/test execution powers both the existing gates and SonarCloud analysis.

Alternative considered:
- Add a second Sonar-only workflow. Rejected because it duplicates restore/build/test work and splits the main validation signal across multiple pipelines.

### Use SonarScanner for .NET directly in GitHub Actions

The SonarSource documentation for GitHub Actions and .NET uses SonarScanner for .NET wrapped around the build. Using the scanner directly keeps the integration close to the official CI-based flow, avoids introducing a third-party action as core infrastructure, and makes quality-gate waiting explicit in the workflow.

Alternative considered:
- Use a community wrapper action for SonarScanner for .NET. Rejected to avoid adding a non-official dependency for a core quality gate when the native scanner flow is straightforward.

### Extend the existing coverage target to emit OpenCover alongside Cobertura

Codecov already consumes Cobertura reports from `make test-coverage`, while SonarCloud's .NET coverage import supports OpenCover. Emitting both formats from the same test run preserves the current Codecov flow and adds Sonar-compatible coverage without a separate duplicate test execution.

Alternative considered:
- Run a second dedicated Sonar coverage test pass. Rejected because it increases CI time and duplicates test execution.

### Fail closed for missing Sonar configuration on trusted runs

For pushes and same-repository pull requests, missing Sonar configuration should stop the workflow with an explicit message so the repository cannot silently lose the quality gate. For fork pull requests, the workflow should explain that SonarCloud is skipped because GitHub does not expose repository secrets to untrusted forks.

Alternative considered:
- Soft-skip all missing-config cases. Rejected because it hides a broken repository quality gate.

## Risks / Trade-offs

- [Risk] SonarCloud automatic analysis may still be enabled and compete with CI-based analysis. -> Mitigation: document that maintainers should disable automatic analysis for this project when switching to CI-based coverage-backed analysis.
- [Risk] The exact GitHub check label is owned by the SonarCloud app and can vary with Sonar branding. -> Mitigation: document the expected Sonar-created check and instruct maintainers to confirm the rendered name after the first decorated PR.
- [Risk] Fork pull requests cannot access `SONAR_TOKEN`, so they cannot produce a trusted Sonar analysis from the repository workflow. -> Mitigation: emit an explicit workflow summary explaining the limitation and keep same-repo PRs and `main` pushes fail-closed.
- [Risk] Coverage import can regress if the report pattern or format changes. -> Mitigation: keep coverage generation centralized in `make test-coverage` and point both Codecov and SonarCloud at deterministic file globs under `test-results/`.

## Migration Plan

1. Add the OpenSpec delta and implement the workflow, README, docs, and coverage-target updates.
2. Validate locally with `rtk make fmt` and `rtk make acceptance`.
3. Merge the change and open a test pull request from a repository branch to confirm SonarCloud PR decoration, quality-gate status, and the direct SonarCloud PR-analysis link.
4. After the first decorated PR run, configure GitHub branch protection manually to require the Sonar-created check if desired.

## Open Questions

- Whether the repository's SonarCloud project is on a plan tier that supports the desired pull-request analysis flow for this repository shape is assumed to be handled by the maintainer-side setup referenced in the issue.
