## Why

The initial applicability contract made membership visible only on produced
evidence records. A missing record could therefore remove the only proof that a
control was required, allowing a downstream summary to shrink its denominator.
It also left membership/state combinations incomplete, so independent family
implementations could disagree on optional and not-applicable inputs.

## What Changes

- Define a canonical expected applicability-membership collection, independent
  of produced evidence records, for every effective v0.8 control.
- Require summaries to left join expected controls to records and synthesize an
  unassessable missing-record outcome without inferring membership.
- Define the exhaustive membership × assessability state invariant table,
  including supplied-but-invalid optional evidence.
- Preserve #685 as a consumer of canonical control identity, not a second
  applicability or policy-counting engine.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `governance-applicability-evidence`: Make expected membership independently
  materialized and complete the membership/state invariants.

## Impact

- Updates only the shared OpenSpec design/spec for #505 and its downstream
  implementation contracts (#506/#507 and opting-in v0.8 families).
- Does not add production code, a policy schema, public API, CLI behavior, or
  current runtime behavior.
