## ADDED Requirements

### Requirement: Validation JSON errors are authoritative

When a `validate` invocation has selected `--format json`, every owned configuration, policy, report-routing, or build-state-preflight termination path SHALL retain its existing single structured JSON document on stdout. The command SHALL retain its established exit code, and human-format behavior SHALL remain unchanged.

#### Scenario: Validation build-state failure is parseable JSON
- **WHEN** `validate --format json` is blocked by an owned build-state preflight failure
- **THEN** stdout parses as one JSON error document and the command retains its existing runtime-error exit code
