## ADDED Requirements

### Requirement: Policy-check JSON errors are authoritative

When `policy check --format json` terminates on an owned command, policy, or configuration failure, it SHALL retain its existing single structured JSON document on stdout with policy diagnostic details where available. Its exit code and human-format stderr output SHALL remain unchanged.

#### Scenario: Invalid policy is parseable JSON
- **WHEN** `policy check --format json` loads an invalid policy
- **THEN** stdout parses as one JSON error document containing the structured policy failure
