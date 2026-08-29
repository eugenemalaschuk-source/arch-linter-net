## ADDED Requirements

### Requirement: Applicability evidence drives authoritative completion
Every v0.8 family that opts into applicability SHALL project its canonical
expected-membership and produced-record integrity result to the shared
governance assessment-completion boundary. A required expected entry that is
missing, duplicate, orphaned, incompatible, unexpectedly empty, stale,
unmapped, ambiguous, or otherwise insufficient SHALL be unassessable rather
than being inferred from a zero finding count. Explicit optional and
not-applicable membership SHALL remain visible without changing the required
denominator.

#### Scenario: Complete records permit trusted conformance
- **WHEN** every required expected applicability entry joins to one compatible
  evaluable record and no configured architecture control fails
- **THEN** the family supplies trusted evaluable evidence that can contribute
  to authoritative assessment `pass`

#### Scenario: Collection-integrity evidence prevents a clean completion
- **WHEN** a required expected entry has no compatible produced record or the
  produced collection contains an orphan identity
- **THEN** the family supplies canonical unassessable completion evidence with
  the collection-integrity reason and does not report a clean empty result
