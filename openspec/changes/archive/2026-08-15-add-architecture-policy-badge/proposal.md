## Why

The repository currently proves its own architecture policy in CI, but that
product-specific trust signal is not visible to GitHub visitors. A dynamic
badge should expose only a successful strict self-policy result and must be
published by trusted default-branch automation, never by a manually maintained
README value.

## What Changes

- Add a dynamic GitHub Actions status badge for the strict ArchLinterNet
  self-policy result.
- Refresh the badge source from trusted `main` CI without committing badge text
  or creating refresh commits.
- Add the architecture-policy badge and precise semantics to the README and
  CI-integration guide, keeping architecture coverage separate from test
  coverage.
- Extend CI requirements to fail closed: a failed strict self-policy result
  cannot publish a passing architecture badge.

## Capabilities

### New Capabilities

- `architecture-policy-badge`: Dynamic public badge data proving the latest
  successful strict self-policy validation of the default branch.

### Modified Capabilities

- `github-actions-ci`: Trusted default-branch CI publishes the architecture
  badge data only after strict self-policy validation succeeds.

## Impact

Affected areas are a dedicated CI workflow, a focused workflow-contract test,
Make targets, README, CI integration guidance, and the associated OpenSpec
requirements. No product policy schema, test-coverage metric, or
release-forensics behavior changes.
