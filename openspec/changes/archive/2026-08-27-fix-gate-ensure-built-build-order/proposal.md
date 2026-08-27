## Why

`gate --ensure-built` currently loads selected assemblies while collecting the
baseline candidates, before it invokes its own graph build. On Windows, that
process-held assembly handle blocks MSBuild from replacing a stale selected
output, so a valid gate run fails solely because the gate owns its build.

## What Changes

- Route `gate --ensure-built` candidate collection through the existing
  metadata-only preparation, receipt-backed build, and post-build materialization
  sequence.
- Ensure that candidate analysis begins only after the refreshed selected
  artifacts have passed ordinary receipt verification.
- Add a focused stale-output CLI gate regression that exercises the rebuilt
  candidate path, including Windows execution in the existing test matrix.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `analysis-build-state-preflight`: Explicit preparation also guarantees the
  metadata-before-build ordering for architecture debt-gate candidate collection.

## Impact

Changes are confined to Core baseline verification orchestration and its focused
regression coverage. The CLI flags, policy and baseline schemas, public APIs,
receipt schema, diagnostic schema, ordinary gate behavior, and policy-weakening
comparison remain unchanged.
