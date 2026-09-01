## Why

The initial corrective Health projection still promotes reportable coverage to
blocking failure, omits ordinary audit-only diagnostics, and does not expose a
counter-backed proof that baseline comparison reuses the evaluation snapshot.
Those gaps leave `architecture-health/v1` unable to distinguish non-blocking
evidence from strict failures or demonstrate its single-analysis contract.

## What Changes

- Project coverage according to the existing `analysis.coverage` severity:
  `error` blocks, while `warn` remains visible but non-blocking.
- Add an explicit non-blocking audit-evidence dimension that retains canonical
  audit diagnostics and cycles without changing the strict Health gate.
- Expose immutable snapshot counters on the Health result and test the
  one-composition, one-project-graph, one-assembly-load orchestration contract.
- Add receipt-driven regressions and documentation for those semantics.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `architecture-health-summary`: Preserve severity-aware coverage and
  audit-only evidence while making shared-snapshot orchestration observable.

## Impact

Core Health projection/result APIs, Core tests, reviewed public API snapshots,
and Health CLI documentation are updated additively. CLI and Testing continue
to consume the same Core-owned outcome.
