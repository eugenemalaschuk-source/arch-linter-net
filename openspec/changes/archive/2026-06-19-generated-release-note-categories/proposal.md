## Why

The manual release workflow can build and publish packages, but GitHub-generated release notes have no category configuration. Without `.github/release.yml`, every merged PR falls into a generic uncategorized section, making release notes harder for users to scan for breaking changes, features, and fixes.

## What Changes

- Add `.github/release.yml` with release-note categories for breaking changes, features, fixes, documentation, CI/CD, dependencies, and a catch-all section
- Create missing labels `breaking-change`, `dependencies`, and `ignore-for-release` that the release-note config references
- No workflow logic changes, no release notes generation, no NuGet metadata changes — this file is strictly the category definition

## Capabilities

### New Capabilities

- `release-note-categories`: GitHub release note category configuration via `.github/release.yml` with label-based section mapping

### Modified Capabilities

- None

## Impact

- `.github/release.yml` — new file, ~35 lines of YAML
- GitHub labels — three new labels created (`breaking-change`, `dependencies`, `ignore-for-release`)
- No runtime code changes, no API changes, no dependency changes
