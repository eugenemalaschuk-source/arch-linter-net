## 1. Import graph and source roles

- [x] 1.1 Add stable import failure categories and a focused fakeable path-identity seam.
- [x] 1.2 Implement portable import grammar, relative resolution, canonical identity, boundary/case checks, cycle/duplicate detection, and graph limits.
- [x] 1.3 Parse and validate root/fragment YAML shapes based on graph role rather than filename.

## 2. Deterministic composition and loader integration

- [x] 2.1 Compose keyed maps, ordered collections, singleton settings, raw classification nodes, and contract groups in depth-first pre-order.
- [x] 2.2 Integrate import resolution and effective-document validation into `ArchitecturePolicyDocumentLoader` without changing its public API or validator order.
- [x] 2.3 Preserve the no-import path and existing monolithic-policy behavior.

## 3. Schemas and tests

- [x] 3.1 Update the root schema and add a fragment schema with shared definitions and filename-neutral role rules.
- [x] 3.2 Add unit tests for portable paths, graph traversal/order, conflicts, limits, role validation, and stable failure categories.
- [x] 3.3 Add integration/regression tests for arbitrary filenames, nested imports, equivalent monolithic models, classification aggregation, and existing fixtures.

## 4. Validation and synchronization

- [x] 4.1 Run formatting and the full repository acceptance gate, fixing all failures.
- [x] 4.2 Synchronize the implementation artifacts and delta spec with the verified behavior.
