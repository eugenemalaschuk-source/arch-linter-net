## Context

`architecture-coverage-model` (#97, already merged) reserved `scope: dependency_edge` and a `between: List<List<string>>` field on `ArchitectureCoverageContract` (already present in `ArchitectureContractModels.cs`), but deliberately left the enforcement semantics — what concretely makes a layer pair "governed" — to the implementing change. `ArchitectureRunnerFactory.ValidateImplementedCoverageScopes` currently throws for `dependency_edge`, so no policy can declare it today.

The raw data already exists: `ArchitectureCoverageInventory.BuildDependencyEdges()` computes every first-party namespace-to-namespace edge (`ArchitectureCoverageDependencyEdge(SourceNamespace, TargetNamespace)`) by walking the type reference graph and filtering to first-party targets. This change classifies those edges against declared layer pairs.

Three existing contract families already express layer-to-layer relationships and are candidates for "governing" an edge:
- `ArchitectureDependencyContract`: `Source` (single layer name) + `Forbidden` (list of layer names) — a directed, explicit forbidden-pair declaration.
- `ArchitectureLayerContract`: `Layers` (ordered list) — enforces inward-only ordering across the whole chain; any two layers in the list have their relative ordering checked in both directions.
- `ArchitectureIndependenceContract`: `Layers` (list) — enforces no references between any pair in the list, in either direction.

## Goals / Non-Goals

**Goals:**
- Classify observed first-party namespace-to-namespace edges into `covered`, `excluded`, or `uncovered` per declared `(sourceLayer, targetLayer)` pair in a `scope: dependency_edge` contract's `between` list.
- Reuse the existing `Coverage` diagnostic kind, `ArchitectureCoverageSummary` shape, and audit/strict severity wiring without introducing new diagnostic types or severity values.
- Make "governed by" a structural check against already-declared dependency/layer/independence contracts, not a new rule-authoring surface.

**Non-Goals:**
- Replacing or altering the behavior of dependency, layer, or independence contracts themselves.
- Semantic data-flow or call-graph analysis — edge detection is the existing namespace-level reference graph, unchanged.
- A fourth "out-of-scope" coverage status: layer pairs not declared in any `between` list are simply never evaluated, not classified.

## Decisions

### Decision: "Governed by" is a per-contract-family structural check, evaluated per declared pair
For a declared pair `(A, B)` (source layer A, target layer B), the pair is `governed` (and thus edges within it are `covered`, absent an exclusion) when any of the following holds across the document's loaded contracts (strict and audit, all families, regardless of which family's edges are being scored):
1. **Dependency contract**: some `ArchitectureDependencyContract` has `Source == A` and `Forbidden` contains `B` (ordinal). The contract explicitly names this directed pair as governed, whether or not a violation is currently present.
2. **Layer contract**: some `ArchitectureLayerContract.Layers` contains both `A` and `B` (any order, since the layer contract's chain ordering check governs every pair within it, both the allowed forward direction and the forbidden backward direction).
3. **Independence contract**: some `ArchitectureIndependenceContract.Layers` contains both `A` and `B`. Independence is bidirectional by definition, so this governs both `(A, B)` and `(B, A)`.

Rationale: each of these three contract families already determines, structurally, whether a violation *would* be raised if an edge crossed that pair. Reusing their declared layer lists means coverage answers "is this edge inside a contract's jurisdiction" without re-implementing or guessing at violation logic, and without requiring a new policy-authoring concept beyond the already-specified `between` field.

Alternative considered: treat a pair as governed only if the source/target layers literally appear in the *same* contract instance as adjacent or violation-eligible items (e.g., only `Forbidden` membership, not layer-contract chain membership). Rejected because layer contracts are the dominant mechanism for ordering enforcement in this codebase (see `architecture/dependencies.arch.yml`), and excluding them would make nearly every real edge "uncovered" even when a layer contract already enforces it — defeating the purpose of distinguishing blind spots from already-governed edges.

### Decision: Edge-to-pair matching reuses `ArchitectureLayerResolver.MatchesNamespace`
An observed edge `(sourceNamespace, targetNamespace)` matches declared pair `(A, B)` when `sourceNamespace` matches declared layer `A`'s namespace pattern and `targetNamespace` matches declared layer `B`'s namespace pattern, using the same `ArchitectureLayerResolver.MatchesNamespace` matcher already used by namespace/assembly/project coverage and by layer/independence/dependency checking. No new matching logic is introduced.

### Decision: Both the summary and findings classify per observed edge, not per declared pair
`BuildDependencyEdgeCoverageSummary` and `CheckDependencyEdgeCoverageContract` both classify and count *every observed edge instance* (source/target namespace pair) within a declared `between` pair — not one aggregated outcome per declared pair. A declared pair with five observed edges, four governed and one not, contributes four to `covered` and one to `uncovered`/an uncovered finding, not a single "this pair is partially covered" outcome. This mirrors namespace coverage's per-namespace (not per-root) granularity: `between` declares the scope being classified, the same way `roots` does for namespace coverage, but the unit being classified is the edge, not the pair.

### Decision: Exclusions match by declared pair, not by individual edge
`exclude` entries for `dependency_edge` scope set `between: [A, B]` (reusing the same field name/shape as the contract's own `between`, scoped to one exclusion entry) plus mandatory `reason`. An exclusion suppresses every edge matching that pair, not individual source/target namespace combinations — keeping exclusion granularity consistent with how the pair itself is declared and avoiding a second namespace-glob surface on top of layer-pair declaration.

### Decision: Loader validation mirrors existing scope-exclusivity checks
`ArchitectureContractLoader`'s existing per-scope field validation (which already rejects `roots` on `assembly`/`project` scope, etc.) gains a `dependency_edge` branch requiring non-empty `between`, each pair has exactly two non-empty declared layer names, and rejecting `roots`/`contract_ids` on this scope.

## Risks / Trade-offs

- **Risk**: A layer contract with many layers makes nearly every pair "governed," weakening the signal. → **Mitigation**: this matches the existing semantics of layer contracts (any pair's ordering is genuinely checked), so it is correct, not a workaround; policy authors who want finer-grained coverage should declare narrower layer contracts, which is an existing, unchanged lever.
- **Risk**: Coverage findings could be confused with dependency/layer/independence violations. → **Mitigation**: reuse the existing `Coverage` diagnostic kind (distinct from `Dependency`), exactly as namespace/project/assembly coverage already does — no new ambiguity beyond what's already accepted for those scopes.
- **Trade-off**: Edges outside any declared `between` pair are invisible to this scope (by design, matching "uncovered is namespace/project/assembly-style, not exhaustive"). This is consistent with `architecture-coverage-model`'s existing "roots membership determines what is classified" principle, just expressed via `between` instead of `roots`.

## Open Questions

None outstanding — the per-contract-family governance rule above resolves the one explicit open design question carried over from `architecture-coverage-model`.
