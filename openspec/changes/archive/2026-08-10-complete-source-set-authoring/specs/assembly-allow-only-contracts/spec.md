## ADDED Requirements

### Requirement: Assembly allow-only supports source-set expansion
The system SHALL apply the source-set expansion model to directional assembly allow-only contracts
while preserving declared-target filtering and direct-only dependency semantics for every resolved
source.

#### Scenario: A disallowed direct reference is attributed to its expanded source
- **WHEN** one expanded assembly allow-only source directly references a declared assembly outside
  its allowed list
- **THEN** exactly one finding identifies that resolved source and derived contract instance
