## 1. Snapshot projection

- [x] 1.1 Deduplicate projected architecture entries by the existing `(Kind, Identity)` key immediately before snapshot construction, preserving logical entries that differ in either field; verify the Core build has no warnings or errors.
- [x] 1.2 Add a projector-level linked-internal-marker-equivalent regression with repeated semantic role/context facts; verify it proves one entry per logical identity, retains distinct entries, leaves input facts intact, and serializes successfully.

## 2. Verification and specification lifecycle

- [x] 2.1 Run the focused `ArchitectureChangeSnapshotProjectorTests` test family and verify it passes.
- [x] 2.2 Format the changed C# files and verify the formatting diff is clean.
- [x] 2.3 Synchronize the archived OpenSpec specification with the implemented behavior and verify `openspec validate --all` passes.
