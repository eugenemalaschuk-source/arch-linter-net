## Context

`ArchitectureBaselineApplicationService` owns the read-only candidate collection
used by baseline verification and the architecture debt gate. Its current
`EnsureBuilt` path creates an ordinary runner before build-state preparation;
that runner can load the selected project output. The validation service already
uses an internal metadata-only preparation and receipt-backed materialization
flow for the same build-state contract.

## Goals / Non-Goals

**Goals:**

- Make gate candidate collection obey the pre-build no-load boundary on every
  platform, including Windows where the lock is observable.
- Preserve the selected output identity through receipt refresh and analyze only
  the refreshed, receipt-verified artifact closure.
- Keep the read-only baseline comparison and existing preflight result shape.

**Non-Goals:**

- Change ordinary baseline verification, baseline-writing commands, policy
  weakening evaluation, CLI syntax, public APIs, receipt formats, or cache
  behavior.
- Add a second build path or make gate use a new abstraction.

## Decisions

### Use the established metadata-only preparation path for ensured baseline verification

When baseline verification requests `EnsureBuilt`, it will load and validate the
policy and selected contract IDs, then prepare the runner metadata without a CLR
assembly load. The build-state preflight will build from that metadata, refresh
the exact receipt-verified artifact closure, and run ordinary post-build
preflight before materializing the runner for candidate collection.

This extends the validation service's proven ordering rather than using the
legacy post-build discovery path. The latter begins with an ordinary runner and
therefore cannot satisfy the Windows file-lock invariant.

### Preserve the current direct-runner path for all other baseline operations

Generation, update, prune, diff, migrate, ordinary verification, and
`--no-restore` without `--ensure-built` retain their existing setup path. Only
the explicit build-capable verification branch needs metadata-only preparation.
This avoids turning a targeted reliability correction into an unrequested
candidate-collection redesign.

### Prove both orchestration and user-facing stale-output behavior

A focused fake-composition test will assert that ensured verification prepares
before materializing a runner and performs receipt-backed post-build
verification. A disposable CLI integration regression will generate a matching
baseline, make its selected output stale, and run `gate --ensure-built
--no-restore`; this executes in the Windows test matrix where pre-fix assembly
locking is reproducible.

## Risks / Trade-offs

- [Prepared metadata is incomplete] → retain the existing fail-closed preflight
  and materialize only after a complete refreshed selection is verified.
- [Candidate analysis diverges from validation output selection] → use the
  existing receipt-to-artifact-closure refresher and prepared-runner
  materialization seam.
- [Integration test adds process time] → keep the disposable single-project
  fixture and use it as focused, platform-independent coverage while Windows CI
  supplies the locking proof.

## Migration Plan

No migration is required. Existing callers retain their flags and result shapes.
The fix ships in the next package build and can be reverted as one focused
change if necessary.
