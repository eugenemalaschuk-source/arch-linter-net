## Why

The consolidated Health workflow evaluates strict, audit, and the no-new-debt
gate over one immutable `ArchitectureAnalysisSnapshot`, but the current-side
architecture change snapshot still starts a separate CLI analysis. Consumer
evidence shows that this projection is the dominant remaining candidate-side
preparation cost after Health consolidation.

## What Changes

- Add an optional Health workflow output that writes the canonical current
  change snapshot from the caller-owned immutable analysis session.
- Reuse the retained runner/session for validation, baseline candidate
  comparison, and the historical namespace/assembly graph projections.
- Preserve graph semantics, canonical identities/order, cancellation, and
  fail-closed preflight behavior.
- Keep public-API verification and persisted cross-process prepared state out
  of this seam.
- Prove serialized equivalence with the independent legacy workflow and add a
  CLI integration path.

## Capabilities

### New Capabilities

### Modified Capabilities

- `analysis-snapshot`: allow one caller-owned snapshot to serve Health,
  baseline, and canonical change projections in one process.
- `architecture-change-report`: allow Health to persist the current snapshot
  from the same complete analysis session.

## Impact

The Core snapshot remains the sole owner of the prepared runner and its
disposable load scope. The CLI gains `health --change-snapshot <path>`.
Existing single-command behavior and standalone `change snapshot` remain
unchanged. No persisted analysis cache or cross-process authorization
protocol is introduced.
