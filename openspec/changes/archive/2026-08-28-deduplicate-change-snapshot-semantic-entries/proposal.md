## Why

`change snapshot` rejects an otherwise valid analysis when the same internal marker source is linked into multiple assemblies and produces identical semantic-role and semantic-context facts. The per-assembly classification facts are correct, but their logical snapshot surfaces collide at validation time and prevent consumers from using snapshots and reports.

## What Changes

- Deduplicate identical `(Kind, Identity)` architecture change entries at the Core snapshot-projection boundary, after all classification facts have been produced and before snapshot validation.
- Preserve one deterministic semantic-role and semantic-context entry for repeated logical facts while retaining distinct entries whenever either kind or identity differs.
- Add focused projector regression coverage for repeated linked-marker-equivalent classification facts.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-change-report`: Complete change snapshots must collapse repeated logical semantic-role and semantic-context surfaces without changing per-assembly classification behavior.

## Impact

- `src/ArchLinterNet.Core/Change/ArchitectureChangeSnapshotProjector.cs` projects a unique logical entry set.
- `tests/ArchLinterNet.Core.Tests/ArchitectureChangeSnapshotProjectorTests.cs` proves duplicate semantic entries are collapsed before snapshot uniqueness validation.
- No CLI surface, snapshot schema version, public API, policy format, or runtime classification behavior changes.
