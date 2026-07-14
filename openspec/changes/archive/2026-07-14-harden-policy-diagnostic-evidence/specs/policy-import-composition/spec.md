## ADDED Requirements

### Requirement: Consistency provenance selects identified contracts

The policy provenance index SHALL select only contract declarations whose IDs
participate in a policy-consistency diagnostic when that diagnostic supplies a
primary contract ID or one or more conflicting contract IDs. It SHALL NOT
select a contract solely because its display name matches. Name-based selection
MAY be used only when the diagnostic supplies no participating contract IDs.

#### Scenario: Same display name appears in a different contract family

- **WHEN** a consistency conflict identifies two dependency contracts by ID
- **AND WHEN** an unrelated contract in another family has the same display
  name as one of those contracts
- **THEN** the diagnostic's primary and related policy locations identify only
  the two participating dependency contracts
