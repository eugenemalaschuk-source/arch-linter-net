## ADDED Requirements

### Requirement: Release-note category configuration

The repository SHALL contain `.github/release.yml` that defines label-based categories for GitHub auto-generated release notes.

#### Scenario: Categories group PRs into release-note sections

- **WHEN** GitHub generates release notes from merged PRs
- **THEN** PRs with matching labels SHALL be grouped into the configured sections: Breaking Changes, Features, Fixes, Documentation, CI/CD, Dependencies, Other Changes

#### Scenario: Catch-all captures unlabeled PRs

- **WHEN** a merged PR has no labels matching any configured category
- **THEN** it SHALL appear in the "Other Changes" section

#### Scenario: Exclude label suppresses PRs from notes

- **WHEN** a merged PR has the `ignore-for-release` label
- **THEN** it SHALL be excluded from the generated release notes entirely

### Requirement: Breaking Changes section

The release notes SHALL include a "Breaking Changes" section that captures PRs labeled `breaking-change`, surfaced as the first category in the generated notes.

#### Scenario: Breaking change appears first

- **WHEN** a PR is labeled `breaking-change`
- **THEN** it SHALL appear under the "Breaking Changes" heading, ordered before all other categories

### Requirement: Label mapping

The system SHALL recognize the following existing labels for release-note grouping.

#### Scenario: Bug label maps to Fixes

- **WHEN** a PR has the `bug` label
- **THEN** it SHALL appear under the "Fixes" section

#### Scenario: Enhancement label maps to Features

- **WHEN** a PR has the `enhancement` label
- **THEN** it SHALL appear under the "Features" section

#### Scenario: Documentation label maps to Documentation

- **WHEN** a PR has the `documentation` label
- **THEN** it SHALL appear under the "Documentation" section

#### Scenario: CI label maps to CI/CD

- **WHEN** a PR has the `ci` label
- **THEN** it SHALL appear under the "CI/CD" section
