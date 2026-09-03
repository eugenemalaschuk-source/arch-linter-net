## ADDED Requirements

### Requirement: Architecture Health badge accepts only complete canonical evidence
An assessable `badge architecture-health` input SHALL contain the complete
canonical report-evidence envelope: `schema_version` 2,
`kind=architecture-health-report-evidence`, and inner `gate` and `health`
values equal to the top-level canonical Health state. Every validation outcome
used as report evidence SHALL contain a complete supported policy-inventory
receipt; incomplete, unsupported, inventory-less, or inconsistent outcomes
SHALL make the badge unassessable. The command SHALL not silently discard such
outcomes to produce an assessable badge.

#### Scenario: Production-shaped canonical evidence is projected
- **WHEN** the command receives a Health document with a version-2 canonical
  report-evidence envelope whose inner state and complete inventory receipts
  agree with the top-level state
- **THEN** it projects the canonical Health, ignore debt, and effective rule
  count into the deterministic ready badge

#### Scenario: Unsupported evidence envelope is rejected
- **WHEN** the report-evidence schema version or kind is absent or unsupported
- **THEN** the command emits the explicit unassessable badge
- **AND** it exits 2 without inventing a ready state

#### Scenario: Inner state disagreement is rejected
- **WHEN** the report-evidence gate or Health differs from the top-level
  canonical state
- **THEN** the command emits the explicit unassessable badge
- **AND** it does not project the inventory counters

#### Scenario: Inventory-less outcome is rejected
- **WHEN** a validation outcome lacks a complete supported policy-inventory
  receipt
- **THEN** the command emits the explicit unassessable badge
- **AND** it does not ignore that outcome to produce a colored badge

