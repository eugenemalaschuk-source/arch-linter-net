## Why

The initial measure implementation exposes a partial or collapsed native
universe in several ambiguous identity cases, despite the accepted metric
semantics requiring canonical contributors and fail-closed scope completeness.
This correction closes those gaps before the draft PR can be approved.

## What Changes

- Preserve canonical resolved-assembly and type identities in metric contributor
  sets instead of deduplicating by display/simple names.
- Keep an external dependency fact outside the selected bounded target from
  producing an unrelated project-ownership failure.
- Permit the trusted zero result when the topology scope explicitly allows an
  empty universe.
- Treat an assembly endpoint with more than one same-simple-name retained
  subject as ambiguous without using its reference identity to choose one.
- Preserve resolved assembly subjects that have no loadable types, and fail
  closed when an ambiguous endpoint can belong to the metric's selected node.
- Add focused regressions for every corrected boundary.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. The accepted `architecture-metric-semantics` and
`architecture-metric-measurement` requirements already mandate these
behaviours; this change brings their implementation into conformance.

## Impact

`ArchitectureMetricEvaluator`, `ArchitectureTopologyEvaluator`, and focused
Core metric/topology tests. No public API, policy schema, or CLI option changes.
