# Independence Contracts Specification

## Purpose
Evaluates mutual independence across a set of layers, ignoring self-references, so no layer in the set may reference any other.

## Requirements

### Requirement: Evaluate mutual independence across layers
The system SHALL verify that for each pair of layers in the contract, types in layer A do not reference types in layer B AND types in layer B do not reference types in layer A.

#### Scenario: Independent layers
- **WHEN** layers `[A, B]` have no cross-references in either direction
- **THEN** the contract returns an empty violation list

#### Scenario: One-directional dependency
- **WHEN** types in layer A reference types in layer B (but not vice versa)
- **THEN** a violation is returned for layer A's references to layer B

#### Scenario: Bidirectional dependency
- **WHEN** types in layer A reference types in layer B and types in layer B reference types in layer A
- **THEN** violations are returned for both directions

### Requirement: Independence contracts ignore self-references
The system SHALL not report violations for references within the same layer.

#### Scenario: Intra-layer references
- **WHEN** a type in layer A references another type in layer A
- **THEN** no violation is reported


### Requirement: Independence contract accepts optional id
An independence contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** an independence contract with `id: no-cross-talk` produces a violation
- **THEN** the violation SHALL have `ContractId == "no-cross-talk"`

#### Scenario: Violation without explicit ID
- **WHEN** an independence contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`
