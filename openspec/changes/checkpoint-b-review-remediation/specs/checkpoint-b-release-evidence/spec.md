## ADDED Requirements

### Requirement: Checkpoint B evidence has executable, duplicate-free scenario outcomes
Every Checkpoint B scenario record SHALL be returned by the oracle that executed
the scenario. The aggregator SHALL reject a platform record with a duplicate,
missing, or unexpected scenario ID before authorization.

#### Scenario: A scenario is duplicated
- **WHEN** a platform evidence record contains two entries with the same scenario ID
- **THEN** aggregation fails and no release authorization is emitted
