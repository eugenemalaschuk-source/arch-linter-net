## ADDED Requirements

### Requirement: Source analysis retains partial type declaration evidence

The system SHALL retain a source declaration inventory for every analyzed CLR type, including its
assembly, full type name, type kind, whether each declaration uses the C# `partial` modifier, and
the normalized source-file path. The inventory SHALL retain all declarations even when the existing
unique-source-file fact is ambiguous.

#### Scenario: A type has declarations in two source files

- **WHEN** source analysis encounters two declarations of the same CLR type in different files
- **THEN** the declaration inventory exposes both normalized paths in stable ordinal order
- **AND** existing unique-source-file consumers continue to receive their ambiguity result

#### Scenario: A non-partial type has one declaration

- **WHEN** source analysis encounters one ordinary type declaration
- **THEN** the inventory exposes one declaration marked as not partial

### Requirement: Layout conventions limit declarations of a type

The system SHALL allow strict and audit layout convention contracts to declare an optional
`max_declarations_per_type` positive integer. For source types selected by the contract, validation
SHALL report one violation per type whose declaration count exceeds that maximum.

#### Scenario: A partial aggregate exceeds the configured limit

- **WHEN** a strict layout convention selects a type declared in three source files and sets
  `max_declarations_per_type: 1`
- **THEN** strict validation fails with a diagnostic naming the type, the expected maximum, the
  observed count, and all declaration paths

#### Scenario: A single-source type satisfies the limit

- **WHEN** a strict layout convention selects a type with one declaration and sets
  `max_declarations_per_type: 1`
- **THEN** no declaration-count violation is reported

#### Scenario: An audit declaration-count violation does not fail strict mode

- **WHEN** only an audit layout convention observes a type above its declaration-count limit
- **THEN** audit output reports the violation
- **AND** strict validation remains successful
