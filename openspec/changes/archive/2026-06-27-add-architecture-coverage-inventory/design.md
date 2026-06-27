## Context

`ArchitectureAnalysisSession` already lazily builds `ArchitectureTypeIndex` and `ArchitectureReferenceGraph` per run. `ArchitectureContractRunner` already resolves layers via `ArchitectureLayerResolver`/`NamespaceGlobPattern`, expands templates via `LayerTemplateExpander`, and discovery facts come from `ArchitectureProjectDiscovery.ResolveFromDocument()` during `ArchitectureRunnerFactory.BuildRunner()`. The architecture-coverage-model spec (design-only) defines the vocabulary future coverage contracts will use, but no shared inventory exists yet — issue #97 is solely the inventory engine those future contracts (#98-#103) will consume.

## Goals / Non-Goals

**Goals:**
- Provide one deterministic, reusable `ArchitectureCoverageInventory` per session that surfaces: namespaces, a representative type per namespace, declared layers, expanded layer templates (with `Exhaustive`), project/assembly facts, and namespace/layer-level dependency edges.
- Reuse existing scanning/resolution/discovery components without re-implementing matching or scanning.
- Guarantee stable ordering across repeated builds against the same inputs (no reflection-order or dictionary-order leakage).

**Non-Goals:**
- No classification of units as covered/uncovered/excluded/unknown/stale/empty-input — that is the responsibility of future coverage contract handlers.
- No `exclude` entry handling (coverage contracts don't exist yet for this engine to read excludes from).
- No wiring into `ArchitectureContractRunner`/`ArchitectureContractCatalog` contract execution, and no change to the existing `ArchitectureRunnerFactory` guard rejecting declared `strict_coverage`/`audit_coverage` contracts.
- No CLI/report output format.

## Decisions

**Location: `src/ArchLinterNet.Core/Execution/ArchitectureCoverageInventory.cs`.**
Per `architecture/dependencies.arch.yml`, `core_execution` already depends on `core_model`, `core_resolution`, `core_scanning`, `core_contracts` and must not depend on `core_validation`. The inventory only needs types from those allowed layers (it consumes `ArchitectureTypeIndex`, `ArchitectureReferenceGraph`, `ArchitectureLayerResolver`, `LayerTemplateExpander`, all already in `core_execution`/`core_resolution`, and `ArchitectureProjectDiscovery` in `core_discovery`/Discovery namespace — confirmed as an allowed existing reference since `ArchitectureRunnerFactory` already calls it from Execution-adjacent code). This avoids a new layer and keeps the change additive only.

**Construction: a static factory `ArchitectureCoverageInventory.Build(...)` rather than a constructor with side effects.**
Mirrors `LayerTemplateExpander.Expand(...)`'s static factory style already used in this codebase for derived, read-only facts. Takes the already-built `ArchitectureContractDocument`, `ArchitectureAnalysisSession` (for `TypeIndex`/`ReferenceGraph`), and the already-resolved `ProjectDiscoveryResult` so it never re-scans assemblies or re-parses project files — it only aggregates facts already collected elsewhere in the session.

**Namespace and representative-type collection: derive from `ArchitectureTypeIndex`'s loaded types, not a fresh reflection pass.**
`ArchitectureTypeIndex` already lazily loads all types for target assemblies. The inventory groups those types by `Type.Namespace`, sorts namespaces with `StringComparer.Ordinal`, and picks the alphabetically-first type's full name as representative — deterministic and avoids a second `ArchitectureTypeScanner.GetLoadableTypes()` pass.

**Layer/template facts: store the already-resolved `ArchitectureLayerContract` list (declared layers) and `LayerTemplateExpander.Expand(document.LayerTemplates)` output as-is**, since both are already order-stable (template expansion iterates the document's declared template list in document order, and declared layers are stored in document order — the inventory leaves contract-declaration order untouched rather than re-sorting layers, since layer identity/order already matters for diagnostics elsewhere in the codebase; only namespace and dependency-edge collections, which have no inherent author-declared order, are explicitly sorted).

**Dependency edges: aggregate `ArchitectureReferenceGraph.GetReferencedTypes(type)` results to namespace pairs.**
For every type the inventory's namespace map captures, look up direct referenced types via the existing `ArchitectureReferenceGraph`, map each to its namespace, and produce deduplicated `(sourceNamespace, targetNamespace)` pairs excluding self-edges, sorted by source then target with `StringComparer.Ordinal`. This is namespace-level (not full layer resolution) so it stays a pure aggregation step; mapping namespace edges to layer edges is left to whichever future contract needs that view, since layer resolution can yield ambiguous/no-match cases that are a classification concern, not an inventory concern.

**Project/assembly facts: store the `ProjectDiscoveryResult` (or null) verbatim rather than re-deriving project info.**
The spec requires the inventory to "represent project/assembly data when discovery information is available" — when discovery wasn't run or returned nothing, the field is simply absent (`null`/empty), which downstream consumers interpret as "unknown," not fabricated data.

## Risks / Trade-offs

- **Risk:** Aggregating reference-graph edges at namespace level for every namespace could be expensive for very large assemblies. → **Mitigation:** Edges are computed lazily (only when a consumer asks for them) and reuse the already-memoized `ArchitectureReferenceGraph` rather than rescanning IL; explicit performance tuning beyond avoiding duplicate scans is out of scope per the issue's non-goals.
- **Risk:** Picking "alphabetically first type" as representative may not always be the most diagnostically useful type. → **Mitigation:** Acceptable per the issue's acceptance criteria, which only requires *a* representative type, not the *best* one; future contract handlers can layer better heuristics on top of the inventory's raw per-namespace type list without changing the inventory's shape.
- **Risk:** Building the inventory unconditionally on every run (even when no coverage contract is declared) could regress unrelated performance/behavior. → **Mitigation:** Per the issue's acceptance criteria ("Existing validation behavior remains unchanged when no coverage contract is configured"), the inventory is exposed as an opt-in lazily-built accessor (e.g. on `ArchitectureAnalysisSession`, following the existing `Lazy<T>` pattern used for `TypeIndex`) — it is never built unless something asks for it, so no behavior or performance changes for existing policies.

## Open Questions

None — scope is fully bounded by issue #97's stated acceptance criteria and non-goals.
