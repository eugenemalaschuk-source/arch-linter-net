## Why

Review found two deterministic provenance defects in specialized output and
template binding, plus JSON shape drift in optional location fields.

## What Changes

- Order classification path locations by encounter ordinal.
- Bind generated layer templates by stable owner identity, not display name.
- Share the optional-field JSON location mapping for exception output.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `policy-import-composition`: all provenance consumers retain encounter order and exact template ownership.
- `violation-reporting`: policy exception JSON matches ordinary diagnostic shape.

## Impact

Classification diagnostics, template expansion, JSON reporting, and tests.
