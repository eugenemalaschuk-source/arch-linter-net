# Fix semantic selector coverage

## Why

Semantic coverage cannot evaluate source-relative `!{source.metadata.<key>}` contextual selectors because it has no source descriptor. In addition, consumer deduplication uses CLR type names for collection constraints, conflating distinct `in` selectors. The semantic classification specification still documents the superseded role/key-only marker.

## What Changes

- Evaluate source-relative contextual selectors against compatible source and target facts during semantic coverage.
- Use a structural, deterministic selector identity for contextual consumer deduplication.
- Align semantic-classification documentation with the executable full-selector coverage model.

## Impact

- Affected capabilities: `architecture-coverage-reporting`, `semantic-classification-model`
- Affected code: contextual-consumer registry, semantic coverage computation, Core tests
