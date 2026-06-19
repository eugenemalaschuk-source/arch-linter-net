## ADDED Requirements

### Requirement: Version detection from git tags
The system SHALL detect the latest SemVer-compatible release tag from repository tags to determine the base version for increment calculations.

#### Scenario: Detects latest stable tag
- **WHEN** the repository has tags `v0.1.0` and `v0.0.9`
- **THEN** the system detects `v0.1.0` as the latest stable version

#### Scenario: Detects latest preview tag
- **WHEN** the repository has tags `v0.1.0-preview.1`, `v0.1.0-preview.2`, and `v0.0.9`
- **THEN** the system detects `v0.1.0-preview.2` as the latest version

#### Scenario: Stable tag has higher precedence than preview
- **WHEN** the repository has tags `v0.1.0` and `v0.1.0-preview.3`
- **THEN** the system detects `v0.1.0` as the latest version because stable releases have higher SemVer precedence than prereleases

#### Scenario: Non-SemVer tags are ignored
- **WHEN** the repository has tags `v0.1.0`, `release-candidate-1`, and `build-123`
- **THEN** the system detects `v0.1.0` and ignores non-SemVer tags

#### Scenario: No valid tags produces clear error
- **WHEN** the repository has no tags or no SemVer-compatible tags
- **THEN** the system fails early with an error message indicating no valid base version was found

#### Scenario: Numeric prerelease parts are compared as numbers
- **WHEN** the repository has tags `v0.1.0-preview.9` and `v0.1.0-preview.10`
- **THEN** the system detects `v0.1.0-preview.10` as the latest version

### Requirement: Version calculation from release type
The system SHALL calculate the next package version deterministically based on the selected release type and the latest detected tag.

#### Scenario: Preview increments from latest preview
- **WHEN** the latest tag is `v0.1.1-preview.2` and release type is `preview`
- **THEN** the calculated version is `0.1.1-preview.3`

#### Scenario: Preview starts after latest stable
- **WHEN** the latest tag is `v0.1.0` and release type is `preview`
- **THEN** the calculated version is `0.1.1-preview.1`

#### Scenario: Patch finalizes preview train
- **WHEN** the latest tag is `v0.1.1-preview.2` and release type is `patch`
- **THEN** the calculated version is `0.1.1`

#### Scenario: Patch after stable increments patch
- **WHEN** the latest tag is `v0.1.0` and release type is `patch`
- **THEN** the calculated version is `0.1.1`

#### Scenario: Minor bumps minor version
- **WHEN** the latest tag is `v0.1.0` and release type is `minor`
- **THEN** the calculated version is `0.2.0`

#### Scenario: Major bumps major version
- **WHEN** the latest tag is `v0.1.0` and release type is `major`
- **THEN** the calculated version is `1.0.0`

#### Scenario: Minor after preview produces stable
- **WHEN** the latest tag is `v0.1.1-preview.2` and release type is `minor`
- **THEN** the calculated version is `0.2.0`

#### Scenario: Major after preview produces stable
- **WHEN** the latest tag is `v0.1.1-preview.2` and release type is `major`
- **THEN** the calculated version is `1.0.0`

#### Scenario: Version is always output without v prefix
- **WHEN** the latest tag is `v0.1.0` and release type is `patch`
- **THEN** the calculated version is `0.1.1` (no `v` prefix)

### Requirement: Manual version override
The system SHALL accept an explicit version override that bypasses tag detection for emergency recovery scenarios.

#### Scenario: Override bypasses detection
- **WHEN** `version_override` is set to `0.2.0-rc.1`
- **THEN** the system outputs `0.2.0-rc.1` without checking repository tags

#### Scenario: Override with v prefix is stripped
- **WHEN** `version_override` is set to `v0.2.0-rc.1`
- **THEN** the system outputs `0.2.0-rc.1` (v prefix is stripped)

#### Scenario: Override accepts stable versions
- **WHEN** `version_override` is set to `1.0.0`
- **THEN** the system outputs `1.0.0`

### Requirement: Version validation
The system SHALL validate any calculated or provided version against a SemVer-compatible NuGet version pattern before proceeding.

#### Scenario: Invalid override is rejected
- **WHEN** `version_override` is set to `not-a-version`
- **THEN** the system fails with a clear validation error

#### Scenario: Empty override is allowed when release type is provided
- **WHEN** `version_override` is empty and `release_type` is `preview`
- **THEN** the system calculates the version from tags normally
