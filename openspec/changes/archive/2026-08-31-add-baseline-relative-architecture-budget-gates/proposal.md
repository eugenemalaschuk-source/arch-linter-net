## Why

Absolute metric budgets are useful once a repository can meet a fixed limit, but
they cannot prevent a legacy metric from getting worse without authorizing all
existing debt. The finding-level baseline remains the authority for accepted
violations; metric ratcheting needs a separate, reviewed scalar baseline.

## What Changes

- Add baseline-relative modes to existing strict and audit metric-budget
  contracts: `no_worse_than_baseline` and `max_delta`, with an optional
  absolute maximum cap.
- Add a version-3 baseline document shape with a separate deterministic metric
  baseline collection. It stores a scalar value and canonical metric identity,
  without altering finding-level baseline matching or suppression.
- Capture eligible metric values in explicit baseline generation, while update
  and prune retain reviewed metric baseline values unchanged. Normal validation
  never writes or refreshes a metric baseline.
- Fail closed when a selected relative gate lacks a baseline or when its metric
  identity no longer matches the reviewed entry; emit current, baseline, delta,
  threshold, cap, and canonical contributor evidence for relative failures.
- Extend schemas, Core models, normal finding projections, documentation, and
  focused tests while preserving policies and v1/v2 baselines that do not use
  relative metric gates.

## Capabilities

### New Capabilities

- `architecture-metric-baseline-gates`: Reviewed, versioned metric-value
  baselines and deterministic relative budget enforcement.

### Modified Capabilities

- `architecture-metric-budgets`: Metric-budget contracts gain bounded
  baseline-relative modes in addition to existing absolute bounds.
- `baseline-generation`: Baseline lifecycle supports metric-baseline capture
  and preservation without automatic value updates or finding-debt conflation.

## Impact

- Core baseline models/loading/generation and metric-budget policy validation,
  execution, diagnostic payloads, normalized output, and reviewed public API.
- Baseline and policy JSON schemas, packaged compatibility metadata, Core/CLI
  tests, and metric/baseline authoring documentation.
- No new dependency, validation mode, hosted history, automatic approval, or
  replacement of the finding-level baseline/debt lifecycle.
