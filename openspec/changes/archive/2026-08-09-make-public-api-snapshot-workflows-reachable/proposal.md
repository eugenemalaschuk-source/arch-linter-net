## Why

`public-api capture`, `diff`, and `update` reject ordinary build artifacts without a
product receipt, but currently offer no way to create that receipt. This makes the
reviewed-snapshot workflow unreachable from a normal repository checkout.

## What Changes

- Add explicit build preparation options to the receipt-requiring public-API
  operations, with the same semantics as validation's `--ensure-built` and
  `--no-restore` options.
- Re-load and preflight the post-build artifact state before capturing a surface, so
  snapshot operations consume the receipt-backed artifacts they prepared.
- Document the supported capture, diff, and update workflow and add packed-CLI
  acceptance coverage for it, including stale-state rejection.
- Audit equivalent subcommands; baseline operations do not currently invoke
  build-state preflight and therefore do not share this deadlock.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `analysis-build-state-preflight`: allow the public-API application workflow to
  request the existing explicit preparation path while retaining receipt verification.
- `cli-command-dispatch`: expose compatible preparation options on the public-API
  capture, diff, and update command surfaces.
- `public-api-snapshots`: define the supported receipt-backed snapshot workflow.

## Impact

Changes the public CLI option surface, public Core request records, public-API
application-service setup sequence, CLI/help documentation, and executable
acceptance fixtures. No new dependency or architecture layer is introduced.
