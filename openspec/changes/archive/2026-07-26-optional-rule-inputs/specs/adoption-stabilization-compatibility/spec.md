## ADDED Requirements

### Requirement: Planned-empty rule inputs are implemented as schema-backed coverage state
The implementation SHALL provide the planned-empty rule-input lifecycle required by this capability's compatibility contract: exact input identity, mandatory reason, provenance, typed output, automatic covered transition, and fail-closed stale or unknown identities.

#### Scenario: Compatibility lifecycle is preserved
- **WHEN** a policy moves from a planned-empty input to matching code
- **THEN** the same declaration produces optional-empty before the code exists and covered state after it exists
