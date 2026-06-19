## ADDED Requirements

### Requirement: Dependency contract accepts optional id
A dependency contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** a dependency contract with `id: my-rule` produces a violation
- **THEN** the violation SHALL have `ContractId == "my-rule"`

#### Scenario: Violation without explicit ID
- **WHEN** a dependency contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`
