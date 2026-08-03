## ADDED Requirements

### Requirement: Every selected analysis unit has an eligibility outcome
The system SHALL attach exactly one cache eligibility outcome to every selected analysis unit, including missing, stale, wrong-context, unverifiable, cancelled, and preparation-failed outcomes. `Platform` and runtime identifier SHALL participate in receipt and eligibility context.

#### Scenario: Preflight blocks a project
- **WHEN** preflight returns any state other than current
- **THEN** the diagnostic contains `cache-ineligible` and stable reason codes
