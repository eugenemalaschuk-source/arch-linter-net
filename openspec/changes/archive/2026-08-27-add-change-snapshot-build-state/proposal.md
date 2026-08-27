## Why

`change snapshot` cannot analyze a consumer that opts into an ASP.NET Core shared
framework, even though the documented post-build validation path succeeds for the
same policy. This leaves architecture-change reports incomplete for a common
framework-dependent consumer and blocks the v0.7 adoption workflow.

## What Changes

- Add the supported build-state options to `change snapshot`: `--ensure-built`,
  `--no-restore`, `--configuration`, `--framework`, `--platform`, and `--runtime`.
- Route an explicit post-build request through every analysis that contributes to a
  snapshot, including validation, namespace and assembly graph projection, and
  optional baseline-debt comparison.
- Keep ordinary snapshot invocation non-building and preserve the snapshot's
  canonical identities, mode semantics, deterministic ordering, and report-only
  comparison behavior.
- Add packaged-CLI regression coverage against the existing ASP.NET Core fixture
  and document the supported invocation.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-change-report`: `change snapshot` gains an explicit post-build
  invocation that produces a complete snapshot for opted-in shared-framework
  consumers.
- `analysis-build-state-preflight`: explicit build-state preparation is available
  to every analysis contributor needed by a change snapshot, with the selected
  output context applied consistently.

## Impact

CLI change-command parsing and help, Core graph and baseline candidate requests,
their build-state preparation paths, Core public-API approval evidence, focused
NUnit coverage, and user-facing CLI/workflow documentation are affected. No policy
schema, shared-framework discovery behavior, snapshot schema, or report semantics
change.
