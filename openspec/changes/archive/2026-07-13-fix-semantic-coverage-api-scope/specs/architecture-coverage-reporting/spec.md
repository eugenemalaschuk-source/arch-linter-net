## ADDED Requirements

### Requirement: Semantic diagnostics respect coverage roots
When a semantic-role coverage contract declares roots, classification conflict and metadata-failure evidence and findings SHALL include only subjects within those roots.

#### Scenario: Out-of-scope conflict does not fail a rooted contract
- **WHEN** a classification conflict exists outside a semantic contract's declared roots
- **THEN** that contract's summary and findings omit the conflict

### Requirement: Exclusion evidence preserves positional record compatibility
The exclusion summary item SHALL retain its positional `Item` and `Reason` API, including `init` properties and `Deconstruct`, while adding optional evidence.

#### Scenario: Existing two-value deconstruction remains valid
- **WHEN** a consumer deconstructs a legacy exclusion summary item into item and reason
- **THEN** it continues to compile and run
