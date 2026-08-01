## Why

The archived cooperative-cancellation change left cancellation gaps in publication,
process cleanup, policy loading, and shared baseline/public-API paths. These gaps can
publish incomplete evidence, lose diagnostics, or return a completed result after a
late cancellation.

## What Changes

- Complete cancellation checks and evidence for report routing.
- Drain child-process async output after polling and bound post-kill cleanup.
- Propagate cancellation through policy loading, build-state hashing/receipts, and
  shared baseline/public-API operations.
- Add final outcome and scan-loop cancellation boundaries with regression coverage.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `cooperative-cancellation`: Complete cancellation coverage for every required shared pipeline surface.

## Impact

Core validation/build-state APIs, CLI report routing, baseline and public-API application seams,
NUnit regression tests, and the cooperative-cancellation specification.
