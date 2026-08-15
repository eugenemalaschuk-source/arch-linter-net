## ADDED Requirements

### Requirement: Layout conventions evaluate source declaration-count expectations

The layout-convention policy schema SHALL accept `max_declarations_per_type` as an optional positive
integer expectation. It SHALL reject a contract that sets it to zero or a negative value, and it
SHALL evaluate the expectation from the source declaration inventory rather than from the
single-source-file facts used by file-name checks.

#### Scenario: Non-positive declaration maximum is rejected

- **WHEN** a policy declares `max_declarations_per_type: 0`
- **THEN** policy validation fails with an actionable diagnostic identifying that field

#### Scenario: Declaration-count diagnostic has deterministic paths

- **WHEN** a selected type exceeds `max_declarations_per_type`
- **THEN** its layout-convention diagnostic lists its declaration paths in stable ordinal order
