## ADDED Requirements

### Requirement: Public API JSON errors are authoritative

When a public-API subcommand that accepts `--format json` terminates on an owned configuration, policy, snapshot, or build-state failure, it SHALL write exactly one versioned JSON error document to stdout with a stable error category and typed details where available. Its exit code and human-format stderr output SHALL remain unchanged.

#### Scenario: Public API preflight failure is parseable JSON
- **WHEN** a public-API subcommand runs with `--format json` and is blocked by an owned build-state preflight failure
- **THEN** stdout parses as one JSON error document and no human fallback text is emitted there
