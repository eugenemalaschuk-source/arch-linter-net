## Why

The initial architecture PR report producer cannot create a base snapshot when the base commit
does not contain `architecture/baseline.arch.yml`. In addition, the privileged publisher treats the
overall CI conclusion as a producer-integrity signal, preventing a valid report from appearing for
the very architecture failure it must explain or for unrelated CI failures.

## What Changes

- Make baseline input discovery independent for the base and head worktrees so each snapshot uses
  `--baseline` only when that tree contains the baseline file.
- Bind publication readiness to the dedicated architecture report producer's bounded artifact
  protocol, not to the overall CI conclusion or unrelated job outcomes.
- Add executable, fixture-driven publisher decision tests for first publication, update/rerun,
  stale evidence, malformed/oversized bindings, legacy migration, failed/cancelled production,
  and fork-safe inert handling.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-pr-report-publication`: Define per-tree optional baseline behavior, producer
  integrity independent from architecture verdict and unrelated CI, and executable publication
  scenario evidence.
- `github-actions-ci`: Require the CI producer to expose a dedicated, artifact-bound readiness
  signal even when the strict architecture gate fails.

## Impact

- `.github/workflows/ci.yml` and `publish-architecture-pr-report.yml`
- Workflow publisher test support under `tools/release/tests/`
- Architecture PR report publication and CI OpenSpec specifications
