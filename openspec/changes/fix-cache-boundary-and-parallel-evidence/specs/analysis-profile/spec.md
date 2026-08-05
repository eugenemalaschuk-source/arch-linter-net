## ADDED Requirements

### Requirement: Final release evidence is an attributable pre/post comparison
The final post-optimization evidence SHALL retain both strict and audit profiles for paired runs, raw wall-clock/allocation/resource samples, median and p95 summaries, exact source commit and package identity, explicit build configuration, and a #374 baseline-to-post-to-delta table. It SHALL compare every cached sample with its uncached canonical baseline and every parallel sample with its sequential counterpart, including finding order, completion, exit/publication state, and deterministic counters.

#### Scenario: Release documentation consumes the comparison
- **WHEN** release documentation reads the checked-in final report
- **THEN** it can inspect raw profiles, distributions, identities, configuration, and baseline/post/delta without recalculating the dataset
