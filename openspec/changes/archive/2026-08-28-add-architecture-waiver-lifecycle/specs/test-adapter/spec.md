## ADDED Requirements

### Requirement: Testing exposes canonical waiver lifecycle evidence
The Testing adapter SHALL expose typed canonical waiver lifecycle records and
policy-hygiene outcomes from validation, including the evaluation date and
policy provenance, without requiring callers to parse human, JSON, or YAML
output. It SHALL allow tests to supply an explicit date for deterministic
lifecycle evaluation.

#### Scenario: Test asserts deterministic stale state
- **WHEN** a test validates a policy with a structured waiver that no longer
  matches and supplies an evaluation date
- **THEN** it can assert the waiver's ID, `stale` state, target, provenance,
  and failed strict policy-hygiene outcome through typed results
