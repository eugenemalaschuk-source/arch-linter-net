## Context

The engine already computes most of the raw data a dependency graph needs, but never assembles it into a reusable graph:

- `ArchitectureReferenceGraph` (src/ArchLinterNet.Core/Execution/ArchitectureReferenceGraph.cs) does BFS over direct type references and can return `(referencedType, path)` tuples.
- `ArchitectureCoverageInventory.DependencyEdges` (src/ArchLinterNet.Core/Execution/ArchitectureCoverageInventory.cs) already derives deterministic, deduplicated namespace-to-namespace edges from the reference graph.
- `ArchitectureAnalysisSession.CheckAssemblyDependencyContract`/`BuildAssemblyLookup` (src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.AssemblyDependency.cs) already resolves direct assembly-to-assembly references via `Assembly.GetReferencedAssemblies()`, restricted to `Context.TargetAssemblies`.
- `ArchitectureExternalDependencyViolationFinder` and `Document.ExternalDependencies` (src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs) already classify first-party types touching a declared external group.
- Violations already carry `ContractId` and, for transitive dependency/configuration diagnostics, `DependencyPaths` (arrays of full type names).

None of this is exposed as a graph a user can export or query. Issue #62 asks for a normalized graph model, a `graph` export command, and an `explain` command, without changing `validate`/`baseline` behavior.

## Goals / Non-Goals

**Goals:**
- One normalized graph model spanning type, namespace, assembly, and external nodes/edges.
- Deterministic construction (stable ordering) so `graph` output is diffable in CI.
- `graph` and `explain` verbs that reuse the engine's existing execution/session machinery — no parallel analysis pipeline.
- Contract IDs surfaced on edges that participate in a violation.

**Non-Goals:**
- Transitive assembly/project-level path resolution. `assembly-dependency-contracts` already declares `dependency_depth: transitive` unsupported at that granularity (`RequireDirectDependencyDepth` throws); the graph must not imply a capability the engine doesn't have. Assembly-level graph/explain stays direct-only.
- Any change to `validate`/`baseline` output, exit codes, or the YAML policy schema.
- Interactive UI, dashboards, or a general-purpose graph query language.

## Decisions

### 1. Single unified node/edge model, scoped per export by "level"
Rather than three separate models (type graph, namespace graph, assembly graph), use one `ArchitectureDependencyGraph { Nodes, Edges }` with a `ArchitectureGraphNodeKind` discriminant (`Type`, `Namespace`, `Assembly`, `External`). A single build call produces a graph at one selected level (`namespace` default, `type`, or `assembly`); it does not mix levels in one export. This keeps the model simple (one shape to serialize/format) while keeping each individual export's semantics unambiguous — a mixed-level graph would require deciding how a `Type` node relates to an `Assembly` node in the same traversal, which the issue doesn't ask for and the engine has no existing logic for.

Alternative considered: separate `TypeGraph`/`NamespaceGraph`/`AssemblyGraph` types. Rejected — three near-identical shapes with three sets of (de)serialization/formatting code for no behavioral benefit; the level is just a filter over which builder ran.

### 2. Graph builder reuses existing computations, does not reimplement traversal
- `namespace` level wraps `ArchitectureCoverageInventory.DependencyEdges` (already deterministic) rather than recomputing edges from scratch.
- `type` level wraps `ArchitectureReferenceGraph.GetReferencedTypes` direct edges.
- `assembly` level wraps the same `Context.TargetAssemblies` + `GetReferencedAssemblies()` approach already used by `CheckAssemblyDependencyContract`.
- External nodes/edges are added at `namespace`/`type` level only, sourced from the same detection `ArchitectureExternalDependencyViolationFinder` already performs (matching first-party type/namespace against `Document.ExternalDependencies` patterns), not a new IL scan.

This is a deliberate "no new analysis" constraint: the graph is a *view* over data the engine already derives for validation, not a second source of truth that could drift from violation reporting.

### 3. Contract ID attachment via post-hoc mapping, not inline during traversal
The graph builder runs the same contract checks the `validate` path runs (via existing engine/session APIs), collects the resulting violations, then maps each violation back onto the edge(s) it corresponds to:
- Direct violation (`SourceType`/`ForbiddenNamespace` or equivalent pair) → tag the single matching edge.
- Transitive violation with `DependencyPaths` → tag every consecutive pair along each path as an edge, since each hop legitimately "participates" in that violation.

Edges with no matching violation get an empty `ContractIds` list — a graph is exported successfully even when the policy passes with zero violations.

Alternative considered: compute contract IDs live inside each traversal call. Rejected — would duplicate the graph-building logic once per contract-checking codepath; the post-hoc pass is O(violations) and keeps the graph builder pure/independent of contract execution order.

### 4. `graph` and `explain` are non-pass/fail verbs
Both new verbs always exit `0` on success and `2` on runtime error (bad args, missing policy file, unresolvable source/target) — mirroring the `baseline` verb's convention, not the pass/fail (`0`/`1`) convention of `validate`. An `explain` call that finds no path is a *successful* explanation with a negative result, not a failure — reported as `"path": null` in JSON / a plain "no dependency path found" message in human format, exit `0`.

### 5. `explain --level assembly` is rejected, not silently degraded
Since assembly graphs have no transitive path concept (design goal above), `explain --level assembly` prints a clear error ("assembly-level explain does not support path resolution; only direct-edge queries are meaningful — use `graph --level assembly` to inspect direct references directly") and exits `2`, rather than silently falling back to direct-edge-only semantics under the same flag surface a namespace/type explain uses.

### 6. CLI wiring matches the existing hand-rolled dispatcher
`Program.Main` gains two more `args[0]` checks (`"graph"`, `"explain"`) alongside the existing `"baseline"` check, each routed to its own `Run*Command(args[1..])` method with the same manual switch-based option parsing already used by `RunValidateCommand`/`RunBaselineCommand`. No new parsing dependency introduced.

### 7. Graph DTOs live in `Core.Model`, not a self-contained `Core.Graph` namespace
The self-architecture policy's `cli-must-use-validation-application-seam` contract forbids the CLI project from depending on `Core.Execution` directly — the same constraint `ValidationRequest`/`ValidationOutcome` are already subject to, which is why those types live in `Core.Validation` rather than `Core.Execution`. The graph node/edge/level records (`ArchitectureGraphNode`, `ArchitectureGraphEdge`, `ArchitectureDependencyGraph`, `ArchitectureGraphLevel`, `ArchitectureGraphNodeKind`) are pure DTOs with no execution-layer dependencies, so they belong in `Core.Model` alongside `ArchitectureViolation` — CLI already depends on Model freely. The graph *builder* (`ArchitectureDependencyGraphBuilder`), which does need direct session/reference-graph/scanning access, lives in `Core.Execution` next to `ArchitectureCoverageInventory`, the existing class it most resembles (a read-only analytical view built from session internals). `Core.Graph` is reserved for the public seam only: `ArchitectureGraphRequest`/`Outcome`, `ArchitectureExplainRequest`/`Outcome`, the formatter, and the two application services — mirroring the shape of `Core.Validation`. This split was discovered by running the project's own self-architecture policy against the new code (see `architecture/dependencies.arch.yml`, which gained a `core_graph` layer and host-independence contract analogous to `core_validation`'s).

## Risks / Trade-offs

- **Risk**: Tagging every hop of a transitive `DependencyPaths` violation could make a single violation touch many edges, making `graph --format dot/mermaid` visually noisy for large transitive chains. → Mitigation: this only affects contracts that already declare `dependency_depth: transitive`; direct contracts (the common case) tag exactly one edge. Acceptable given the issue explicitly asks for transitive-path visibility.
- **Risk**: Building the graph re-runs contract checks, duplicating work already done by a prior `validate` call in the same CI pipeline. → Mitigation: acceptable per issue's non-goal ("performance optimization beyond deterministic construction is out of scope"); `graph`/`explain` are on-demand diagnostic commands, not part of the hot validation path.
- **Risk**: Users may expect `explain --level assembly` to "just work" like namespace/type. → Mitigation: explicit rejection with an actionable error message, plus documentation in `--help`, rather than a confusing partial result.

## Open Questions

None — acceptance criteria and existing engine capabilities fully bound the design choices above.
