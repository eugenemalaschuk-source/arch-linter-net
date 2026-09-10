# Design

The policy-context export already carries the resolved reviewed snapshot entries. The policy-weakening comparator reuses `PublicApiSnapshotDiffer` to calculate the canonical base/head delta from those entries. An approval is accepted only when its schema/kind, base and current context digests, contract identity, comparison mode, and complete `Added` set match exactly, and the computed delta has no `Removed` or `Changed` entries.

The approval suppresses only the `resolved_snapshot_entries` typed-fact impact finding for that exact public API contract. It therefore cannot suppress selector widening, assembly inventory changes, comparison-mode changes, or any other policy weakening.
