# Spec Delta

## ADDED Requirements

### Requirement: Baseline merge work scales with input size
The system SHALL resolve contracts and merge finding ignores with expected work proportional to the contracts, baseline entries, and inline-plus-loaded finding ignores in the processed groups (O(C+B+I)), excluding identity field hashing and required output construction.

Contract indexing SHALL be deferred until an ordinary contract lookup is required. For small groups whose baseline entries all share one case-insensitive contract ID, the system MAY resolve that ID once with first-match semantics and reuse the result. Imported external diagnostics SHALL be excluded before contract indexing.

#### Scenario: Independently scaled merge inputs retain bounded work and semantics
- **WHEN** C, B, and I are independently varied across at least three sizes for version-1, version-2, and version-3 baseline merges
- **THEN** deterministic contract lookup and ignore membership work grows proportionally to the relevant inputs and the resulting ordered ignores retain the existing merge semantics

#### Scenario: Sparse and imported-only groups avoid unused contract indexes
- **WHEN** a small baseline group has multiple entries for the same contract ID, or an external group contains only imported external diagnostics
- **THEN** the system preserves first-match and imported-diagnostic semantics without constructing an unused group-wide contract index
