## MODIFIED Requirements

### Requirement: Unassessability has canonical reasons and provenance
Every unassessable completion SHALL retain deterministic reason codes and
canonical control/family/policy provenance. A reason originating from an
unassessable produced record SHALL be accepted only when its family, control,
and policy provenance exactly match that record's canonical provenance;
mismatched provenance SHALL be replaced by canonical invalid-record-integrity
evidence. Reason codes SHALL distinguish at least missing required input,
unexpected empty input, unmapped subject, ambiguous subject, stale declaration,
malformed external input, wrong external repository, revision, or scope, and
missing or invalid applicability-record or expected-entry integrity. An
integrity reason for malformed expected provenance SHALL use the expected
entry's canonical control and family rather than the untrusted mismatched
values. Display text, local paths, timestamps, and finding counts SHALL NOT be
the machine-readable reason or identity.

#### Scenario: Multiple insufficient inputs remain explainable
- **WHEN** one required control has a stale declaration and another is missing
  a required external artifact
- **THEN** both deterministic reason/provenance records remain available in
  stable order and neither is flattened into a generic zero-findings result

#### Scenario: Malformed expected provenance is canonicalized for reporting
- **WHEN** an expected entry has a provenance control or family that differs
  from its own canonical identity
- **THEN** its invalid-expected-integrity reason identifies the canonical
  expected control and family and the assessment is `unassessable`

#### Scenario: Foreign unassessable reason provenance is not reported
- **WHEN** a produced unassessable record carries a reason for a different
  family, control, or policy
- **THEN** completion reports canonical invalid-record-integrity evidence and
  does not include the foreign provenance in machine-readable or human output

### Requirement: Assessment completion fails closed across requested controls
For an authoritative assessment, any requested control with an unassessable
applicability or completeness outcome, or any expected identity without exactly
one compatible produced record, SHALL make the overall completion state
`unassessable`. `optional` and `not_applicable` controls SHALL remain visible
but SHALL not inflate the required denominator; a deliberately absent optional
input SHALL be represented by an explicit compatible `not_applicable` record,
not by omitting its record. Completion aggregation SHALL not infer evaluability
from a zero-finding count, a configured-control count, or a missing record.
It SHALL derive applicability completion exclusively from the canonical
expected-membership and produced-record collections at the assessment trust
boundary; an executor- or family-supplied precomputed completion value SHALL
NOT make an assessment pass, fail, or become unassessable.

#### Scenario: Missing applicability record cannot become a pass
- **WHEN** the expected membership contains any control whose applicability
  record is missing
- **THEN** the control exposes `missing_applicability_record` evidence and the
  authoritative assessment is `unassessable`

#### Scenario: Precomputed completion cannot replace canonical evidence
- **WHEN** a family supplies no expected-membership or produced-record entries
  together with a precomputed passing completion value
- **THEN** the authoritative assessment has no applicability completion from
  that input and the precomputed value cannot alter the ordinary conformance
  result
