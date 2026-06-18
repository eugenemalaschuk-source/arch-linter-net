## ADDED Requirements

### Requirement: Evaluate strict dependency contracts
The system SHALL evaluate each strict dependency contract by finding all types in the source layer's namespace and checking that none reference types in any forbidden layer's namespace.

#### Scenario: No violations found
- **WHEN** source layer types do not reference any forbidden layer types
- **THEN** the contract returns an empty violation list

#### Scenario: Violations found
- **WHEN** source layer type `A` references forbidden layer type `B`
- **THEN** the contract returns a violation with `ContractName`, `SourceType = "A"`, `ForbiddenNamespace`, and `ForbiddenReferences` containing `"B"`

#### Scenario: Allowed types exempted
- **WHEN** source type references a forbidden type that is listed in `allowed_types`
- **THEN** that reference is excluded from violations

#### Scenario: Ignored violations exempted
- **WHEN** a source+forbidden pair matches an entry in `ignored_violations`
- **THEN** that violation is excluded from results

### Requirement: Evaluate forbidden legacy runtime layers
The system SHALL check source types against all namespaces in `legacy_runtime_layers` when `forbidden_legacy_runtime` is true.

#### Scenario: Legacy runtime violation
- **WHEN** `forbidden_legacy_runtime` is true and source type references a type in a legacy runtime namespace
- **THEN** a violation is returned for that legacy namespace

### Requirement: Multiple forbidden layers per contract
The system SHALL evaluate each forbidden layer independently and return all violations across all forbidden layers.

#### Scenario: Violations across multiple forbidden layers
- **WHEN** source type references types in both forbidden layer A and forbidden layer B
- **THEN** violations are returned for both forbidden namespaces
