## Why

The #523 reference contract incorrectly says that full selected-diagnostic provenance flows into
the baseline projection. Production deliberately projects only stable canonical identity and
strict/audit debt lifecycle facts so equivalent reruns do not create baseline churn.

## What Changes

- Define separate retention guarantees for canonical findings and rendered/Testing outputs versus
  baseline candidates and applicability records.
- Correct the public current-context reference guide to describe baseline identity and debt
  lifecycle without claiming that it contains producer/run or artifact provenance.
- Archive the corrected delta and update the PR validation summary to report 20 focused cases.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `external-diagnostics-federation`: constrain the baseline projection promise to its stable
  identity and debt lifecycle semantics.

## Impact

The OpenSpec contract, public guide, and PR description change. Production code, public API, and
baseline data model remain unchanged.
