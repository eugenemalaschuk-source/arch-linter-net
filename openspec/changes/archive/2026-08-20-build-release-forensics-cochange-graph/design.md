## Context

Canonical history ingestion already returns ordered commits, TaskKeys, rename
provenance, and logical-file events. `history_analysis` already validates path
categories, ignores, co-change weights, and the optional significance threshold.
The missing step is a deterministic projection of that evidence into the graph
defined by `release-architecture-forensics`.

## Goals / Non-Goals

**Goals:**

- Add an internal, immutable co-change projection to a successful history result.
- Preserve raw commit IDs, canonical TaskKeys, vertex file/rename provenance,
  category cohorts, components, ranks, weights, and threshold for downstream users.
- Build clusters exclusively from threshold-qualified base-graph edges.
- Keep all ordering and numeric calculations canonical and independent of input
  enumeration order.

**Non-Goals:**

- Do not change range ingestion, TaskKey extraction, path identity, rename
  resolution, or policy validation.
- Do not calculate hotspot, bottleneck, centrality, OCP, candidates, or final
  release reports.
- Do not add a public API, configuration file, executable, or dependency.

## Decisions

### Build after canonical file evidence and policy loading

`HistoryPolicyIngestionService` will pass the effective `history_analysis`
configuration into history analysis. The graph builder will run after logical
file construction, so it consumes accepted rename unions and exact pathname
identity rather than recalculating either. The default direct ingestion service
will use the reviewed default configuration.

This is preferred over graph-specific configuration or CLI arguments because
#237 is the sole configuration authority.

### Preserve all pair evidence while identify `G0` explicitly

The graph result will retain canonical pair evidence for every association from
commit or TaskKey episodes, with an explicit base-edge indicator. `G0` contains
only pairs with a positive commit count; task-only pairs remain inspectable with
no normalized edge components. This makes the no-task-topology rule testable and
keeps exact TaskKey links available to later analyses.

Dropping task-only evidence would make its required positive `TaskCoChange`
count unobservable; treating it as an edge would violate the semantic contract.

### Normalize only existing base edges per unordered category cohort

Vertices retain their primary category and the builder derives a canonical
unordered cohort from the two endpoint categories. Each cohort normalizes the
raw commit and task counts of its own `G0` edges, quantizes each component with
nine-place half-even rounding, then applies the validated `alpha` and `beta`.
Paths and tasks use existing scalar-value comparers.

This avoids global cross-category comparison and avoids synthesizing metrics for
task-only pairs.

### Treat thresholding as a cluster view

The builder first completes and orders `G0`; it then filters only its combined
weights when a threshold is configured. A cohort-local connected-components
pass produces clusters with at least two members. Cluster maximum and aggregate
are calculated from qualifying edges only, so the threshold cannot affect raw
pairs, components, or later file-score inputs.

### Keep provenance by reference to canonical evidence

Vertices reference their logical file and associated rename components; pairs
reference commit IDs and canonical TaskKeys. This avoids copied, potentially
divergent provenance while retaining a direct path to the mandatory ingestion
evidence required by #240 and #243.

## Risks / Trade-offs

- [Pair evidence can grow quadratically for wide commits/tasks] → The initial
  implementation is intentionally exact and bounded by the analyzed evidence;
  no lossy threshold is applied before `G0`.
- [Future analysis may need additional metrics] → Keep the model narrowly
  limited to canonical pair/cluster data; #240 and #243 can consume it without
  premature score abstractions.
- [Rounding differences can change threshold equality] → Use `decimal` and one
  shared nine-place half-even quantizer before ranking or comparison.

## Migration Plan

The result is internal and additive. Existing direct ingestion uses the default
effective policy; policy-backed ingestion begins passing its already validated
configuration. There is no data migration or rollback action beyond reverting
the feature commit.

## Open Questions

None. The parent theory and issue acceptance criteria fix the relevant
semantics.
