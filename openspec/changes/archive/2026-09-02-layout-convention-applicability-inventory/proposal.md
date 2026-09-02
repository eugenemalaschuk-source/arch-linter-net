## Why

Layout-convention selectors currently validate only source files they already match. When a
reviewed convention folder or selector drifts, a zero-subject result can therefore look clean
instead of showing that the intended architecture surface was not assessed. The shared
applicability-evidence and normalized-projection seams are now available to make that gap
explicit without a second layout-specific result model.

## What Changes

- Add opt-in expected convention/folder inventory declarations for strict and audit layout
  convention controls, bounded by their existing source-file selector scope.
- Record deterministic layout applicability evidence for missing expected items, unexpected
  zero matches, unmapped observed subjects in declared exhaustive scope, and conflicting
  mutually-exclusive convention mappings.
- Route that evidence through the existing applicability evaluator and normalized Human, JSON,
  SARIF, Testing, and baseline projection path; audit remains the default for the new advisory
  drift reporting and strict enforcement is explicit.
- Preserve the behavior of existing layout-convention policies that do not declare an
  applicability inventory.
- Document the inventory schema, bounded scope, audit/strict behavior, and deterministic
  non-goals.

## Capabilities

### New Capabilities

<!-- None. -->

### Modified Capabilities

- `layout-convention-contracts`: Layout convention controls can opt into a bounded expected
  inventory and exhaustive coverage assessment, producing shared applicability evidence instead
  of silently accepting unassessed subjects.
- `governance-applicability-evidence`: The shared applicability contract recognizes
  layout-convention inventory evidence and preserves its stable control identity and provenance.

## Impact

The change affects Core YAML contracts and validators, source-fact layout evaluation, shared
applicability transport, normalized reporting adapters, Core/CLI tests, public policy
documentation, and the self-policy/schema fixtures. It adds no runtime dependencies and does
not create a general filesystem traversal or fuzzy-matching API.
