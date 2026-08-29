## Why

The v0.8 architecture-metrics and budget work needs stable values that describe
structural governance facts rather than noisy implementation activity. Without
one reviewed definition for each metric, measure-first reports and future
budgets could count the same dependency differently or conceal an incomplete
measurement universe behind a neutral value.

## What Changes

- Define a bounded architecture-metric semantics capability for the initial
  component dependency, external-group, project/assembly, topology-slice type,
  and reviewed public-contract-surface metric families.
- Specify canonical subject identity, native counting universe, set-based
  deduplication, contributor ordering, and self-edge/cycle treatment for each
  supported metric.
- Define family-specific assessability requirements that reuse the shared
  governance-applicability evidence vocabulary and do not mistake an
  incomplete scope for a zero metric value.
- Reserve measure-first rendering, policy schema, threshold evaluation,
  baselines, and normalized findings for the dependent implementation issues.

## Capabilities

### New Capabilities

- `architecture-metric-semantics`: Deterministic, bounded definitions for
  architecture governance metrics and their applicability evidence.

### Modified Capabilities

- None.

## Impact

- Adds the OpenSpec source-of-truth contract and design artifacts that #517,
  #518, and #519 will consume.
- Reuses existing dependency graph, native topology, project/assembly, public
  API surface, and governance-applicability authorities; it introduces no
  production model, policy schema, CLI command, output projection, finding,
  baseline identity, public API, or runtime behavior in this issue.
