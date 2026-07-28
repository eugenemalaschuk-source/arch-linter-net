## ADDED Requirements

### Requirement: SARIF policy-check projection
SARIF output for policy check SHALL project typed configuration failures and deferred checks with policy provenance and command status, without claiming that architecture validation passed.

#### Scenario: SARIF contains deferred check
- **WHEN** a valid policy has fact-dependent checks deferred
- **THEN** the SARIF log identifies the deferred state and preserves the policy location where available
