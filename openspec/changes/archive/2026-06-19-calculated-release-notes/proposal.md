## Why

The manual release workflow can calculate a package version and build artifacts, but does not generate release notes from merged PRs. Maintainers currently have no dry-run reviewable notes before publication, and downstream tasks (NuGet metadata, GitHub Release) have no reusable notes file to consume.

## What Changes

- Add `Generate release notes` step to `.github/workflows/release-nuget.yml` that calls the GitHub `releases/generate-notes` API with the calculated package version
- Extend `tools/release/calculate_version.py` with a `--github-env` mode that exports `TARGET_TAG` and `PREVIOUS_TAG` alongside `PACKAGE_VERSION`, so the release notes step can derive its comparison range from the same SemVer-aware tag detection
- Raise the release job `contents` permission from `read` to `write` (required by the generate-notes endpoint)
- Write generated notes to `artifacts/release-notes/release-notes.md` with stable path for reuse by downstream tasks (#25, #26, #27)
- Print generated notes in workflow logs for dry-run review

## Capabilities

### New Capabilities
- `release-notes-generation`: generate release notes from merged PRs using the calculated version tag and the `.github/release.yml` category configuration

### Modified Capabilities

*None.* The existing `manual-nuget-release`, `release-version-bump`, and `ci-release-gate` specs describe requirements that are not changing.

## Impact

- `.github/workflows/release-nuget.yml`: new step added after version calculation
- `tools/release/calculate_version.py`: new `--github-env` output mode; new `DetectedTag` dataclass; existing `detect_latest_tag()` preserved for test compatibility
- `tests/release/test_calculate_version.py`: new test cases for release-context export
- `permissions` block in release job: `contents: write` (was `read`)
- No new external dependencies — uses `gh api` (pre-installed on GitHub runners) and `jq`
