# Spec Delta

## ADDED Requirements

### Requirement: Baseline merge work scales with input size
The system SHALL resolve contracts and merge finding ignores with expected work proportional to the contracts, baseline entries, and inline-plus-loaded finding ignores in the processed groups (O(C+B+I)), excluding identity field hashing and required output construction.

#### Scenario: Independently scaled merge inputs retain bounded work and semantics
- **WHEN** C, B, and I are independently varied across at least three sizes for version-1, version-2, and version-3 baseline merges
- **THEN** deterministic contract lookup and ignore membership work grows proportionally to the relevant inputs and the resulting ordered ignores retain the existing merge semantics