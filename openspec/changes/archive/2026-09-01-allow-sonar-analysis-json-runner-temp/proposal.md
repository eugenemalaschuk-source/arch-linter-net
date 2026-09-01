## Why

The merged Sonar verification fix correctly permits the scanner log from GitHub Actions'
`RUNNER_TEMP`, but the same workflow also writes `sonar-project-analyses.json` there. The
verification command still routes that read-only response through the release-workspace-only
sanitizer, so the post-merge quality job fails before checking the analysis revision.

## What Changes

- Validate the Sonar project analyses response with the existing environment-anchored
  `RUNNER_TEMP` trust boundary used for the scanner log.
- Add regression coverage proving a response outside the checkout but inside `RUNNER_TEMP` is
  accepted, while a workspace path outside `RUNNER_TEMP` is rejected.
- Make the runner-temp requirement explicitly cover both Sonar verification inputs.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `release-tooling-workspace-confinement`: explicitly include the Sonar analyses response in the
  bounded read-only runner-temp verification inputs.

## Impact

- Updates `tools/release/main_quality_coverage.py` and its Python regression tests.
- Updates the release-tooling OpenSpec requirement and archived change artifacts.
- No public API, workflow, dependency, or production assembly changes.
