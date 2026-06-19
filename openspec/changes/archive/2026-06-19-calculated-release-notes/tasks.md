## 1. Extend `calculate_version.py` with release context

- [x] 1.1 Add `DetectedTag(name, version)` dataclass and refactor `detect_latest_tag()` to return `Optional[DetectedTag]`, keeping a compatibility wrapper for existing tests
- [x] 1.2 Add `--github-env` argument that writes `PACKAGE_VERSION`, `TARGET_TAG`, `PREVIOUS_TAG` key=value lines to the specified file
- [x] 1.3 Handle `PREVIOUS_TAG` being empty when no tags exist and `--version-override` is used
- [x] 1.4 Preserve backward compatibility: stdout-only mode when `--github-env` is absent

## 2. Add tests for release context scenarios

- [x] 2.1 Add test: `--github-env` with preview tag exports PACKAGE_VERSION, TARGET_TAG, and PREVIOUS_TAG
- [x] 2.2 Add test: `--github-env` with stable latest tag exports correct PREVIOUS_TAG
- [x] 2.3 Add test: `--github-env` with `--version-override` still detects and exports PREVIOUS_TAG from tags
- [x] 2.4 Add test: `--github-env` with `--version-override` and no tags exports PREVIOUS_TAG as empty
- [x] 2.5 Add test: `--github-env` writes to a file (not stdout)
- [x] 2.6 Verify all existing tests still pass with the `DetectedTag` refactor

## 3. Add release notes generation step to workflow

- [x] 3.1 Raise `release` job permissions from `contents: read` to `contents: write`
- [x] 3.2 Switch `Calculate package version` step from stdout capture to `--github-env $GITHUB_ENV`
- [x] 3.3 Add `Generate release notes` step after version calculation that:
  - Creates `artifacts/release-notes/` directory
  - Builds API payload with `tag_name`, `target_commitish`, `configuration_file_path`, and conditional `previous_tag_name`
  - Calls `gh api repos/${{ github.repository }}/releases/generate-notes` via POST
  - Writes `.body` to `artifacts/release-notes/release-notes.md`
  - Prints the generated notes to the workflow log
- [x] 3.4 Add `echo` statements in the version step to print `TARGET_TAG` and `PREVIOUS_TAG` for log visibility

## 4. Validation

- [x] 4.1 Run `make acceptance` locally to confirm no regressions
- [x] 4.2 Run existing `test_calculate_version.py` tests to confirm all pass with `DetectedTag` refactor
- [x] 4.3 Run the new release context test cases
- [x] 4.4 Review `release-nuget.yml` syntax for correctness
