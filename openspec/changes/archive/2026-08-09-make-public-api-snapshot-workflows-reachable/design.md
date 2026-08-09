## Context

The public-API application service creates a runner, runs ordinary build-state
preflight, and captures the surface. It has no input through which a caller can
request the existing receipt-producing build preparation mode. A normal `dotnet
build` therefore produces an artifact that correctly remains untrusted, while the
CLI cannot request the product-owned preparation that would establish trust.

## Goals / Non-Goals

**Goals:**

- Make all surface-reading public-API operations reachable through explicit,
  receipt-backed preparation.
- Preserve the ordinary fail-closed default and reuse validation's build-state
  preparation semantics, including `--no-restore`.
- Ensure the surface is captured from a newly reloaded, receipt-verified
  post-build runner.

**Non-Goals:**

- Accepting manually built artifacts without a receipt.
- Introducing automatic snapshot approval or changing snapshot write safeguards.
- Adding build-state preparation to baseline operations, which do not run
  build-state preflight today.

## Decisions

### Expose preparation consistently on every public-API operation

`capture`, `diff`, `update`, and `migrate` all resolve the same live surface;
each receives `--ensure-built` and `--no-restore`, which are forwarded through
typed Core requests. Including `migrate` closes the same otherwise hidden
deadlock instead of leaving an equivalent public command unreachable.

Adding the options only to capture/diff/update was rejected because migrate
uses the same receipt-requiring path. Relaxing receipt requirements for ordinary
build outputs was rejected because it violates the existing fail-closed model.

### Recreate the runner after successful preparation

The service first creates a runner sufficient to identify the selected graph,
runs preflight in the requested mode, then recreates the runner and runs
ordinary preflight after successful `EnsureBuilt`. The second runner supplies
the verified post-build assembly bytes to the scanner and prevents reading the
preparation-era state.

Reusing the original runner was rejected because it can retain missing or
pre-build assembly resolution results. Invoking preparation as a separate
wrapper command was rejected because it recreates the unsupported workflow the
issue prohibits.

### Keep request context minimal and aligned with existing public-API options

The public-API request models add preparation mode and no-restore only. They do
not add validation-only configuration, target-framework, platform, or RID
selection options because public-API commands have no such public surface today;
the fix needs the existing default build context, not a new matrix of selectors.

## Risks / Trade-offs

- A prepared operation performs an extra runner setup → this is necessary to
  scan verified post-build artifacts; focused tests assert the sequence.
- Added CLI switches could drift between subcommands → shared option creation
  and command-definition tests keep them uniform.
- Installed-tool behavior can differ from direct project execution → acceptance
  coverage exercises the packed/installed CLI fixture.

## Migration Plan

No persisted data migration is required. Existing commands keep ordinary
fail-closed behavior; callers use `--ensure-built` (and optionally
`--no-restore`) on the public-API command that needs preparation. Reverting the
change restores the previous command surface without invalidating snapshots or
receipts.

## Open Questions

None.
