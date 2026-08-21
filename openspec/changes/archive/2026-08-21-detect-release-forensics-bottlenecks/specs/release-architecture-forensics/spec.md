## ADDED Requirements

### Requirement: Deterministic bottleneck analysis projection
The system SHALL project finalized canonical file, TaskKey, author, and `G0` evidence into category-local parallel-development bottleneck findings. Each finding SHALL expose canonical TaskKey pairs and ordered source provenance, pair-exclusive exact epoch-second intervals and gaps, canonical authors, raw and normalized components, effective weights, logical-file aliases, and the pathname-reuse limitation. The projection SHALL consume neither source spellings nor `Gtheta` topology and SHALL describe pressure rather than an actual merge conflict.

#### Scenario: Pair-exclusive independence
- **WHEN** a file has one canonical file-evidence commit containing `issue#101` but not `issue#102` and another containing `issue#102` but not `issue#101`
- **THEN** both TaskKeys participate in one independent pair and their pair-exclusive intervals determine temporal proximity

#### Scenario: Shared commit does not establish a pair
- **WHEN** the only file-evidence commit contains canonical `issue#101` and `issue#102`
- **THEN** the finding has ordinary task breadth but zero independent-task spread and zero temporal proximity

#### Scenario: G0-only centrality
- **WHEN** the significance threshold changes while raw `G0` edges are unchanged
- **THEN** distinct-neighbor degree, centrality, and bottleneck score remain identical

#### Scenario: Exact temporal evidence
- **WHEN** pair-exclusive intervals have a 90,000-second positive gap, including epochs outside host date ranges or equal epochs with different timezone tokens
- **THEN** the gap has `days_between=2`, temporal proximity `0.333333333`, and endpoints are selected solely from epoch integers
