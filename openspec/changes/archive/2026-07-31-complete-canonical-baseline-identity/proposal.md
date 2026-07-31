## Why

Baseline document version 2 already carries structured identities, but several registered
finding families still derive their identity from display-oriented fields. This can let one
reviewed baseline entry suppress multiple results or make a previously precise entry behave
as though it were broadly scoped.

## What Changes

- Audit every registered finding family and publish a generated, test-backed classification
  of its baseline capability and identity dimensions.
- Complete canonical `identity_version: 1` construction for every baseline-capable family,
  including source-set-expanded instances and repeated semantic occurrences.
- Make baseline lifecycle, Testing, and SARIF comparison surfaces use the same identity and
  report requalified entries as reviewable changed or stale results rather than matching them
  broadly.
- Document the recapture/update-and-prune migration path for each newly qualified identity.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `baseline-generation`: define complete family coverage, exact identity matching, and safe
  requalification lifecycle behavior.
- `diagnostics-model`: require the typed finding inventory to expose the canonical identity
  used by all projections.
- `contract-family-registry`: require a complete baseline-capability classification for each
  registered family.
- `source-set-expansion`: preserve authored provenance and concrete instance identity in
  baseline keys.
- `adoption-stabilization-compatibility`: document changed/stale handling and the explicit
  recapture path for requalified v2 identities.

## Impact

Core finding creation and baseline comparison, generated inventory tests, CLI and Testing/SARIF
projection tests, baseline migration guidance, and the affected OpenSpec capabilities.
