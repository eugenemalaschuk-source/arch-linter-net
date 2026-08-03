## Why

The evaluated-manifest cache guard now fails closed, but four production-path gaps still leave
Platform/RID identity incomplete, trust repository paths through reparse-point ancestors, permit
unbounded traversal after an input budget is exhausted, or attach a false mismatch reason to a
matching cache-ineligible receipt.

## What Changes

- Carry Platform and RuntimeIdentifier from validation and snapshot requests through preflight,
  output selection, receipt publication, and receipt verification.
- Reject any selected build input whose path has a symbolic-link or junction ancestor, and stop
  recursive collection as soon as either input budget is exhausted.
- Treat a receipt whose manifest digest, eligibility, and rejection reasons match the current
  manifest as consistent even when the agreed outcome is `cache-ineligible`.
- Add end-to-end and bounded-collection regression tests for these outcomes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `analysis-build-state-fingerprints`: require physical repository containment, bounded traversal,
  and stable cache-ineligible receipt agreement.
- `analysis-build-state-preflight`: require production propagation of Platform/RID through
  validation, build preparation, output resolution, and receipt verification.

## Impact

Affected code includes Core validation/snapshot request models, preflight and preparation
services, evaluated manifest collection, receipt verification, public API approvals, schemas,
and Core NUnit tests. No third-party dependency changes are required.
