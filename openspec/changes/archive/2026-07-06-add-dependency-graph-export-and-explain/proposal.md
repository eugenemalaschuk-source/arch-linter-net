## Why

Today the engine reports violations and, for some transitive dependency contracts, an ad-hoc path of type names — but there is no way to see the dependency graph itself, or to ask "why does A depend on B?" without re-deriving it from raw diagnostics. Issue #62 asks for a normalized graph export and an explain command so developers can understand architecture violations and dependency paths directly, without hand-reconstructing them.

## What Changes

- Add a normalized `ArchitectureDependencyGraph` model covering type, namespace, assembly, and external-group nodes/edges, built deterministically from the existing analysis session.
- Add a graph builder that assembles the graph at a selectable level (`namespace` default, `type`, `assembly`), reusing existing infrastructure (`ArchitectureReferenceGraph`, `ArchitectureCoverageInventory`, the assembly-dependency-contract assembly lookup, and `Document.ExternalDependencies`) rather than introducing new resolution logic.
- Attach contract IDs to graph edges that participate in a violation (direct violations and every hop of a transitive `DependencyPaths` violation).
- Add a new CLI verb `graph` that exports the dependency graph in `json`, `dot`, or `mermaid` format.
- Add a new CLI verb `explain` that reports the direct or shortest dependency path between a `--source` and `--target` node (type/namespace level), including the external-group case, and an explicit "no path" result when none exists.
- Both new verbs are strictly additive: the no-verb `validate` command and the `baseline` command keep their existing behavior and exit codes.

## Capabilities

### New Capabilities
- `dependency-graph-model`: normalized graph model (nodes/edges/kinds) and deterministic graph construction at namespace/type/assembly level, with contract IDs attached to violating edges.
- `graph-export-command`: CLI `graph` verb exporting the dependency graph in JSON/DOT/Mermaid formats.
- `explain-command`: CLI `explain` verb reporting direct/transitive/no-path/external-group dependency explanations between two nodes.

### Modified Capabilities
- None. This change does not alter the requirements of `dependency-contracts`, `cycle-contracts`, `diagnostics-model`, `violation-reporting`, `dependency-edge-coverage-contracts`, or `assembly-dependency-contracts` — it reads their existing outputs to build the new graph, without changing validation behavior.

## Impact

- New code under `src/ArchLinterNet.Core/Model/` (graph node/edge/level DTOs — placed here, not a dedicated namespace, because the self-architecture policy forbids the CLI from depending on `Core.Execution` directly, the same reason `ArchitectureViolation` lives in Model), `src/ArchLinterNet.Core/Execution/` (the graph builder, alongside `ArchitectureCoverageInventory` which it reuses), and `src/ArchLinterNet.Core/Graph/` (the public request/outcome/formatter/application-service seam, mirroring the `Core.Validation` folder's shape).
- `src/ArchLinterNet.Cli/Program.cs` gains two new verb dispatches (`graph`, `explain`) alongside the existing `baseline` verb, following the same hand-rolled arg-parsing pattern.
- `architecture/dependencies.arch.yml` (the project's own self-policy) gains a `core_graph` layer and matching host-independence/DI/MSBuild contracts, mirroring `core_validation`, since the new `Core.Graph` namespace must itself be governed.
- No changes to YAML policy schema, no changes to `validate`/`baseline` exit codes or output.
- New NUnit tests under `tests/ArchLinterNet.Core.Tests` (graph builder/model, explain service) and CLI integration tests (graph/explain verbs).
