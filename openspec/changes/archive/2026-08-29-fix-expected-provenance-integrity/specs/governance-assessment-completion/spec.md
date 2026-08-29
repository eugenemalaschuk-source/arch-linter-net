## MODIFIED Requirements

### Requirement: Unassessability has canonical reasons and provenance
Every unassessable completion SHALL retain deterministic reason codes and
canonical control/family/policy provenance. Reason codes SHALL distinguish at
least missing required input, unexpected empty input, unmapped subject,
ambiguous subject, stale declaration, malformed external input, wrong external
repository, revision, or scope, and missing or invalid applicability-record or
expected-entry integrity. An integrity reason for malformed expected provenance
SHALL use the expected entry's canonical control and family rather than the
untrusted mismatched values. Display text, local paths, timestamps, and finding
counts SHALL NOT be the machine-readable reason or identity.

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
