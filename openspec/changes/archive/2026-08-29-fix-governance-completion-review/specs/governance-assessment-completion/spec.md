## MODIFIED Requirements

### Requirement: Assessment completion fails closed across requested controls
For an authoritative assessment, any requested control with an unassessable
applicability or completeness outcome, or any expected identity without exactly
one compatible produced record, SHALL make the overall completion state
`unassessable`. `optional` and `not_applicable` controls SHALL remain visible
but SHALL not inflate the required denominator; a deliberately absent optional
input SHALL be represented by an explicit compatible `not_applicable` record,
not by omitting its record. Completion aggregation SHALL not infer evaluability
from a zero-finding count, a configured-control count, or a missing record.

#### Scenario: Missing applicability record cannot become a pass
- **WHEN** the expected membership contains any control whose applicability
  record is missing
- **THEN** the control exposes `missing_applicability_record` evidence and the
  authoritative assessment is `unassessable`
