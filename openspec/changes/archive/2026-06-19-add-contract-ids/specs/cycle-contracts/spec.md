## ADDED Requirements

### Requirement: Cycle contract accepts optional id
A cycle contract SHALL accept an optional `id` field. When provided, cycle results from this contract SHALL include the contract ID.

#### Scenario: Cycle result includes contract ID
- **WHEN** a cycle contract with `id: no-cycles` detects a cycle
- **THEN** the cycle result SHALL have `ContractId == "no-cycles"`

#### Scenario: Cycle result without explicit ID
- **WHEN** a cycle contract without explicit `id` detects a cycle
- **THEN** the cycle result SHALL have `ContractId` set to the fallback ID derived from `name`
