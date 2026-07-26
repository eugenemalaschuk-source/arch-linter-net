## ADDED Requirements

### Requirement: The coverage inventory carries the resolved source-set expansion
The shared coverage inventory SHALL expose the policy document's resolved source-set expansion alongside expanded layer templates, so coverage consumers can prove which sources an authored contract resolved to without re-running expansion.

#### Scenario: Coverage proves the resolved expansion inventory
- **WHEN** the coverage inventory is built for a policy whose contracts reference named source sets
- **THEN** it exposes each authored contract's resolved sources and each set's resolved members in deterministic order
