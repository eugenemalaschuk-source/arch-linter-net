## Why

The main NuGet package smoke workflow restores projects beneath `$RUNNER_TEMP` and then passes
`obj/project.assets.json` to `verify_restored_main_packages.py`. Repository-workspace confinement
rejects this runner-owned input, causing the post-merge package verification job to fail.

## What Changes

- Validate the verifier's read-only `--assets` input against the current resolved `RUNNER_TEMP` root.
- Reject arbitrary paths outside that root before reading them.
- Add CLI regression coverage for accepted runner-temp assets and rejected external assets.

## Capabilities

### Modified Capabilities

- `release-tooling-workspace-confinement`: include the main NuGet assets file among the explicitly
  bounded runner-temp verification inputs.

## Impact

- `tools/release/verify_restored_main_packages.py` and its tests.
- The main NuGet Builds workflow's post-restore smoke verification.
