## ADDED Requirements

### Requirement: Method body contract accepts optional id
A method body contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** a method body contract with `id: no-reflection` produces a violation
- **THEN** the violation SHALL have `ContractId == "no-reflection"`

#### Scenario: Violation without explicit ID
- **WHEN** a method body contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`
