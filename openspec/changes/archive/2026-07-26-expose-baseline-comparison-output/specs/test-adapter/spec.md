## ADDED Requirements

### Requirement: Test adapter exposes baseline comparison outcomes

`ArchitectureValidationBuilder` SHALL provide typed operations for baseline
`diff`, `verify`, and `migrate` comparisons. Each operation SHALL use the same
Core comparison semantics as the corresponding CLI command and return public
comparison entries with structured identity and status suitable for assertions.

#### Scenario: Tests assert a diff status
- **WHEN** a test runs a baseline diff through the Testing adapter against a new
  current finding
- **THEN** the returned comparison outcome contains that entry with status `new`
  and its canonical identity

#### Scenario: Tests assert a verify status
- **WHEN** a test runs baseline verification through the Testing adapter against a
  stale or ambiguous baseline entry
- **THEN** the returned outcome exposes the corresponding structured status and
  the verification gate result

#### Scenario: Tests assert migration status
- **WHEN** a test runs a dry-run baseline migration through the Testing adapter
- **THEN** the returned outcome exposes matched, stale, and ambiguous migration
  entries without writing a baseline file
