## 1. Graph model

- [x] 1.1 Add `ArchLinterNet.Core.Graph` namespace with `ArchitectureGraphNodeKind` enum (`Type`, `Namespace`, `Assembly`, `External`), `ArchitectureGraphNode(Id, Kind)`, `ArchitectureGraphEdge(SourceId, TargetId, SourceKind, TargetKind, ContractIds)`, and `ArchitectureDependencyGraph(Nodes, Edges)`.
- [x] 1.2 Define a `GraphLevel` enum (`Namespace`, `Type`, `Assembly`) used to select build granularity.

## 2. Graph builder

- [x] 2.1 Add a graph builder in `Execution` that produces namespace-level nodes/edges by wrapping `ArchitectureCoverageInventory.DependencyEdges`.
- [x] 2.2 Add type-level graph construction using `ArchitectureReferenceGraph`'s direct reference enumeration.
- [x] 2.3 Add assembly-level graph construction reusing the `Context.TargetAssemblies` + `Assembly.GetReferencedAssemblies()` approach from `ArchitectureAnalysisSession.AssemblyDependency.cs` (direct-only).
- [x] 2.4 Add `External` node/edge construction for `namespace`/`type` levels from `Document.ExternalDependencies` and the existing external-dependency detection logic; omit at `assembly` level.
- [x] 2.5 Run the normal contract checks and map resulting violations onto graph edges: direct violations tag the single matching edge; transitive `DependencyPaths` violations tag every consecutive-pair edge along each path.
- [x] 2.6 Sort nodes by `(Kind, Id)` and edges by `(SourceId, TargetId, SourceKind)` ordinally; sort each edge's `ContractIds` ordinally.

## 3. Graph export formats

- [x] 3.1 Implement JSON serialization: `{ "nodes": [{ "id", "kind" }], "edges": [{ "source", "target", "sourceKind", "targetKind", "contractIds" }] }`.
- [x] 3.2 Implement DOT (Graphviz) formatting with `label` set to joined `ContractIds` when non-empty.
- [x] 3.3 Implement Mermaid (`graph TD`) formatting.

## 4. CLI: `graph` verb

- [x] 4.1 Add `graph` dispatch in `Program.Main` alongside the existing `baseline` verb.
- [x] 4.2 Parse `--policy`, `--mode`, `--level`, `--format`, `--condition-set`, `--contract`, `--help` following the existing manual switch-based parsing style; validate `--level`/`--format` values and exit `2` on invalid input.
- [x] 4.3 Wire to the graph builder and selected formatter; print to stdout; exit `0` on success regardless of violation count, `2` on runtime error.
- [x] 4.4 Add `graph` usage text to CLI help output.

## 5. CLI: `explain` verb

- [x] 5.1 Add `explain` dispatch in `Program.Main`.
- [x] 5.2 Parse `--source`, `--target`, `--policy`, `--level` (namespace/type only — reject `assembly` with exit `2`), `--format`, `--condition-set`, `--help`.
- [x] 5.3 Build the graph internally at the requested level; check for a direct edge first, then run BFS shortest-path.
- [x] 5.4 Implement external-group resolution: when `--target` matches a key in `Document.ExternalDependencies`, resolve first-party nodes with an edge into that `External` node.
- [x] 5.5 Implement human and JSON output formats, including the explicit "no dependency path found" / `"path": null` result.
- [x] 5.6 Add `explain` usage text to CLI help output.

## 6. Tests

- [x] 6.1 Unit tests for deterministic ordering (nodes, edges, contract IDs) across repeated builds.
- [x] 6.2 Unit tests for namespace/type/assembly level graph construction (direct edges, transitive-only assembly reference excluded).
- [x] 6.3 Unit tests for contract ID attachment (direct violation single edge; transitive violation multi-hop tagging).
- [x] 6.4 Unit tests for external-group node/edge construction.
- [x] 6.5 CLI integration tests for `graph` verb: default JSON output, `--level`/`--format` combinations, invalid option handling, exit codes.
- [x] 6.6 CLI integration tests for `explain` verb: direct path, transitive path, no-path case, external-group case, assembly-level rejection, JSON/human formats.
- [x] 6.7 Verify existing `validate`/`baseline` CLI integration tests still pass unmodified (no behavior change).

## 7. Spec synchronization and archive

- [x] 7.1 Confirm implementation matches `specs/dependency-graph-model`, `specs/graph-export-command`, `specs/explain-command` delta specs; adjust either code or specs if divergence is found during implementation.
- [x] 7.2 Run `openspec validate --all` (or equivalent) after implementation and tests are complete.
- [x] 7.3 Run `openspec archive add-dependency-graph-export-and-explain` to rebuild `openspec/specs/<capability>/spec.md` for the three new capabilities.
