## ADDED Requirements

### Requirement: Persisted change reports carry compatible execution context
The versioned machine-readable architecture-change report SHALL retain the
mode and condition-set scope validated from its input snapshots and a
non-empty execution identifier supplied by the report workflow.  Consumers
SHALL reject a report whose context is absent, malformed, or unsupported.

#### Scenario: Change report retains report workflow identity
- **WHEN** a workflow compares compatible strict snapshots with an execution
  identifier
- **THEN** its JSON report retains that identifier, strict mode, and
  condition-set scope alongside the ordered delta sections

#### Scenario: Context-less report is unsupported
- **WHEN** a consumer reads a persisted architecture-change report without
  required execution context
- **THEN** it rejects the report rather than treating it as compatible with a
  Health artifact
