## 1. Core snapshot contract

- [x] 1.1 Add versioned snapshot, surface-entry, finding/debt, and delta result models with stable canonical identities.
- [x] 1.2 Build a complete-analysis snapshot service by reusing graph, coverage, semantic classification, normalized finding, and baseline identity facts.
- [x] 1.3 Add strict snapshot parsing, compatibility validation, deterministic JSON serialization, comparison, and human rendering.

## 2. CLI integration

- [x] 2.1 Add a composed `change` command module with `snapshot` and `report` instance handlers and typed options.
- [x] 2.2 Wire the Core service through the existing CLI runtime/composition seam and implement output/error/exit-code behavior.

## 3. Tests and public surface

- [x] 3.1 Add Core fixtures and tests for new namespace, project/assembly, dependency edge, removed namespace, semantic context, coverage blind spot, baseline-existing debt, ordering, and invalid snapshots.
- [x] 3.2 Add CLI tests for snapshot/report parsing, human and JSON output, completed-drift exit behavior, and no-report validation compatibility.
- [x] 3.3 Update reviewed Core public API snapshots if the new supported Core service changes the public surface.

## 4. Documentation and validation

- [x] 4.1 Document the snapshot/report workflow and CI integration in user-facing MkDocs pages.
- [x] 4.2 Run focused Core and CLI tests, formatting, OpenSpec validation, and the architecture lint implicated by the new Core/CLI boundaries.
