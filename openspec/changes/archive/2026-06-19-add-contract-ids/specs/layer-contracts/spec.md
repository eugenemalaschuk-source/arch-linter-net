## ADDED Requirements

### Requirement: Layer contract accepts optional id
A layer contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** a layer contract with `id: layer-order` produces a violation
- **THEN** the violation SHALL have `ContractId == "layer-order"`

#### Scenario: Violation without explicit ID
- **WHEN** a layer contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`
