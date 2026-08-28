## Why

The initial #697 fix correctly chose the snapshot boundary but deduplicated every projected entry by its serialized identity. That suppresses the existing fail-closed guard when two structurally different semantic facts happen to serialize to the same legacy identity string, silently losing architecture evidence instead of rejecting the snapshot.

## What Changes

- Deduplicate only structurally equivalent semantic-role observations and semantic-context observations during their respective snapshot projections.
- Preserve the final snapshot validator's ability to reject any remaining duplicate `(Kind, Identity)` pair, including a collision caused by ambiguous legacy identity serialization.
- Add a real two-assembly classification regression, plus focused collision and projector-boundary regressions.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-change-report`: Clarify that repeated semantic observations are collapsed only when their structured facts are equivalent, while distinct facts that share a serialized identity remain a fail-closed snapshot error.

## Impact

- `ArchitectureChangeSnapshotProjector` replaces broad entry deduplication with collision-safe semantic observation projection.
- Core tests cover both dynamically emitted distinct CLR types from separate assemblies and a fail-closed serialized-identity collision.
- Snapshot schema, CLI, policy grammar, and per-assembly classification behavior remain unchanged.
