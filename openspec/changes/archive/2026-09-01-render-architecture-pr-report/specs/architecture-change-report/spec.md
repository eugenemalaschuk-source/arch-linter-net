## ADDED Requirements

### Requirement: Change report retains resolved findings for downstream projections
The deterministic architecture-change report SHALL retain the stable normalized findings present in the base snapshot but absent from the current snapshot as `resolved_findings`, separately from added, existing, and baseline-debt findings. The report SHALL preserve the same compatible-mode and condition-set validation, ordering, and complete-snapshot-only comparison rules as its other delta sections.

#### Scenario: Resolved finding is retained without a second comparison
- **WHEN** a finding exists in a compatible base snapshot and is absent from the compatible current snapshot
- **THEN** the canonical change report contains that finding once in its ordered resolved-findings section
- **AND** downstream consumers can disclose the resolution without reopening or recomparing either snapshot

#### Scenario: Existing and new findings remain distinct from resolutions
- **WHEN** a compatible current snapshot contains one base-known finding and one finding absent from the base while another base finding is absent from current
- **THEN** the report retains the three findings in existing, new, and resolved sections respectively
- **AND** no finding is counted in more than one section
