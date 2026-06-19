## Why

The manual release workflow (`.github/workflows/release-nuget.yml`) builds and packs NuGet packages without first validating repository acceptance. This means a broken build, failing tests, or lint violations can slip into release packages. The release process doc already describes acceptance as a prerequisite, but the workflow doesn't enforce it.

## What Changes

- Add `setup-uv` step to the release workflow (required by `make acceptance`)
- Add `make acceptance` step after `Build` and before all `Pack *` steps in `.github/workflows/release-nuget.yml`
- No changes to package metadata, versioning, NuGet publish logic, GitHub Pages deploy, or CI workflow

## Capabilities

### New Capabilities

None. This is a CI/tooling change, not a product capability.

### Modified Capabilities

None. No spec-level behavior changes.

## Impact

- **File**: `.github/workflows/release-nuget.yml` — two new steps added
- **Runtime**: Release workflow runs full acceptance gate (lint + tests) before packing. Adds ~1–2 min to manual release run.
- **Dependencies**: `uv` required in runner (via `astral-sh/setup-uv` action, already used by CI workflow)
- **Risk**: Low — acceptance is the same gate CI already runs. Release previously skipped it.
