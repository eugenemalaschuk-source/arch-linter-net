## ADDED Requirements

### Requirement: Deterministic co-change graph evidence

A successful history ingestion result SHALL retain a deterministic co-change
projection over the retained logical files. It SHALL expose every canonical
pair association with its ordered endpoint paths, endpoint-category cohort,
commit-evidence IDs, canonical TaskKeys, raw commit and task counts, and whether
the pair is a `G0` edge. A pair is a `G0` edge only when its commit count is
positive; task-only evidence SHALL remain observable but SHALL NOT create a
base-graph edge.

Every `G0` edge SHALL expose its nine-place half-even commit component, task
component, combined co-change weight, cohort-local rank, and the effective
co-change commit/task weights that produced it. Components and ranks SHALL use
only `G0` edges in the same unordered endpoint-category cohort. Graph vertices SHALL retain the
canonical logical-file identity and links to applicable ordered rename-component
provenance. Pair TaskKeys SHALL retain the canonical TaskKey identity used by
the original ordered task provenance.

When `co_change_significance` is configured, the result SHALL expose clusters
formed only from `G0` edges whose already quantized combined weight is greater
than or equal to the threshold. A cluster SHALL contain at least two members,
remain endpoint-cohort-local, sort members by canonical scalar-value path, and
retain only qualifying edges for its maximum and nine-place half-even aggregate.
Without a threshold, the result SHALL retain pair evidence and expose no
clusters.

#### Scenario: Task-only pair remains outside the base graph
- **WHEN** one canonical TaskKey has file episodes for two files but no
  canonical file-evidence commit contains both files
- **THEN** their pair exposes a positive task count and zero commit count but
  is not a `G0` edge

#### Scenario: Threshold-qualified cluster excludes an internal weak edge
- **WHEN** AB has `.600000000`, BC has `.700000000`, AC has `.590000000`, and
  the configured threshold is `.600000000`
- **THEN** the cluster `{A,B,C}` exposes maximum `.700000000` and aggregate
  `1.300000000` from AB and BC only
