## ADDED Requirements

### Requirement: Asmdef contract accepts optional id
An asmdef contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** an asmdef contract with `id: unity-rules` produces a violation
- **THEN** the violation SHALL have `ContractId == "unity-rules"`

#### Scenario: Violation without explicit ID
- **WHEN** an asmdef contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`
