# Allow Only Contracts Specification

## Purpose
Restricts a source layer to only reference an explicitly allowed set of layers, flagging any other namespace reference as a violation.

## Requirements

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


### Requirement: Allow-only contract accepts optional id
An allow-only contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** an allow-only contract with `id: allowed-refs` produces a violation
- **THEN** the violation SHALL have `ContractId == "allowed-refs"`

#### Scenario: Violation without explicit ID
- **WHEN** an allow-only contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`
