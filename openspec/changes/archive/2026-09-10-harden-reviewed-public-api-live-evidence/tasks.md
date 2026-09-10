## 1. Implementation

- [x] Add a live CLR public-API evidence model to the weakening request.
- [x] Capture public API evidence through the existing application service for debt-gate and CLI approval flows.
- [x] Require exact current-snapshot-to-live-surface equality and calculate the approval delta from base snapshot to live surface.
- [x] Preserve evaluator independence from formatting code.

## 2. Verification

- [x] Cover exact and additions-only approvals, hand-edited snapshots, stale digest, wrong contract, removals, selector widening, and accidental live exports.
- [x] Run focused Core and CLI tests plus repository policy checks.
