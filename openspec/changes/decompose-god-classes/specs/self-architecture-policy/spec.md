## ADDED Requirements

### Requirement: Production types are not handwritten partial aggregates

The repository self-policy SHALL first measure source declaration counts for production `src` types
in audit mode and, after migration, SHALL strictly require every governed production type to have at
most one handwritten source declaration. The strict rule SHALL not govern test fixtures that model
C# partial-type semantics.

#### Scenario: A new production partial aggregate fails the strict gate

- **WHEN** a production type is split across two handwritten source files after the strict rule is
  enabled
- **THEN** `make lint-architecture` fails with the type name and both declaration paths

#### Scenario: A partial-language test fixture remains analyzable

- **WHEN** a test fixture deliberately declares one type across multiple source files
- **THEN** the production declaration-count rule does not report that fixture
- **AND** the source-file index continues to expose its ambiguity semantics for its dedicated tests
