## Why

Release Architecture Forensics already defines canonical co-change semantics, but
the ingestion pipeline currently exposes only the raw file and task evidence.
Implementing the graph now gives subsequent bottleneck and reporting work one
deterministic, provenance-preserving source for pair and cluster evidence.

## What Changes

- Build the retained-file `G0` graph from canonical commit co-change evidence.
- Count canonical TaskKey co-change independently, using it only to weight
  existing `G0` edges.
- Normalize graph components within unordered endpoint-category cohorts using
  the validated co-change profile.
- Construct threshold-only `Gtheta` clusters with canonical ordering and
  qualifying-edge aggregates.
- Expose graph vertices, raw pair evidence, components, effective weights,
  threshold, categories, and TaskKey/rename provenance links to downstream Core
  analysis and reporting.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `release-forensics-history-cli`: Include deterministic co-change graph
  evidence in the successful internal history result without changing the
  existing fail-closed ingestion boundary.

## Impact

Affected code is limited to the Core history-analysis pipeline and NUnit
fixtures. The change consumes the established `history_analysis` configuration
and introduces no public API, new executable, dependency, or alternate policy
authority.
