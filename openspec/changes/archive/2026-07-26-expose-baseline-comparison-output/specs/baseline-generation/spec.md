## ADDED Requirements

### Requirement: Baseline comparison results are available to machine consumers

The system SHALL project the canonical comparison entries from `baseline diff`,
`baseline verify`, and `baseline migrate` without reparsing display messages. Each
projection SHALL preserve an entry's structured identity when present and one of
the lifecycle statuses `new`, `matched`, `stale`, or `ambiguous`.

#### Scenario: Comparison result preserves exact identity
- **WHEN** a version 2 baseline comparison identifies a current or baseline entry
- **THEN** every machine-readable projection exposes the same canonical identity
  fields used for matching, rather than a display-text-derived key

#### Scenario: Comparison result preserves status
- **WHEN** a diff, verify, or migrate command classifies entries as new, matched,
  stale, or ambiguous
- **THEN** every machine-readable projection exposes the classification as a
  structured status value
