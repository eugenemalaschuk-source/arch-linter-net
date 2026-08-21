## Why

The policy-context export contains authored analysis lists, not the effective
scanner/discovery scope. Comparing source roots, target assemblies, or projects
as literal inventories can misclassify an expansion through path containment,
scanner defaults, or project discovery as semantic weakening.

## What Changes

- Classify changed authored `source_roots`, `target_assemblies`, and `projects`
  as `impact_not_proven` until the context carries trusted effective scope
  evidence.
- Preserve existing bounded handling for project include/exclude globs.
- Add regressions for broader and empty source-root lists and a discovery-backed
  empty authored target-assembly list.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `policy-weakening-guardrails`: Bound authored analysis-scope comparisons by
  effective discovery and scanner evidence.

## Impact

Changes the internal Core comparator, NUnit regression coverage, and review
guidance only. It does not invoke project discovery or alter policy-context
schema in this correction.
