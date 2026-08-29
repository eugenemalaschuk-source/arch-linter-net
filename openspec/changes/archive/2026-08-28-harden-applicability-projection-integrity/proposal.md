## Why

The initial #505 correction made expected membership independent of evidence
records, but it still conflates a valid produced-record state with integrity of
the collection joined to those expectations. It also leaves orphan records and
irrelevant native evidence dimensions able to be handled inconsistently by
later family evaluators and projections.

## What Changes

- Separate the state of a valid produced applicability record from the
  integrity outcome of the expected-to-produced collection join.
- Require an anti-join before the canonical left join so unknown/orphan record
  identities make the collection visibly unassessable.
- Define sparse, typed native evidence dimensions: only dimensions meaningful
  to both the family and the configured control are materialized.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `governance-applicability-evidence`: Make collection-integrity outcomes,
  orphan detection, and native-evidence dimensions deterministic without
  changing v0.8 runtime behavior.

## Impact

- Updates the design-only #505 OpenSpec contract consumed by #506, #507, and
  later opting-in governance families.
- Adds no production code, policy schema, public API, CLI behavior, or
  effective-policy inventory/counting behavior.
