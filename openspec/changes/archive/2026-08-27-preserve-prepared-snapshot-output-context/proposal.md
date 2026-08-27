## Why

`change snapshot --ensure-built` builds and verifies the caller's requested output context, but
its graph and baseline contributors rediscover outputs from policy defaults. An override such as
`--configuration Release`, `--framework net10.0`, or `--runtime win-x64` can therefore turn a
successful preparation into a false blocked snapshot or analyze a different artifact.

## What Changes

- Preserve the receipt-verified artifact selection from snapshot preparation for every prepared
  graph and baseline contributor.
- Extend the prepared-state handoff contract so it conveys the effective configuration, target
  framework, platform, runtime identifier, and selected artifact paths rather than only a boolean.
- Add packaged regressions where CLI overrides differ from policy defaults, including a RID-specific
  selection.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `architecture-change-report`: prepared snapshots consistently use the caller-selected output context.
- `analysis-build-state-preflight`: prepared analysis can consume a supplied, receipt-verified artifact selection.

## Impact

- Core graph and baseline request models and isolated post-build runner setup.
- CLI change-snapshot orchestration and public API approval snapshots.
- Packaged ASP.NET Core acceptance coverage and focused Core/CLI tests.
