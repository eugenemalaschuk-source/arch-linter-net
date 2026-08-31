## Why

The initial effective-policy inventory projected only the validation mode that
produced it and did not apply authored source-set selection identities to waiver
lifecycle evaluation. That leaves future repository-level consumers without one
authoritative strict/audit/coverage inventory and can hide blocking waiver debt
from a selected authored contract.

## What Changes

- Make `architecture-policy-inventory/v1` a repository-level inventory over the
  effective selected policy, with a complete strict/audit/coverage partition
  regardless of the invocation mode.
- Treat an authored source-set contract ID as selecting all of its expanded
  aliases for waiver-lifecycle evaluation as well as rule execution and
  inventory projection.
- Synchronize the reviewed whole-Core public API approval baseline with the
  intentionally expanded Core surface.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-policy-inventory`: require one repository-level rule authority
  across strict, audit, and coverage controls.
- `architecture-waiver-lifecycle`: require authored source-set selection IDs to
  preserve every selected waiver lifecycle record.

## Impact

Core inventory projection and validation wiring, waiver selection, Core tests,
the whole-Core approval baseline, cache/CLI/Testing projections, and the two
existing capability specifications.
