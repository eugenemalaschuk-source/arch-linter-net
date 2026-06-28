## Why

Architecture coverage analysis (#57, #96, #98–#101, #143–#146) is fully implemented in `ArchLinterNet.Core`/`ArchLinterNet.Cli` but remains a local-only diagnostic: nothing in repository CI runs it, publishes its JSON output as an artifact, surfaces it to reviewers, or fails a PR when new uncovered/unknown coverage findings appear. Issue #147 makes architecture coverage a first-class CI quality signal, the same way test coverage already is.

## What Changes

- Add a dedicated `architecture-coverage` GitHub Actions workflow (separate from the existing `ci.yml`) that runs ArchLinterNet against the repository's own policy in both `strict` and `audit` modes and uploads `architecture-strict.json` / `architecture-audit.json` as build artifacts on every PR and `main` push.
- The strict run fails the workflow (closed) on any violation or new non-baselined coverage finding; the audit run always completes (`continue-on-error`) and is always uploaded for visibility.
- Add a Markdown coverage report generator (Python script under `tools/scripts/`, following the existing `lint_csharp_file_size.py` pattern) that turns the strict JSON's `coverage_summary`/`coverage_findings` into the human-readable report format from the issue, including a "new-code coverage" section that maps PR-changed first-party files to namespace/project/assembly coverage units and reports unmapped files as `unknown` rather than assuming coverage.
- Add a sticky PR comment step (via `actions/github-script`, no new third-party Action dependency) that posts/updates a single marker-tagged comment with the generated Markdown report instead of leaving a new comment on every push.
- Add a README badges section including a build status badge and a new architecture-coverage workflow badge, clearly distinguished from each other.
- Document baseline debt semantics (existing debt vs. new findings vs. stale baseline entries vs. exclusion `reason` requirements) in `docs/guides/ci-integration.md`.

## Capabilities

### New Capabilities
- `architecture-coverage-ci-reporting`: CI workflow that runs strict/audit architecture coverage analysis, publishes JSON artifacts, generates a Markdown coverage report (including new-code coverage diffing against the PR base), and posts/updates a sticky PR comment from that report.

### Modified Capabilities
- `github-actions-ci`: README quality-signal badges are added; no change to the existing `ci.yml` workflow's behavior (kept separate, per `ci-release-gate`'s precedent that `ci.yml` should remain stable across unrelated CI changes).

## Impact

- New file: `.github/workflows/architecture-coverage.yml`.
- New file(s): `tools/scripts/architecture_coverage_report.py` (+ tests under `tools/scripts/tests/` or wherever existing Python script tests live).
- Modified: `README.md` (badges section), `docs/guides/ci-integration.md` (baseline debt semantics section).
- No changes to `ArchLinterNet.Core`/`ArchLinterNet.Cli` source, no changes to `architecture/dependencies.arch.yml` (self-policy gets no new coverage contracts as part of this change), no changes to `.github/workflows/ci.yml`.
