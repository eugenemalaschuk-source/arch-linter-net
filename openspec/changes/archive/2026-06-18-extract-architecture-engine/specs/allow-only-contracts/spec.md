## ADDED Requirements

### Requirement: Evaluate allow-only contracts
The system SHALL verify that source layer types reference only types in explicitly allowed layers (plus their own layer).

#### Scenario: All references allowed
- **WHEN** source type references types only in allowed layers and its own layer
- **THEN** the contract returns an empty violation list

#### Scenario: Reference outside allowed layers
- **WHEN** source type references a type in a layer not in the `allowed` list
- **THEN** a violation is returned with `ForbiddenNamespace = "outside allowed layers"`

#### Scenario: Allowed types exempted
- **WHEN** a reference matches an entry in `allowed_types`
- **THEN** that reference is excluded from violations

### Requirement: Allow-only ignores non-project types
The system SHALL exclude references to types that are not in any defined layer namespace from allow-only violation checks.

#### Scenario: External type reference
- **WHEN** source type references `System.String` (not in any layer)
- **THEN** no violation is reported for that reference
