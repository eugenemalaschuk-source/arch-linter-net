## ADDED Requirements

### Requirement: Artifact planning is metadata-only and complete
The system SHALL separate output/reference selection from CLR loading and SHALL compute a complete metadata-only reference closure for cache authorization. If the closure cannot be proven complete, it SHALL mark cache reuse ineligible.

#### Scenario: Unsupported closure input fails closed
- **WHEN** planning cannot resolve a selected artifact or its reference closure metadata
- **THEN** cache reuse is rejected before a cache outcome is accepted
