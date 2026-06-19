## ADDED Requirements

### Requirement: Independence contract accepts optional id
An independence contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** an independence contract with `id: no-cross-talk` produces a violation
- **THEN** the violation SHALL have `ContractId == "no-cross-talk"`

#### Scenario: Violation without explicit ID
- **WHEN** an independence contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`
