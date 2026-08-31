## Why

Measure-first architecture metrics must never present a partial direct-reference scan as a
trusted numeric result. The existing metric semantics already require a metric to become
unassessable when required evidence is incomplete, but the metric fact projections currently
discard reflection and IL scan failures.

## What Changes

- Preserve per-source completeness for direct reflection-reference scans in the analysis session.
- Preserve per-source completeness for the metric-only external-dependency IL projection.
- Make topology relation and external-dependency-group metrics return `missing_required_input`
  with no numeric value or contributors when their required source evidence is incomplete.
- Treat explicit unresolved target assemblies as missing measurement evidence before an empty scope
  can be reported as an evaluable zero.
- Treat a partial `Assembly.GetTypes()` universe as missing measurement evidence rather than
  counting only the types that happened to load.
- Add regressions using unloadable reference evidence and focused output/validation coverage.

## Capabilities

No capability specification delta is required. The accepted `architecture-metric-semantics`
specification already requires complete evidence and prohibits partial known-subset values; this
change implements that existing requirement at the scanner boundary.

## Impact

- `ArchLinterNet.Core` type/reference, topology, external-dependency, snapshot and metric projections.
- Core and CLI metric regression tests.
- No policy syntax, public command surface, or validation diagnostic behavior changes.
