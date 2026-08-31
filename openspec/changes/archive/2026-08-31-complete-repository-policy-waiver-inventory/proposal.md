## Why

`architecture-policy-inventory/v1` now counts strict, audit, and coverage
controls at repository scope, but its waiver records still come only from the
mode that produced the current validation outcome. That leaves no Core-owned
repository total when waivers exist in both strict and audit policy.

## What Changes

- Collect canonical waiver lifecycle records for every selected policy mode
  before projecting each outcome's policy inventory.
- Preserve `ValidationOutcome.Waivers` and its gating behavior as the current
  mode's lifecycle evidence.
- Make strict and audit outcomes expose identical repository-level inventory
  rule and waiver-debt evidence, including the canonical waiver drill-down.
- Document the additional companion-mode lifecycle evaluation and cover it
  with an end-to-end strict/audit regression test.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-policy-inventory`: require inventory waiver debt and records to
  cover every selected strict and audit policy mode.
- `analysis-snapshot`: permit repository inventory construction to evaluate a
  selected companion mode for its lifecycle evidence while retaining
  mode-specific result and gating fields.

## Impact

Core snapshot evaluation and cache-backed outcome reconstruction, policy
inventory documentation and specifications, and focused Core end-to-end tests.
