## ADDED Requirements

### Requirement: Baseline JSON errors are authoritative

When a baseline subcommand that accepts `--format json` terminates on an owned configuration, policy, or build-state failure, it SHALL write exactly one versioned JSON error document to stdout with a stable error category and typed details where available. Its exit code and human-format stderr output SHALL remain unchanged.

#### Scenario: Verify configuration failure is parseable JSON
- **WHEN** `baseline verify --format json` encounters an owned configuration failure
- **THEN** stdout parses as one JSON error document rather than human-readable configuration text
