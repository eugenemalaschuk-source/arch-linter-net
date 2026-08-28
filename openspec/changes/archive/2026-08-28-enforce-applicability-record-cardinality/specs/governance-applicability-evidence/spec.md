## MODIFIED Requirements

### Requirement: Effective controls have explicit applicability membership
The effective-policy/control projection SHALL materialize one canonical expected
applicability-membership entry for every effective v0.8 control whose family
has applicability semantics. An entry SHALL contain the canonical
effective-control identity, family discriminator, membership (`required`,
`optional`, or `not_applicable`), and canonical policy/family provenance that
established the membership. The expected membership collection SHALL be
complete and independent from produced applicability records.

Every produced applicability record SHALL reference exactly one expected entry
by canonical effective-control identity. Membership belongs to the expected
entry; a record SHALL NOT define a competing membership value. For every
expected entry, the produced-record collection SHALL contain zero or exactly
one record with that identity; duplicate records are invalid
contract-integrity evidence. The expected membership collection SHALL remain
available even when evaluation fails to produce a record. The collection is an
applicability authority, not a second effective-policy inventory or counting
engine; #685 may consume the same canonical control identity but does not
derive membership or a denominator.

One effective control has one expected entry regardless of source-set
expansion, match/finding count, display name, YAML position, or runtime
enumeration. Controls outside the required set SHALL remain explicitly visible
as `optional` or `not_applicable`; they SHALL NOT be silently omitted.

#### Scenario: Required control is counted exactly once
- **WHEN** one effective control is expanded across multiple source sets or
  yields multiple findings
- **THEN** the expected membership collection has exactly one entry for its
  canonical control identity and contributes exactly one member when its
  membership is `required`

#### Scenario: Required record is missing
- **WHEN** expected applicability controls contain required controls A and B,
  produced records contain a record only for A, and B's record is absent
- **THEN** a consumer left joins the expected collection to records, retains A
  and B in the required denominator, and represents B as `unassessable` with a
  `missing_applicability_record` reason rather than inferring membership from
  family semantics or omitting B

#### Scenario: Duplicate record is unassessable
- **WHEN** two produced applicability records reference the same expected
  required control identity
- **THEN** consumers preserve the control once in the required denominator,
  do not count either duplicate as evaluable, and represent the control as
  unassessable contract-integrity evidence with duplicate-record provenance

#### Scenario: Optional control is visible without inflating the denominator
- **WHEN** explicit policy semantics make configured control C optional
- **THEN** C has an explicit expected `optional` membership entry, remains
  separately disclosed whether its record is evaluable, not applicable, or
  unassessable, and does not inflate the required denominator
