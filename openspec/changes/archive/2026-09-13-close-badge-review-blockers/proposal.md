## Why

PR #858 still has two correctness gaps in the turnkey Relay path: disabled
renewal emits an active scheduled workflow, and non-hour-aligned cadence values
are rounded up to an hourly cron while the preview reports the lower intended
cost. Both defects make generated behavior disagree with the approved plan and
the bounded-cost contract.

## What Changes

- Generate the Relay renewal workflow only when `renewal.enabled` is true.
- Generate exact UTC cron trigger slots for every supported cadence, using
  multiple POSIX schedule entries when one expression cannot represent the
  interval, so the configured daily trigger count matches the cost preview.
- Keep the registry event allowlist and managed-file list consistent with the
  optional workflow's presence.
- Add regression and conformance coverage for disabled renewal and a
  non-hour-aligned cadence such as 90 minutes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `openspec/specs/badge-turnkey-setup/spec.md`: optional renewal output and
  generated schedule semantics must agree with configuration and bounded cost.

## Impact

The change affects the CLI setup output writer, generated Relay workflow
templates, setup conformance tests, the turnkey setup guide, and the archived
turnkey setup contract. No public API, OIDC trust boundary, or promotion
protocol changes are required.
