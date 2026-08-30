## Why

Declared-topology drift is currently inferred from exactly mapped subjects even
when mapping evidence is incomplete. An ambiguous or unmapped subject can
therefore create false stale-node or stale-edge findings, which turns a
mapping-completeness problem into an incorrect declaration-drift diagnosis.

The reviewed Core public API snapshot also needs to remain generated from the
full assembled public surface so the approval test stays authoritative.

## What Changes

- Gate stale-node and stale-edge inference on complete topology mapping
  evidence, preserving ambiguous/unmapped subjects as their own applicability
  evidence rather than treating them as absent observations.
- Retain declaration drift only when the evaluator can prove the relevant
  observed mapping graph is complete.
- Add focused regression coverage for ambiguous node mappings and incomplete
  dependency endpoints.
- Regenerate and verify the reviewed Core public API snapshot through its
  canonical lifecycle.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `declared-topology-evaluation`: declaration-drift inference requires
  complete mapping evidence and must not be derived from ambiguous or unmapped
  subjects.

## Impact

- `ArchitectureTopologyEvaluator` drift calculation and its regression tests.
- Declared-topology evaluation specification and archived change history.
- Reviewed Core public API snapshot validation.
