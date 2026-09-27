## ADDED Requirements

### Requirement: GitHub Release publisher accepts the complete package-version contract

The GitHub Release asset publisher SHALL validate the release tag as the canonical lowercase `v` prefix followed by a package version accepted by the existing `version_override` validator. It SHALL accept valid stable, prerelease, and build-metadata versions without narrowing prerelease identifiers to `preview.N`. Malformed version strings SHALL fail before invoking GitHub CLI operations. This identity validation SHALL NOT replace or broaden the separate Checkpoint B publication authorization.

#### Scenario: Publisher accepts prerelease and build-metadata versions

- **WHEN** the release tag is `v0.2.0-rc.1`, `v0.2.0-alpha.1`, or `v0.2.0+build.123`
- **THEN** the publisher accepts the release identity using the shared package-version parser

#### Scenario: Publisher rejects a malformed or noncanonical tag

- **WHEN** the release tag lacks the lowercase `v` prefix or its package version is not accepted by `version_override` validation
- **THEN** the publisher rejects the identity before invoking GitHub CLI operations

#### Scenario: Release-scope authorization remains independent

- **WHEN** the publisher accepts a syntactically valid package version
- **THEN** the candidate still requires its separately verified Checkpoint B release-scope authorization before publication
