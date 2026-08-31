## Why

Review of issue #523 found that its reference contract overstates the information carried by the
shared applicability projection and leaves two acceptance properties vulnerable to regression:
location-only identity and policy-authorized rule selection. The public documentation also
misstates the optional-evidence absence outcome.

## What Changes

- Clarify that canonical finding, baseline, Human, JSON, SARIF, and Testing projections retain
  selected diagnostic provenance, while applicability reports only the external-evidence control
  state and reason codes.
- Add synthetic reference coverage for location-only canonical-identity isolation and end-to-end
  `rule_ids` authorization through selection, normalized findings, baseline, and outputs.
- Correct the optional-evidence documentation and archive the final OpenSpec delta through the
  standard workflow.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `external-diagnostics-federation`: separate diagnostic-provenance and applicability guarantees
  and require the missing acceptance coverage.

## Impact

Reference tests, the external-evidence guide, and the federation specification change. Production
code, public APIs, dependencies, and trust/selection models do not change.
