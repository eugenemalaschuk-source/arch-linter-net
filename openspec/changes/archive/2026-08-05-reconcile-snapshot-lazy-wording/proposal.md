## Why

The first PR #431 corrective archive updated snapshot preparation and counters,
but an earlier overview and multi-mode scenario still imply that a CLR-backed
session exists before cache lookup. That is inconsistent with the owning lazy
materialization requirement.

## What Changes

- Make the snapshot purpose describe an immutable preparation plan and at-most-
  one lazy runner materialization.
- Make multi-mode evaluation explicitly permit cache-only outcomes before a
  session exists, while retaining one shared session after the first miss.

## Impact

- Affected spec: `analysis-snapshot`.
- No runtime behaviour, public API, or release-gate scope changes.
