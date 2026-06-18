## Why

ArchLinterNet needs a controlled way to publish preview NuGet packages for First Ice consumption. Currently there are no GitHub Actions workflows, no package publication process, and no separation between validation CI and release builds. Without an explicit manual release pipeline, package publication stays ad-hoc and there is no dry-run safety net before pushing public packages.

## What Changes

- Add `.github/workflows/ci.yml` for pull request and push validation (restore, build, test only).
- Add `.github/workflows/release-nuget.yml` for manual package build and optional NuGet.org publication.
- PR CI workflow never packs official versioned packages, never reads `NUGET_API_KEY`, never publishes.
- Release workflow uses `workflow_dispatch` with explicit `version` and `publish` inputs.
- Release workflow builds, tests, packs versioned `.nupkg` artifacts, uploads them as workflow artifacts, and publishes only when `publish=true`.
- Update `docs/reference/release-process.md` to document the new two-step manual release procedure.

## Capabilities

### New Capabilities

- `github-actions-ci`: Pull request and push validation workflow with restore, build, and test steps.
- `manual-nuget-release`: Manual workflow_dispatch release workflow with dry-run and publish modes.
- `release-process-documentation`: Documented two-step manual release procedure for preview packages.

### Modified Capabilities

None.

## Impact

- `.github/` directory: new workflow files added.
- `docs/reference/release-process.md`: updated to reflect new manual release flow.
- Repository secrets: requires `NUGET_API_KEY` to be configured for publish step.
- No runtime code changes, no package API changes, no dependency changes.
