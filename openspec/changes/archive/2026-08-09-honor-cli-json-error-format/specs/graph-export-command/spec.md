## ADDED Requirements

### Requirement: Graph JSON errors are authoritative

When `graph --format json` terminates on an owned configuration, policy, or build-state failure, it SHALL retain its existing single structured JSON document on stdout. Its exit code and non-JSON output behavior SHALL remain unchanged.

#### Scenario: Graph build-state failure is parseable JSON
- **WHEN** `graph --format json` encounters an owned build-state failure
- **THEN** stdout parses as one JSON error document
