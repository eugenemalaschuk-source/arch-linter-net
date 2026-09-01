## Context

`main_quality_coverage.py verify-sonar` consumes two read-only artifacts produced by the Sonar
workflow: the scanner log and `sonar-project-analyses.json`. The former was moved to the dedicated
`RUNNER_TEMP` boundary in the previous fix, but the latter still uses `_safe_path`, which rejects
the real GitHub-hosted runner path before revision validation can run.

## Goals / Non-Goals

**Goals:**

- Apply one identical, realpath-aware `RUNNER_TEMP` containment check to both Sonar verification
  inputs.
- Prove the production-shaped external path and the rejection boundary with focused tests.
- Keep inventory inputs and all write paths under their existing workspace/command-file checks.

**Non-Goals:**

- Expanding trust to arbitrary external files or changing workflow path generation.
- Changing Sonar revision, coverage, or report-validation semantics.
- Introducing a second helper or changing public interfaces.

## Decisions

- Reuse `_github_runner_temp_path` for `analysis_json`, matching `scanner_log`. This centralizes
  realpath resolution, cross-drive handling, containment, and fail-closed handling for an unset
  `RUNNER_TEMP`.
- Put the positive regression artifact in a sibling temporary directory outside the release
  workspace, and put the negative artifact in the workspace while pointing `RUNNER_TEMP` at the
  sibling. The pair distinguishes the intended runner boundary from the old `_safe_path` logic.
- Keep the exception narrow to `_verify_sonar`'s two read-only inputs; inventory and output paths
  continue using their existing validators.

## Risks / Trade-offs

- [Risk] A future workflow may place the response outside `RUNNER_TEMP` → Fail closed with an
  explicit path error, requiring the workflow contract to be corrected rather than silently
  weakening confinement.
- [Risk] The positive test depends on temporary-directory sibling creation → Use `tmp_path.parent`
  and skip only the existing platform-specific symlink test when symlinks are unavailable.
