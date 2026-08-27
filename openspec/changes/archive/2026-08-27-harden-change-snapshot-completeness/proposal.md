## Why

The newly supported post-build `change snapshot` path can write a partial snapshot when its optional baseline contributor is blocked. It also repeats the expensive graph build for each contributor, even though all contributors must describe one coherent post-build state.

## What Changes

- Fail snapshot creation without writing an artifact when any requested baseline-debt contributor cannot complete, preserving its typed preflight diagnostics for CLI reporting.
- Prepare an ensure-built project graph exactly once per snapshot, then have validation, graph projections, and baseline debt consume the verified receipt-backed post-build state.
- Approve the intentional additive Core API surface in both maintained public-API baselines and add regression coverage for failure, build-count, and API approval behavior.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-change-report`: a snapshot with requested baseline debt is complete only when that contributor succeeds, and its ensure-built contributors share one prepared state.
- `analysis-build-state-preflight`: an already prepared receipt-backed state can be consumed without a second build while retaining fail-closed preflight verification.

## Impact

The change command, Core graph and baseline build-state request handling, public API approval evidence, NUnit tests, and the existing change-report/build-state specifications are affected. Snapshot schema, policy syntax, and ordinary non-building snapshot behavior are unchanged.
