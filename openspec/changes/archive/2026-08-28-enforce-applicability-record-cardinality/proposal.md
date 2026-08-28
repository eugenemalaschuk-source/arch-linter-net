## Why

The expected-membership left join is deterministic only when each expected
control has at most one produced applicability record. Without an explicit
cardinality rule, duplicate records could inflate an evaluability numerator or
produce ambiguous evidence.

## What Changes

- Require zero or one produced applicability record per expected control.
- Classify duplicate records as unassessable contract-integrity evidence.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `governance-applicability-evidence`: Make expected-to-record join cardinality
  deterministic and fail closed on duplicates.

## Impact

- Refines only the #505 OpenSpec contract before any implementation consumes
  it. No production behavior, public API, schema, or output is added.
