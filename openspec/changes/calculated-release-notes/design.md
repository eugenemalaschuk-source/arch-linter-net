## Context

The manual release workflow (`release-nuget.yml`) calculates a `PACKAGE_VERSION` via `tools/release/calculate_version.py`, then builds, packs, and optionally publishes NuGet packages. Two prerequisites are already on `main`:

- `.github/release.yml` defines release-note categories (Breaking Changes, Features, Fixes, Documentation, CI/CD, Dependencies, Other Changes) mapped to PR labels.
- `calculate_version.py` detects the latest SemVer tag and increments it by release type (`preview`/`patch`/`minor`/`major`).

The gap: the workflow exports only `PACKAGE_VERSION` (the next version), discarding the detected latest tag. The release notes step needs both the target tag (`v{PACKAGE_VERSION}`) and the previous tag (the latest detected tag) to request a comparison range from the GitHub API.

## Goals / Non-Goals

**Goals:**
- Add a workflow step that generates release notes from merged PRs using the GitHub `releases/generate-notes` API
- Export `TARGET_TAG` and `PREVIOUS_TAG` from `calculate_version.py` alongside `PACKAGE_VERSION`, using the same SemVer-aware tag detection
- Raise release job `contents` permission to `write` (required by the generate-notes endpoint)
- Write generated notes to `artifacts/release-notes/release-notes.md`
- Print generated notes in workflow logs for dry-run review
- Handle first-ever release (no previous tag) gracefully by omitting `previous_tag_name` from the API request

**Non-Goals:**
- Custom PR-categorization logic (delegated to `.github/release.yml` and the GitHub API)
- Modifying the NuGet pack steps or package metadata (delegated to #25)
- Creating GitHub Releases (delegated to #26)
- Uploading release notes as a workflow artifact (delegated to #27)
- Changing the CI workflow or any push/PR-triggered workflow

## Decisions

### 1. Release context export in `calculate_version.py`

`calculate_version.py` currently prints only the next version to stdout. The release notes step needs three values:

| Variable | Source | Example |
|---|---|---|
| `PACKAGE_VERSION` | Next version (no `v` prefix) | `0.2.0-preview.3` |
| `TARGET_TAG` | `v` + PACKAGE_VERSION | `v0.2.0-preview.3` |
| `PREVIOUS_TAG` | Raw name of latest detected tag | `v0.2.0-preview.2` or empty |

**Approach:** Add a `--github-env <file>` flag that writes key=value lines to the specified file. When absent, stdout-only mode is preserved for backward compatibility. The script detects the latest tag and stores its raw name alongside the parsed version.

**Why not detect the previous tag separately in the workflow step?** Duplicating tag-detection logic in bash would reimplement SemVer ordering (preview vs stable precedence, numeric comparison) that `calculate_version.py` already handles correctly.

**New type:** `DetectedTag(name: str, version: SemVerVersion)` — `detect_latest_tag()` returns `Optional[DetectedTag]`. The existing `detect_latest_tag()` that returns `Optional[SemVerVersion]` is kept as a thin compatibility wrapper, so existing tests pass unchanged.

### 2. GitHub API for note generation

Use `gh api repos/{owner}/{repo}/releases/generate-notes` with a POST payload containing:
- `tag_name`: `v{PACKAGE_VERSION}` (the tag-to-be)
- `target_commitish`: `$GITHUB_SHA` (the commit being released)
- `previous_tag_name`: `PREVIOUS_TAG` only when non-empty
- `configuration_file_path`: `.github/release.yml` (explicit reference to our categories)

**Why not a custom Python script?** The `.github/release.yml` categories are designed for GitHub's built-in generator. Reimplementing label-based classification in Python would duplicate that configuration and diverge over time.

### 3. Token scope

The `releases/generate-notes` endpoint requires `contents: write`. The release job currently uses `contents: read` (set when only package building existed). This is the correct level for a manual release workflow — the follow-up task #26 (Create GitHub Release) will need the same scope.

### 4. First-release handling

When `PREVIOUS_TAG` is empty (no existing SemVer tags), the API call omits `previous_tag_name`. GitHub then generates notes covering all commits in the repository. This is acceptable for the first release.

The `calculate_version.py` behavior is unchanged: automatic calculation without a SemVer tag fails with a clear error requiring `--version-override`. The "no previous tag" scenario only arises when using `--version-override`.

### 5. Output file path

`artifacts/release-notes/release-notes.md` — a stable unversioned path. Downstream steps (#25, #26, #27) can reference this single file. If version-specific artifact naming is desired later, it applies only at the `upload-artifact` step.

## Risks / Trade-offs

| Risk | Impact | Mitigation |
|---|---|---|
| `gh api` not available in runner | Step fails | `gh` is pre-installed on `ubuntu-latest`; if missing, `actions/checkout` error would occur first |
| API rate limiting on `generate-notes` | Step fails for high-frequency releases | Manual release workflow is low-frequency by nature; acceptable |
| `.github/release.yml` referenced explicitly but also used implicitly by GitHub | Duplicate config reference | Explicit ref ensures the step is self-documenting; GitHub falls back to the same file |
| Large commit range times out API call | Notes truncated or step fails | Acceptable for manual releases; first release is the worst case |
| `workspace_dispatch` with non-default branch | `TARGET_TAG` mismatch with `GITHUB_SHA` | User controls the branch via dispatch; documented in workflow input |
