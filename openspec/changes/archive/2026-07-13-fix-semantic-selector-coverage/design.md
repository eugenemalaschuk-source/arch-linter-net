## Context

Contextual contracts contain selectors in source and target positions. A source-relative target constraint must be considered governed when at least one classified source fact makes the selector match a classified target fact. The existing registry is intentionally independent of execution order, so it must retain enough positional information to perform that existential check.

## Decisions

- Register contextual consumers as either standalone selectors or source/target selector pairs. A paired consumer is governed when a classified source matches the source selector and the classified target matches the target selector with that source descriptor.
- Retain a canonical structural identity that serializes metadata values recursively, including sequence contents, in sorted key order; use it only for deduplication and diagnostic ordering.
- Preserve standalone registration for contextual `source` selectors, while register target and exclude selectors paired to their source selector.
- Update the semantic-classification requirement to describe full executable selectors rather than `(role, metadata key)` markers.

## Risks and mitigations

- Pair-aware matching can evaluate more combinations, but it is limited to the session's classified types and coverage contracts. Tests cover `!{source.metadata.*}` and distinct `in` lists.
