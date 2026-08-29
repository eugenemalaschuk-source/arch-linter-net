## ADDED Requirements

### Requirement: Collection integrity preserves every expected and duplicate provenance
An applicability collection SHALL have exactly one compatible produced record
for every canonical expected identity, regardless of whether its membership is
`required`, `optional`, or `not_applicable`. A missing record SHALL produce
`missing_applicability_record` integrity evidence and SHALL prevent the
collection from being reported as complete; an intentionally absent optional
input SHALL instead produce an explicit compatible `not_applicable` record.

For duplicate expected identities, consumers SHALL select the displayed
representative by a deterministic ordering of membership, family, and complete
provenance, and SHALL retain a duplicate-identity reason for every participating
expected provenance. For duplicate produced identities, consumers SHALL retain
a duplicate-identity reason for every participating produced provenance in
deterministic order. Enumeration order SHALL NOT affect the representative,
required denominator, reason list, or completion state.

#### Scenario: Missing optional record is integrity evidence
- **WHEN** an expected `optional` control has no produced record
- **THEN** the joined projection exposes `missing_applicability_record`, the
  required denominator remains unchanged, and the collection is not complete

#### Scenario: Duplicate records retain producer provenance
- **WHEN** two produced records for one expected identity have distinct policy
  provenance
- **THEN** collection integrity exposes one deterministic duplicate-record
  reason for each producer provenance

#### Scenario: Reordered duplicate expectations are equivalent
- **WHEN** the same conflicting duplicate expected entries are supplied in
  opposite enumeration orders
- **THEN** the displayed representative, required denominator, reasons, and
  completion state are identical
