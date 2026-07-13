## Context

Semantic coverage reuses contextual dependency selectors as evidence that a classification fact is governed. The existing registry retains only a role and metadata key, which loses the selector's constraint semantics. The YAML loader intentionally ignores unmatched properties, so semantic exclusion mappings need a focused raw-YAML validation pass.

## Decisions

- Register an immutable copy of each complete contextual selector and use `ArchitectureContextSelectorMatcher` for both governed and stale checks.
- Deduplicate registered selectors by a deterministic selector description while retaining their metadata values for matching.
- Validate every semantic-role coverage exclusion mapping against its allowed keys (`role`, `metadata`, `reason`) before deserialization.
- Do not add stale evidence for an externally owned selector layer, preserving the existing external empty-selector suppression rule.

## Risks and mitigations

- Context selectors with value constraints will now expose previously hidden uncovered or stale evidence. Regression tests cover literal, collection, and wildcard selector behavior through the existing matcher.
- Raw validation is limited to semantic-role exclusions so compatibility for other permissively loaded YAML remains unchanged.
