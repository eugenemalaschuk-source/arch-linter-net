## Why

The initial fact-and-shape table still marked predicate and cross-field
allowance strings as exact sets. Set subtraction on those textual values can
misclassify a strengthened prefix/glob policy as semantic weakening.

## What Changes

- Restrict semantic set comparison to explicit exact-identity inventories.
- Route prefix, glob, call-pattern, and cross-field location allowances to
  bounded `impact_not_proven` evidence without effective membership proof.
- Add regressions for prefix direction, namespace patterns, and project versus
  assembly composition allowances.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `policy-weakening-guardrails`: Separate exact identity inventories from
  predicate and cross-field union shapes.

## Impact

Changes the internal Core comparator, its NUnit regressions, and review
guidance. It does not change policy-context schema or public API.
