## Context

Issue #990 spans Core analysis, the existing change snapshot/report contract, the CLI presentation layer, and verified-main badge publication. The repository already retains one immutable `ArchitectureAnalysisSnapshot`, a lazy source/type fact index, project discovery metadata, a canonical project/dependency graph, Core-owned PR report projection, and a trusted Architecture Health badge workflow. The design must preserve those ownership boundaries and the v0.9 low-cost performance gate.

## Goals / Non-Goals

**Goals:**

- Add one immutable `repository-metrics/v1` projection to the retained analysis facts.
- Preserve deterministic source identity, project-edge deduplication, ratio, SCC, and depth semantics.
- Carry absolute metrics through existing validation/health and change snapshot artifacts.
- Render one bounded PR delta and a neutral Source lines badge from Core/CLI-owned data.
- Make missing or partial evidence visible and non-governing.

**Non-Goals:**

- No complexity score, quality grade, metric budget, Architecture Health weighting, trend store, or all-pairs graph analysis.
- No new MSBuild/semantic-analysis/cache subsystem and no second PR publisher.
- No per-method/class complexity, duplication, Sonar/Qodana replacement, or automatic thresholds.
- No broad refactoring of existing report or badge infrastructure unrelated to the new projection.

## Decisions

1. **Project from the retained snapshot.** Add a session-owned calculator invoked once by the existing validation/change/health orchestration. It reads loaded types, the existing source-file fact traversal, discovered projects/references, and canonical assembly/project identity. This keeps metrics additive without another MSBuild load or semantic pass. A small internal source inventory extension retains normalized readable C# paths and line totals produced during the existing source scan.

2. **Use project references as the coupling authority.** For discovered projects, normalize project paths and deduplicate only edges whose source and target are both in the analyzed project set. For fixture/assembly-only inputs, use the retained analyzed assembly identities as a deterministic fallback and mark the snapshot partial when project metadata is unavailable. Project rows use a stable project identity plus display name so same-name outputs cannot collapse.

3. **Use Tarjan SCC plus condensation depth.** Build adjacency lists once from the deduplicated edge set. Tarjan's O(V+E) SCC projection supplies cycle counts/ratios and largest SCC; dynamic programming over the condensation DAG supplies longest edge-count depth. A self-loop is cyclic; isolated/empty graphs have depth zero, zero cyclic components, and zero ratios.

4. **Make artifact extensions optional and additive.** Existing architecture-change snapshot/report schema versions remain compatible: new `repository_metrics` fields are optional for readers of older artifacts. A report computes a delta only when both sides are present, complete/compatible, and version-matched. The health/validation JSON and human output expose the current absolute snapshot independently.

5. **Keep presentation ownership in Core/CLI.** Core models and projectors own typed values and compatibility; the CLI owns human Markdown formatting and the Shields payload command. GitHub workflow glue only transports the generated payload/report through the already reviewed publication boundary.

6. **Keep governance independent.** Metrics are attached to `ValidationOutcome`/Health and change artifacts as evidence, but no existing health dimension, finding, exit code, baseline identity, or metric-budget evaluator reads them. Calculation exceptions become partial/unavailable state at the reporting boundary.

## Risks / Trade-offs

- **Source ownership can be incomplete** → use the existing generated-file and project-root ownership rules, deduplicate normalized paths, and mark unreadable/ambiguous evidence partial instead of inventing totals.
- **Assembly-only fixtures lack project references** → provide a deterministic assembly fallback and preserve a partial reason so consumers know project-level coupling was not authoritative.
- **Adding metrics to validation may materialize lazy facts on more runs** → perform one shared projection, reuse the existing fact index, and add profiling assertions that no second scan/load path is introduced.
- **Old change artifacts have no metrics** → retain current schema compatibility and render an explicit unavailable delta.
- **Badge publication could accidentally become a governance signal** → use a separate neutral payload/label and the existing verified-main transport; never change Architecture Health payload semantics.

## Migration Plan

Ship the additive models, optional artifact fields, CLI/report rendering, tests, and workflow wiring together. Existing artifacts and consumers continue to function without metrics. If publication wiring is rolled back, the typed report/PR capability remains harmlessly available and the existing Architecture Health badge path remains unchanged.

## Open Questions

None. The issue acceptance criteria define the required metric families and the existing repository authorities resolve the remaining boundary decisions.
