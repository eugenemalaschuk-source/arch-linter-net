## Why

The `Main Coverage Evidence` job on `main` fails before SonarCloud/Codecov because
`main_quality_coverage.py assemble` rejects GitHub's own `$GITHUB_OUTPUT` runner command file with
`Coverage evidence error: The GitHub output file '...' resolves outside the release workspace.`
`$GITHUB_OUTPUT` lives under the runner's `$RUNNER_TEMP/_runner_file_commands/...`, outside the
repository checkout, so the repository-workspace confinement that `fix-sonarcloud-new-code-debt`
(#736) added to every `Path`-typed CLI argument in `main_quality_coverage.py` — correct for
repository-controlled paths — makes the real workflow unrunnable for this one argument. `main`
telemetry needs this to run on every push, so this is a P0 regression fix (#743).

## What Changes

- Add `_github_command_file_path(value, description, env_var)` to `tools/release/_release_workspace.py`:
  a distinct, narrow trust boundary for GitHub Actions runner command-file transport paths
  (`$GITHUB_OUTPUT`, `$GITHUB_ENV`, etc.), separate from `_safe_path`'s repository-workspace
  confinement. It trusts a path only when it exactly matches the value of the named environment
  variable the runner itself set (e.g. `os.environ["GITHUB_OUTPUT"]`), never merely because it is
  passed under a `--github-*` flag.
- Change the 3 `--github-output` call sites in `tools/release/main_quality_coverage.py`
  (`_assemble`, `_verify_inventory_command`, `_verify_sonar`) from `_safe_path(...)` to
  `_github_command_file_path(..., "GITHUB_OUTPUT")`.
- Leave every other `_safe_path` call site in `tools/release/` unchanged — repository-controlled
  paths (coverage roots, output roots, artifact/manifest-derived paths) keep full workspace
  confinement.
- Add regression tests covering: the runner-shaped path (outside the workspace) is accepted when
  `GITHUB_OUTPUT` matches; an arbitrary `--github-output` path is rejected when it does not match
  `GITHUB_OUTPUT`; the check fails closed when `GITHUB_OUTPUT` is unset.

`tools/release/main_build.py`'s `--github-env`/`--github-output` arguments were never passed
through `_safe_path`, so they are unaffected and out of scope.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `release-tooling-workspace-confinement`: the existing requirement that every `Path`-typed CLI
  argument under `tools/release/` goes through `_safe_path` now has a documented, narrow exception
  for GitHub Actions runner command-file transport arguments, which use the new
  `_github_command_file_path` environment-anchored trust boundary instead.

## Impact

- `tools/release/_release_workspace.py`
- `tools/release/main_quality_coverage.py`
- `tools/release/tests/test_main_quality_coverage.py`
- Restores `Main Coverage Evidence` → `Main SonarCloud` / `Main Codecov` reachability on `main`
  push runs (#706, #728 provenance checks remain authoritative downstream)
- No public API, no architecture-governed dependency edges, no CI workflow YAML changes
