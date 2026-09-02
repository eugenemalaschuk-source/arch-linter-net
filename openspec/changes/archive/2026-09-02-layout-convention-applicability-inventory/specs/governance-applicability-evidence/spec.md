## ADDED Requirements

### Requirement: Layout convention inventories contribute canonical applicability controls

The applicability model SHALL accept layout-convention inventory expected entries and records as
family-native evidence. Each inventory control SHALL contribute one stable expected membership
entry and at most one record, with the inventory's policy identity and linked convention identity
as provenance. Its evidence SHALL retain folder/native subject information without contributing
to a cross-family quality score or causing policy controls to be recounted.

#### Scenario: Layout inventory is complete
- **WHEN** a configured layout inventory has present expected folders, complete exhaustive
  mapping where requested, and nonempty linked selector matches
- **THEN** the canonical applicability projection SHALL mark its control evaluable

#### Scenario: Layout inventory is incomplete
- **WHEN** a configured layout inventory reports stale, unexpected-empty, unmapped, or ambiguous
  evidence
- **THEN** the canonical applicability projection SHALL mark its control unassessable with the
  corresponding stable reason code and provenance

#### Scenario: Policy has no layout inventory
- **WHEN** a policy contains no layout convention applicability inventory
- **THEN** it SHALL contribute no layout-inventory applicability expected entry or record
