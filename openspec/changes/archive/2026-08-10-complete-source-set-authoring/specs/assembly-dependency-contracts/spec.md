## ADDED Requirements

### Requirement: Assembly dependency supports source-set expansion
The system SHALL apply the source-set expansion model to directional assembly dependency contracts
while preserving forbidden-reference validation and direct-only dependency semantics for every
resolved source.

#### Scenario: A forbidden direct reference is attributed to its expanded source
- **WHEN** one expanded assembly dependency source directly references a forbidden assembly
- **THEN** exactly one finding identifies that resolved source and derived contract instance
