## ADDED Requirements

### Requirement: Public schema supports semantic coverage contracts
The published JSON Schema SHALL accept `scope: semantic_role`, semantic exclusion `role` and `metadata` fields, and the documented coverage contract-level fields.

#### Scenario: Schema-aware authoring accepts semantic coverage
- **WHEN** a policy author validates a semantic-role coverage contract against the published JSON Schema
- **THEN** the contract and its reasoned semantic exclusion are accepted
