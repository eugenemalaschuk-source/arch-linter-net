## Why

The initial `architecture-health/v1` projection mistakes applicability evidence for
conformance, drops canonical provenance, and collapses waiver lifecycle states.
It can therefore report a passing dimension while the owning authority reports a
failure, preventing the result from serving as a canonical health contract.

## What Changes

- Preserve conformance separately from evaluability for topology, metrics, and
  imported external diagnostics by projecting their existing typed findings and
  receipts.
- Preserve canonical reason provenance in health output, including family,
  control identity, policy identity, and bounded evidence identity where the
  source authority supplies it.
- Project the authoritative waiver lifecycle state rather than inferring it
  from aggregate inventory totals.
- Reuse one analysis snapshot/candidate collection for health and persistent
  debt comparison, and avoid classifying resolved-only baseline entries as new
  architecture debt or degradation.
- Add regression coverage for the reviewed semantic scenarios.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `architecture-health-summary`: Preserve authority conformance, provenance,
  lifecycle, and baseline-change semantics in the canonical health summary.

## Impact

Core health models/projector/orchestration and their public snapshots may
change additively; CLI and Testing continue to render the same Core result.
The change affects focused Core and CLI tests, documentation, and the existing
Architecture Health OpenSpec specification.
