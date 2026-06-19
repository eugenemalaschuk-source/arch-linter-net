## ADDED Requirements

### Requirement: Allow-only contract accepts optional id
An allow-only contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** an allow-only contract with `id: allowed-refs` produces a violation
- **THEN** the violation SHALL have `ContractId == "allowed-refs"`

#### Scenario: Violation without explicit ID
- **WHEN** an allow-only contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`
