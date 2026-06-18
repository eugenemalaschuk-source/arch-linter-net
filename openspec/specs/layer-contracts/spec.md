## ADDED Requirements

### Requirement: Evaluate inward-only layer ordering
The system SHALL verify that for each layer in the contract's layer list, types in that layer do not reference types in any layer listed earlier (inward direction).

#### Scenario: No layer violations
- **WHEN** layers are ordered `[A, B, C]` and types in B never reference types in A, and types in C never reference types in A or B
- **THEN** the contract returns an empty violation list

#### Scenario: Layer ordering violation
- **WHEN** layers are ordered `[A, B, C]` and a type in layer B references a type in layer A
- **THEN** a violation is returned indicating the inward reference

#### Scenario: Self-layer references allowed
- **WHEN** a type in layer B references another type in layer B
- **THEN** no violation is reported for that reference
