## ADDED Requirements

### Requirement: Testing API exposes normalized findings
The Testing API SHALL expose normalized findings and baseline lifecycle status directly so test assertions do not parse formatted output.

#### Scenario: Baseline lifecycle assertion
- **WHEN** baseline verification reports a stale entry through the Testing API
- **THEN** the returned normalized finding exposes `baseline_state` of `stale` and its canonical identity
