## ADDED Requirements

### Requirement: Core measurement evidence distinguishes unavailable from empty
The public Core measurement and metric-evidence models SHALL expose contributor
identities and their count only when a measurement is evaluable. For an
unassessable result, value, contributors, and contributor count SHALL all be
unavailable rather than representing an unknown contributor universe as zero
or an empty collection.

#### Scenario: Unassessable Core measurement does not imply a zero count
- **WHEN** a required metric input is incomplete and the Core evaluator returns
  an unassessable measurement
- **THEN** its value, contributors, and contributor count are unavailable to a
  direct Core consumer
