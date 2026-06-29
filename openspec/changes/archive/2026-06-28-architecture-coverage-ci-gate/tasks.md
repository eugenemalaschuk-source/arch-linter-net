## 1. Report generator script

- [x] 1.1 Add `tools/scripts/architecture_coverage_report.py` that loads a strict-mode JSON file (`coverage_summary`, `coverage_findings`, `passed`) and renders the Markdown report (overall status + covered/excluded/uncovered/stale/unknown counts) per the issue's format.
- [x] 1.2 Add new-code coverage mapping: given a list of changed first-party file paths, derive each file's namespace (regex on `namespace X;` / `namespace X {`) and nearest enclosing `.csproj` (project/assembly), cross-reference against `coverage_summary`, and append a "New-code coverage" section listing only non-covered or unmapped (`unknown`) units.
- [x] 1.3 Wire the script into the existing `tools/pyproject.toml` / `uv` setup so it runs the same way as `lint_csharp_file_size.py`.
- [x] 1.4 Add script-level tests: JSON parsing, Markdown generation, zero-findings output, failed-gate (`passed: false`) output, and unknown-mapping behavior.

## 2. Architecture coverage workflow

- [x] 2.1 Create `.github/workflows/architecture-coverage.yml` triggered on `pull_request` and `push` to `main`.
- [x] 2.2 Add steps: checkout, setup .NET, setup `uv`, restore, build `ArchLinterNet.Cli`.
- [x] 2.3 Run `--policy architecture/dependencies.arch.yml --mode strict --format json > architecture-strict.json` with `continue-on-error: true`, capturing its outcome for the final job-conclusion step.
- [x] 2.4 Run `--policy architecture/dependencies.arch.yml --mode audit --format json > architecture-audit.json` with `continue-on-error: true`, always.
- [x] 2.5 Always upload `architecture-strict.json` and `architecture-audit.json` as build artifacts (`if: always()`).
- [x] 2.6 Run the report generator against `architecture-strict.json` plus the PR's changed-files diff (`git diff --name-only` against the PR base ref) to produce `architecture-coverage.md`.
- [x] 2.7 On `pull_request` events, use `actions/github-script` to find a prior comment containing a hidden marker and update it, or create a new comment with the marker if none exists.
- [x] 2.8 Add a final step that fails the job if the strict step's outcome was a failure, so the workflow conclusion (and badge) reflects the gate correctly despite `continue-on-error` on the strict step.
- [x] 2.9 Scope `permissions:` for this workflow to `contents: read` and `pull-requests: write` only (not touching `ci.yml`'s permissions).

## 3. README and docs

- [x] 3.1 Add a badges section to `README.md` with a build status badge (from `ci.yml`) and an architecture-coverage badge (from `architecture-coverage.yml`), clearly labeled.
- [x] 3.2 Add a "Baseline debt in CI" section to `docs/guides/ci-integration.md` documenting: existing baselined debt does not fail the PR; new coverage findings fail it; resolved baseline entries surface as stale and should be cleaned up; exclusions require a `reason` and are not a silent bypass.

## 4. Validation

- [x] 4.1 Run `make fmt`.
- [x] 4.2 Run `make acceptance` (lint + test) and fix any failures.
- [x] 4.3 Manually sanity-check the new workflow YAML (e.g. `actionlint` if available, or careful review) since it cannot be executed locally.
