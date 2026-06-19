## Context

The manual release workflow (`.github/workflows/release-nuget.yml`) already calculates versions, builds, packs, and publishes packages. What it lacks is any release-note configuration. GitHub can auto-generate release notes from merged PRs, but without `.github/release.yml` every change falls into a single generic uncategorized section.

Categories must be defined before the release notes step (#24) can generate notes. The configuration file also establishes a label convention that the rest of the release pipeline depends on.

## Goals / Non-Goals

**Goals:**
- Add `.github/release.yml` defining release-note categories mapped to existing and new GitHub labels
- Create three new labels: `breaking-change`, `dependencies`, `ignore-for-release`
- Configure a catch-all `Other Changes` section for unmatched PRs
- Configure `ignore-for-release` exclusion for internal/noisy changes
- Keep the file focused exclusively on release-note grouping

**Non-Goals:**
- Changing the release workflow (`release-nuget.yml`)
- Generating release notes within the workflow
- Embedding notes into NuGet package metadata
- Creating GitHub Releases
- Label governance, PR templates, or enforcement

## Decisions

### Use existing labels where they overlap, aliases for future migration

Existing `bug` maps to Fixes, existing `enhancement` maps to Features. Also accept `fix` and `feature` as alternative labels so future relabeling doesn't break notes.

`breaking-change`, `dependencies`, and `ignore-for-release` are new labels that don't exist yet. They must be created in the same PR as `release.yml` so the config references real entities.

### Category order defines priority

GitHub processes categories top-to-bottom and assigns a PR to the first matching category. Breaking Changes is first so a PR with both `breaking-change` and `enhancement` lands in the most prominent section.

Catch-all (`labels: ["*"]`) is last. It captures any PR that didn't match earlier categories. Only one `"*"` catch-all is permitted by GitHub — enforced at the YAML level by a comment.

### Exclude `ignore-for-release` as a safety valve

Add `ignore-for-release` to `changelog.exclude.labels` so trivial or internal-only PRs (dependabot config, readme fixes, CI-only tweaks) can be excluded from user-facing release notes without requiring a label cleanup.

## Risks / Trade-offs

| Risk | Impact | Mitigation |
|------|--------|------------|
| New labels don't exist at config commit | Classification silently fails | Create labels in the same change, before or alongside release.yml |
| PRs merged without labels fall to catch-all | "Other Changes" section fills with noise | Acceptable for early stage; enforce later |
| Duplicate `"*"` catch-all added later | GitHub rejects the config | Add YAML comment warning about single catch-all limit |
