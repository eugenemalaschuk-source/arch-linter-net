## ADDED Requirements

### Requirement: The advertised current cache schema accepts every writer payload
The schema advertised as the current write contract for `analysis-cache/v1`
SHALL accept every payload emitted by the cache writer, including metric-budget
payloads. A frozen historical cache schema SHALL remain byte-stable and be
advertised only as a legacy read contract when it cannot represent a current
payload.

#### Scenario: A cached metric-budget violation validates against the current schema
- **WHEN** a validation run stores a metric-budget violation in the persistent
  cache
- **THEN** the stored entry validates against the current advertised cache
  schema and its explicit payload discriminator
