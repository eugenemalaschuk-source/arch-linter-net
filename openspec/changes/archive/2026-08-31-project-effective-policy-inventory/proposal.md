## Why

Architecture Health, PR reports, and the public badge need a shared answer to
two current-policy questions: how many effective controls are configured, and
how much explicit waiver debt exists.  Deriving either answer from YAML or
from findings would make downstream consumers disagree and would hide stale or
invalid waiver debt.

## What Changes

- Add a Core-owned, versioned effective-policy inventory projection with one
  deterministic total and mode/family breakdown for effective controls.
- Project the existing canonical waiver-lifecycle records into a distinct
  explicit waiver-debt summary and stable drill-down records without
  re-evaluating waiver matching or expiry.
- Expose the same inventory through validation, Testing, and CLI human/JSON
  output so consumers do not parse policy YAML or infer counts from findings.
- Document the inventory boundary and its distinction from baseline finding
  debt and intentional selector/scope exclusions.

## Capabilities

### New Capabilities

- `architecture-policy-inventory`: Deterministic effective-control and explicit
  waiver-debt inventory for the analyzed architecture policy context.

### Modified Capabilities

- `test-adapter`: Testing projections expose the canonical policy inventory
  from the validation result without recomputing it.

## Impact

- Core validation models, evaluation projection, cache reconstruction, and
  public Core API surface.
- Testing result models and public Testing API surface.
- CLI validation human/JSON rendering and public documentation.
- No new rule evaluator, waiver matcher, baseline lifecycle, or policy
  weakening implementation.
