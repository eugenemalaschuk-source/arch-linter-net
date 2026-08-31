## Why

The deterministic metric semantics defined for #516 cannot yet be evaluated or
inspected. Users need a trustworthy, read-only way to measure a declared
architecture before choosing a budget, without an incomplete scope looking
like a healthy low value.

## What Changes

- Add policy-owned, schema-validated definitions for the closed metric catalog
  from `architecture-metric-semantics`, with no thresholds, formulas, or
  scripts.
- Evaluate declared metrics from the existing analysis snapshot, topology,
  dependency, ownership, external-group, and public-API authorities; return
  ordinal contributor evidence and the shared applicability projection.
- Add `arch-linter-net measure` as a read-only Human/JSON report command,
  including stable, versioned JSON and explicit measurement scope/evaluability.
- Keep complete neutral measurements out of the architecture-finding/SARIF
  pipeline. Present unassessable scope with the existing typed applicability
  evidence rather than a fabricated metric value.
- Document the policy and command workflow, plus realistic multi-module and
  topology examples.

## Capabilities

### New Capabilities
- `architecture-metric-measurement`: Define schema-backed metric definitions,
  deterministic measure-first evaluation, and read-only Human/JSON reporting.

### Modified Capabilities
- None.

## Impact

- Core policy models, raw/schema validation, evaluation, applicability
  projection reuse, result formatting, and architecture metric tests.
- A new CLI command/module, runtime seam, command tests, public CLI help, and
  measurement documentation.
- Reviewed Core public API snapshots may change if the reusable measurement
  request/result types must be public; no existing policy behavior changes
  without declared metrics or an explicit `measure` invocation.
