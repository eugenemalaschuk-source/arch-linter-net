## Why

Baseline comparison commands already calculate canonical, versioned identities and
statuses, but machine consumers cannot consume those results from SARIF or the
Testing API. This forces integrations and tests to parse display output, contrary
to the exact-identity baseline contract.

## What Changes

- Add SARIF output for `baseline diff`, `baseline verify`, and `baseline migrate`.
  Each comparison entry will expose its canonical identity and lifecycle status as
  structured SARIF properties.
- Add a Testing API for running the same baseline comparison operations and
  asserting their typed entries and statuses.
- Document the SARIF and Testing API comparison surfaces in the baseline guide and
  AI authoring guidance.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `baseline-generation`: baseline comparison results gain SARIF and Testing API projections.
- `sarif-diagnostics-output`: SARIF supports canonical baseline-comparison results.
- `test-adapter`: test authors can inspect typed baseline comparison outcomes.

## Impact

Affected areas are the CLI baseline command dispatcher and report formatters,
`ArchLinterNet.Core` baseline comparison models, `ArchLinterNet.Testing`, their
unit/integration tests, and the migration-baseline and AI policy-authoring guides.
Existing human and JSON baseline output, ordinary validation SARIF, and ordinary
validation Testing APIs remain unchanged.
