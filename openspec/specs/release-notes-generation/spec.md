## ADDED Requirements

### Requirement: Release notes generation from calculated version
The manual release workflow SHALL generate a release-notes markdown file for the calculated package version using the GitHub `releases/generate-notes` API and the repository `.github/release.yml` category configuration.

#### Scenario: Notes generated for latest preview version
- **WHEN** the workflow calculates `PACKAGE_VERSION=0.2.0-preview.3` and `PREVIOUS_TAG=v0.2.0-preview.2`
- **THEN** the notes step requests generation for tag `v0.2.0-preview.3` with previous tag `v0.2.0-preview.2`
- **AND** the output file `artifacts/release-notes/release-notes.md` contains PRs merged between `v0.2.0-preview.2` and the current commit, grouped by the categories in `.github/release.yml`

#### Scenario: Notes generated after stable release
- **WHEN** the workflow calculates `PACKAGE_VERSION=0.2.0` and `PREVIOUS_TAG=v0.1.0`
- **THEN** the notes step requests generation for tag `v0.2.0` with previous tag `v0.1.0`

#### Scenario: Notes generated for first-ever release (no previous tag)
- **WHEN** `PREVIOUS_TAG` is empty and `PACKAGE_VERSION=0.1.0` is provided via `--version-override`
- **THEN** the notes step requests generation for tag `v0.1.0` without a `previous_tag_name` parameter
- **AND** the API returns all commits in the repository

#### Scenario: Notes printed in workflow logs
- **WHEN** the notes step completes
- **THEN** the generated markdown content is printed to the workflow run log
- **AND** the content matches what is written to the output file

#### Scenario: Notes use configured categories
- **WHEN** the notes step calls the API
- **THEN** the payload includes `configuration_file_path: ".github/release.yml"` to reference the repository release-note category configuration

#### Scenario: Notes step runs in dry-run mode
- **WHEN** `publish=false` (dry-run)
- **THEN** the notes step still generates and prints the release notes file
- **AND** the generated file is available at `artifacts/release-notes/release-notes.md`

### Requirement: Stable output file path
The release notes SHALL be written to a stable unversioned file path so that downstream steps can consume the same file regardless of version.

#### Scenario: Output path is stable
- **WHEN** the release workflow runs for version `0.2.0-preview.3`
- **THEN** the notes are written to `artifacts/release-notes/release-notes.md`
- **AND** when the workflow runs for version `0.3.0`, the notes are written to the same path

### Requirement: Release context export
The version calculation script SHALL export `TARGET_TAG` and `PREVIOUS_TAG` alongside `PACKAGE_VERSION` when invoked with `--github-env`.

#### Scenario: Preview version exports previous tag
- **WHEN** `calculate_version.py` runs with `--github-env $GITHUB_ENV --release-type preview` and the latest tag is `v0.2.0-preview.2`
- **THEN** it exports `PACKAGE_VERSION=0.2.0-preview.3`, `TARGET_TAG=v0.2.0-preview.3`, and `PREVIOUS_TAG=v0.2.0-preview.2`

#### Scenario: Stable version after preview exports previous tag
- **WHEN** `calculate_version.py` runs with `--github-env $GITHUB_ENV --release-type patch` and the latest tag is `v0.2.0-preview.2`
- **THEN** it exports `PACKAGE_VERSION=0.2.0`, `TARGET_TAG=v0.2.0`, and `PREVIOUS_TAG=v0.2.0-preview.2`

#### Scenario: Version override still detects previous tag
- **WHEN** `calculate_version.py` runs with `--github-env $GITHUB_ENV --version-override 0.3.0` and the latest tag is `v0.2.0`
- **THEN** it exports `PACKAGE_VERSION=0.3.0`, `TARGET_TAG=v0.3.0`, and `PREVIOUS_TAG=v0.2.0`

#### Scenario: Version override with no tags exports empty previous tag
- **WHEN** `calculate_version.py` runs with `--github-env $GITHUB_ENV --version-override 0.1.0` and no tags exist
- **THEN** it exports `PACKAGE_VERSION=0.1.0`, `TARGET_TAG=v0.1.0`, and `PREVIOUS_TAG=""`

### Requirement: Permission scope for release notes generation
The release workflow job SHALL have `contents: write` permission to use the `releases/generate-notes` API endpoint.

#### Scenario: Permission allows API call
- **WHEN** the release workflow runs
- **THEN** the job `permissions` block includes `contents: write`
- **AND** the `gh api` call to `repos/{owner}/{repo}/releases/generate-notes` succeeds
