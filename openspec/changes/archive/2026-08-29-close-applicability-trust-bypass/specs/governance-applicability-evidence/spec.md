## MODIFIED Requirements

### Requirement: Unassessable state preserves stable reason and provenance
A valid `unassessable` record or an unassessable joined collection-integrity
outcome SHALL contain one or more deterministic reason classes and canonical
provenance sufficient to identify the insufficient evidence. Every reason on a
valid `unassessable` produced record SHALL have the same family, canonical
effective-control identity, and policy identity as that record's canonical
provenance. A record with no reason or with mismatched reason provenance is
invalid contract-integrity evidence and SHALL be represented by canonical
invalid-record-integrity evidence rather than exposing the untrusted reason.
Supported reason classes SHALL distinguish, where meaningful to the family,
missing or unavailable required input, unexpected empty input, unmapped
subject, ambiguous subject, stale declaration, malformed or failed external
input, wrong external evidence identity, repository, revision, or scope,
`missing_applicability_record`, duplicate applicability-record identity,
`unknown_applicability_record_identity`, and incompatible applicability-record
identity or state.

Families MAY define additional bounded reason classes but SHALL NOT use display
text as the machine-readable reason.

#### Scenario: Stale and missing evidence stay distinguishable
- **WHEN** one control has a declared selector with no current match and another
  has no produced applicability record
- **THEN** their results expose distinct stable reason classes and provenance
  rather than a shared zero-result status

#### Scenario: Orphan and missing records stay distinguishable
- **WHEN** one expected control has no record and a different produced record
  has no expected identity
- **THEN** the joined collection exposes `missing_applicability_record` and
  `unknown_applicability_record_identity` as distinct integrity reasons

#### Scenario: Unassessable reason with foreign provenance is rejected
- **WHEN** an `unassessable` record for control A carries a reason whose family,
  control, or policy provenance belongs to a different control
- **THEN** the record is invalid contract-integrity evidence, the assessment is
  unassessable, and output contains only the canonical invalid-record-integrity
  provenance rather than the foreign reason provenance
