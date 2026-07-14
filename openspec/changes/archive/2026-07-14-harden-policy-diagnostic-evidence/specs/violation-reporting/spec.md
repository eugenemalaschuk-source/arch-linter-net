## ADDED Requirements

### Requirement: Policy-exception SARIF contains complete declaration evidence

The CLI SHALL emit every available primary and related policy definition in the
SARIF result's `relatedLocations` when a typed policy exception has source
locations. Each related location SHALL contain a portable policy source URI,
the authored source region, and a message identifying the policy role and YAML
path. The locations SHALL follow composition encounter order; for a conflict
this preserves original-then-conflicting order.

#### Scenario: Root definition conflicts with an imported fragment

- **WHEN** a typed policy composition conflict identifies a root declaration
  and a conflicting imported-fragment declaration
- **THEN** the SARIF result `locations` identifies the primary declaration
- **AND THEN** `relatedLocations` contains the root declaration followed by the
  fragment declaration
- **AND THEN** each related-location message identifies its authored YAML path
  and each physical location contains a portable policy source URI
