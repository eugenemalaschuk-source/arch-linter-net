## ADDED Requirements

### Requirement: Declared topology retains native completeness evidence
The declared-topology applicability control SHALL retain deterministic native
facts for the configured subject universe, mapped subjects, reviewed
out-of-scope subjects, unmapped subjects, ambiguous subjects, and enabled stale
declarations. It SHALL use the existing
control identity, applicability membership/state, reason codes, and
provenance; it SHALL NOT introduce a topology-only result envelope or quality
percentage.

#### Scenario: Exhaustive topology is complete
- **WHEN** every observed subject in an exhaustive topology is exactly-one mapped or reviewed out of scope
- **THEN** its applicability record can be evaluable with ordered native mapping evidence

#### Scenario: Mapping gap is unassessable
- **WHEN** an exhaustive topology has an unmapped or ambiguous required subject
- **THEN** its record is unassessable with the existing unmapped-subject or ambiguous-subject reason and native drill-down evidence
