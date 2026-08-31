## Why

Metric semantics already distinguish their native evidence authorities, but the completeness
guard currently treats every metric as type-index dependent and public-surface contributors retain
only a simple assembly name. That can either suppress a metadata-complete assembly measurement or
produce a trusted public-surface value after ambiguous assembly resolution.

## What Changes

- Restrict type-universe completeness checks to metrics that actually consume type-derived facts;
  assembly-topology relation and assembly-footprint metrics remain metadata-native.
- Require an exact canonical assembly binding for each public-surface metric input and use that
  identity in contributor pairs; ambiguous simple names become `missing_required_input`.
- Treat whitespace-only metric target fields as present during typed policy validation, matching
  the schema's closed target shape.
- Add focused regressions for each boundary.

## Capabilities

No capability specification delta is required. The accepted `architecture-metric-semantics`
specification already requires assembly-native graph authority, canonical public contributors, and
unassessable output rather than arbitrary multi-target resolution. This correction implements
those existing requirements at their evaluation boundaries.

## Impact

- `ArchLinterNet.Core` metric evaluator, topology identity helper, and metric policy validator.
- Core metric evaluation and validation tests.
- No public API, policy syntax, report schema, or validation-diagnostic behavior changes.
