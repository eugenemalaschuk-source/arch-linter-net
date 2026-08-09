## ADDED Requirements

### Requirement: Explain JSON errors are authoritative

When `explain --format json` terminates on an owned configuration, policy, or build-state failure, it SHALL retain its existing single structured JSON document on stdout. Its exit code and human-format output behavior SHALL remain unchanged.

#### Scenario: Explain policy failure is parseable JSON
- **WHEN** `explain --format json` loads an invalid policy
- **THEN** stdout parses as one JSON error document
