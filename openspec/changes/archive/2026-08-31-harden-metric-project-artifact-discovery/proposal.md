## Why

Metric project ownership is fail-closed only after assembly resolution, but
project discovery can collapse distinct output artifacts that share an assembly
simple name. Ordinary measure with explicit target assemblies also omits the
project-output evidence required to establish an exact owner. Both paths can
produce an incomplete native project universe.

## What Changes

- Retain discovery evidence that identifies output assembly names with more
  than one distinct project artifact, and make affected project metrics
  unassessable rather than trusting the artifact selected by probing order.
- Materialize project-output ownership evidence for ordinary measurement that
  needs canonical project ownership, even when `analysis.target_assemblies`
  explicitly selects the assemblies.
- Add regressions for duplicate output names with distinct artifacts and for
  an ordinary `target_assemblies` plus project-footprint measurement.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-metric-semantics`: project metrics must fail closed for
  distinct discovered outputs with one simple assembly name, and ordinary
  measure must establish a canonical owner when an explicit target and a
  unique project output agree.

## Impact

The change affects internal project discovery, runner setup, session metadata
indexes, metric applicability, NUnit coverage, and the metric semantic spec.
It introduces no policy syntax, public API, CLI option, or dependency change.
