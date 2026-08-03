## Why

Review of #422 found that partial static discovery could authorize persistent cache reuse. Safe cache authorization requires complete evidence; until it exists, every incomplete analysis unit must be explicitly cache-ineligible.

## What Changes

- Make evaluated-manifest collection bounded and symlink-safe.
- Propagate context and eligibility through all preflight outcomes.
- Reject incomplete SDK/import/reference/artifact evidence instead of authorizing reuse.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `analysis-build-state-fingerprints`: clarify complete-or-ineligible authorization, context, containment, and resource bounds.
- `analysis-build-state-preflight`: require eligibility for every selected unit.

## Impact

Core BuildState models, receipts, preflight diagnostics, approval/schema tests, and cache eligibility tests.
