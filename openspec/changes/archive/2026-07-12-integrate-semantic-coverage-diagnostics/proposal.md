## Why

Semantic role discovery is now available to selectors and contextual contracts, but architecture coverage does not report when those semantic facts are unclassified, ungoverned, stale, excluded, or conflicting. That leaves a new class of architecture blind spots invisible to CI and policy authors.

## What Changes

- Add semantic-role coverage as an implemented scope of the existing coverage contract family.
- Classify discovered role/metadata facts as covered by selector-backed layers or contextual contracts, explicitly excluded, or uncovered.
- Report stale semantic selectors, unresolved/ambiguous classification, and conflict evidence separately from dependency violations.
- Expose deterministic human, JSON, and CI diagnostics, including exclusion reasons and representative evidence.
- Add Sales/Inventory/SharedKernel and Unity/client convention fixtures and update policy-authoring documentation.
- Preserve existing non-semantic coverage behavior and require reasons for semantic exclusions.

## Capabilities

### New Capabilities

None. This is an integration of the existing semantic classification and architecture coverage capabilities.

### Modified Capabilities

- `architecture-coverage-model`: add the `semantic_role` scope and its classification/exclusion rules.
- `architecture-coverage-reporting`: add semantic coverage summaries, evidence buckets, and diagnostics.
- `semantic-classification-model`: make the documented future coverage integration executable for implemented role sources and selectors.

## Impact

The Core coverage contract model, validator, analysis session, role index, contextual-consumer registration, and diagnostic formatters will change. CLI output and CI artifact JSON gain additive semantic coverage sections. NUnit fixtures and documentation will be updated; no new external dependency or runtime behavior validation is introduced.
