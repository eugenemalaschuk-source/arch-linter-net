## Why

Public contract-surface metrics reuse a cached reflection materialization that currently discards
`ReflectionTypeLoadException` completeness. A partially loadable target can therefore produce a
lower trusted metric value even though metric semantics require complete selected export evidence.

## What Changes

- Preserve exported-type-universe completeness in the session-owned public API materialization.
- Make only measure-first public-surface metrics return `missing_required_input` with no partial
  value or contributors when a governed assembly materializes partially.
- Add a regression using a partially loadable public assembly.

## Capabilities

No capability specification delta is required. `architecture-metric-semantics` already requires
public contract-surface metrics to be unassessable when selected observed export facts are
incomplete; this correction transports that existing evidence through the metric path.

## Impact

- `ArchLinterNet.Core` public API scanner, session cache/capture seam, and metric evaluator.
- Core metric applicability tests.
- No public API, policy syntax, report schema, or legacy validation diagnostic behavior changes.
