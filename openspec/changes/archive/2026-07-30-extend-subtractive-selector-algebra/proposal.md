## Why

The 0.5.1 compatibility promise still implies subtractive selection for policy
families that #356 deliberately did not implement. Authors currently have to
duplicate positive rules or cannot express a narrow exception at all, while
other families already use the established include-minus-exclude algebra.

## What Changes

- Classify every selector-bearing policy family as compatible, already covered,
  intentionally incompatible, or deferred outside the 0.5.1 claim.
- Extend the shared selector algebra to compatible type-placement, layout,
  layer-template/container, and source-scoped package, framework, assembly and
  external-dependency policy families without expanding the configured analysis
  graph.
- Preserve authored/imported provenance and surface normalized inclusion and
  exclusion participation consistently through explain, coverage, and machine
  projections.
- Reject invalid exclusion shapes and make stale exclusions observable whenever
  the selected facts permit it.
- Document the compatibility inventory and clarify that exclusions narrow scope
  and do not replace exact baselines.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `layer-contracts`: retain and generalize the shared subtraction vocabulary and
  evidence guarantees.
- `layer-templates`: apply compatible container/template subtraction.
- `type-placement-contracts`: allow constrained type-selector subtraction.
- `layout-convention-contracts`: allow constrained file/type-selector subtraction.
- `package-dependency-contracts`: allow bounded source subtraction.
- `framework-reference-contracts`: allow bounded source subtraction.
- `external-dependency-contracts`: allow bounded layer-source subtraction.
- `source-set-expansion`: define deterministic source exclusion after expansion.
- `architecture-coverage-inventory`: inventory effective selector participation
  and stale exclusion evidence.
- `explain-command`: expose typed effective selector and provenance evidence.

## Impact

The Core policy model, loader/schema validation, source expansion, family
evaluators, normalized diagnostics, coverage and explain projections, NUnit
fixtures, policy-authoring documentation, capability manifest, and OpenSpec
requirements are affected. Existing policies without exclusions remain
behaviorally unchanged.
