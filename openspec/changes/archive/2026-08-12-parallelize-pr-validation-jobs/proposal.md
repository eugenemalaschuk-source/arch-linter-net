## Why

`ci.yml`'s `pull_request_validate` job packs workflow lint, repository lint, SonarCloud/coverage,
architecture coverage, and Python tooling tests into one serial Ubuntu job. None of these steps
consume each other's output (aside from the in-job build reuse that `lint-architecture` currently
provides to the architecture coverage steps), so unrelated failures wait behind whichever check
happens to run first, and one broad "Pull Request Validation" check name conflates unrelated
failure modes. #475 already proved the pattern for the test suites (`unit_tests`, `e2e_tests`,
`packed_artifact_tests` now run as independent matrix jobs). This change applies the same
principle to the remaining PR-validation work so unrelated checks schedule concurrently and fail
independently.

## What Changes

- Split `pull_request_validate` into five independently schedulable jobs: `workflow_quality`,
  `repository_lint`, `coverage_sonar`, `architecture_coverage`, `tooling_support_tests`.
- `workflow_quality` runs actionlint, zizmor, and the workflow Prettier check with no .NET
  restore, so it fails fast.
- `repository_lint` runs `make lint` (code-size, dotnet format, self-architecture, docs) without
  any Sonar/coverage coupling.
- `coverage_sonar` keeps the SonarCloud begin/end lifecycle, the coverage-only unit test target,
  Python tooling coverage, and Codecov upload together (SonarScanner for .NET requires begin/build/
  end in one job), and drops the E2E/packed-artifact work that #475 already removed from the
  coverage path.
- `architecture_coverage` owns the strict/audit architecture coverage analysis, artifact uploads,
  and PR comment publication, independently building the CLI/Testing projects it needs (previously
  implicit via `lint-architecture`'s build in the same job) and preserving fail-closed strict
  semantics.
- `tooling_support_tests` runs the architecture-coverage-report-generator and release-evidence
  aggregator Python test suites (the non-coverage variants) as a lightweight, .NET-independent job.
- `needs:` is used only where a real dependency exists; no artificial ordering is introduced
  between the five new jobs, mirroring the existing `unit_tests`/`e2e_tests`/`packed_artifact_tests`
  pattern.
- Fork/Dependabot PR behavior (all non-secret validation runs; secret-backed Sonar/Codecov steps
  skip with an explanation) is preserved, scoped to `coverage_sonar`.
- No coverage thresholds, lint signals, or test scenarios are removed.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `github-actions-ci`: replaces the "single `validate` job runs the whole pipeline" requirements
  (architecture coverage reuses the same job's build; PR comment posting is a stage of that single
  job) with a requirement that PR validation runs as independently schedulable jobs along real
  dependency boundaries, each responsible for building what it needs.

## Impact

- `.github/workflows/ci.yml`: `pull_request_validate` job removed and replaced by five new jobs.
- `openspec/specs/github-actions-ci/spec.md`: requirements describing the single-job pipeline
  topology are superseded by a multi-job topology requirement.
- No changes to `make/*.mk`, application code, or coverage/Sonar quality policy.
