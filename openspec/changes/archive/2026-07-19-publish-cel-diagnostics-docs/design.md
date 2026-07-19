## Context

CEL `when` predicates compile and evaluate today (#164/#170), and the raw data needed to explain a match already exists at evaluation time:

- `ArchitectureContextSelector` (`src/ArchLinterNet.Core/Contracts/ArchitectureContextSelector.cs`) already carries `When` (raw source text), `WhenLocation` (resolved YAML path/source region), and `WhenContractName`.
- `ArchitectureContextSelectorMatcher.Matches(...)` (`src/ArchLinterNet.Core/Execution/ArchitectureContextSelectorMatcher.cs`) already builds a human-readable description string (`"Contract 'X' at 'Y' (role: R, when: <expr>) for source '...' -> target '...'"`) and calls `ArchitectureExpressionFactService.Evaluate(...)`, but today that description is only used to build an exception message on evaluation *failure* — a successful `true`/`false` result carries no record of which expression ran.
- No violation payload, JSON DTO, SARIF result, or `explain` output currently threads this data through. `openspec/specs/cel-policy-model/spec.md`'s "Reporting preserves provenance and expression explainability" requirement explicitly deferred `explain` and SARIF integration as "separately-scoped follow-up work" — this change is that follow-up.
- `ArchitectureDependencyGraphBuilder` (`src/ArchLinterNet.Core/Execution/ArchitectureDependencyGraphBuilder.cs`) builds `explain`'s underlying graph from **raw type references** at Type level (`PopulateTypeEdges`, via `session.ReferenceGraph.GetReferencedTypes`) — independent of any contract — then overlays `ContractIds` onto existing edges from the violation list. A `when`-narrowed forbidden selector that does *not* match produces no violation and never touches the graph; the graph itself has no concept of selectors or expressions.
- Public docs: the only existing CEL policy-model content lives in `docs/internal/`, excluded from the MkDocs build (`exclude_docs: internal/`). No public page exists.

## Goals / Non-Goals

**Goals:**
- Surface already-computed `when` expression source text and match result through human-readable, JSON, and SARIF output for context-dependency, context-allow-only, and layout-convention diagnostics.
- Give `explain` CEL awareness for the specific source/target pair it is asked to explain, without turning it into a full contract-evaluation surface.
- Publish a single canonical, public GitHub Pages CEL guide, reachable from README/nav/schema docs, that separates standard CEL knowledge from the ArchLinterNet profile/context contract.
- Keep every change additive: no existing JSON/SARIF/human field changes shape or disappears; no existing YAML/policy-load behavior changes.

**Non-Goals:**
- No changes to the CEL parser/binder/evaluator or `CelDiagnostic`/`CelDiagnosticCode` — that diagnostic layer is already sufficient per the issue's acceptance criteria; this change only surfaces it.
- No general-purpose "why didn't this selector match" tool for non-CEL literal selectors — expression provenance is scoped to nodes that actually declare `when`.
- No change to which YAML locations accept `when` (that closed list is owned by `cel-policy-model` and unchanged here).
- No automatic GitHub Pages deployment — `release-nuget.yml`'s `deploy-docs` job stays a manual `workflow_dispatch` step the user triggers after merge.
- `explain` does not gain a general "evaluate arbitrary contract against arbitrary types" mode; it only reports on `when`-bearing selectors already relevant to the graph path/edge it resolves.

## Decisions

### D1: Record expression participation as an additive payload field, not a new diagnostic kind
Add an optional `ExpressionParticipation` record (expression source text, YAML location, contract/selector identity, and `Matched | NotMatched | EvaluationFailed`) to the existing contextual-dependency/contextual-allow-only/layout-convention violation payloads, populated by `ArchitectureContextSelectorMatcher` at the point it already builds the description string for evaluation. This follows the established `diagnostics-model` pattern ("family-specific evidence lives on a payload type, not the shared violation record") instead of introducing a new `ArchitectureDiagnosticKind`. Layout-convention `files_matching.when` reuses the same shape since it already produces its own violation payload.

**Alternative considered:** a new `CelDiagnosticKind` mirrored from `ArchLinterNet.CEL`. Rejected — `CelDiagnostic` models compile/evaluation *errors*, not "this predicate ran and matched"; conflating the two would blur the already-clean separation between engine diagnostics and product-level violation reporting.

### D2: JSON/SARIF field naming follows existing snake_case/additive precedent
JSON gains a `when_expression` object (`source`, `result`, `yaml_path`) on violations that have one, mirroring how `policy_location` was added additively (see `diagnostics-model`'s "Human and CI JSON formatters expose policy origin additively"). SARIF gains a related location + message referencing the same data, following the existing `relatedLocations` pattern used for policy-origin provenance. Diagnostics without a `when` omit the field entirely rather than emitting `null`, consistent with the existing "Policy location JSON has one optional-field shape" requirement.

### D3: `explain` re-evaluates only the `when`-bearing selectors relevant to its own resolved path, not a second contract run
`ArchitectureExplainApplicationService` stays a graph/shortest-path service; it does not gain general contract evaluation. Instead, the CLI-facing explain flow, after resolving `source`/`target`/`path`, looks up context-dependency/context-allow-only contracts whose selectors declare `when` and are attributed (via the already-tagged `ContractIds` on path edges) to an edge on the resolved path, then re-runs `ArchitectureContextSelectorMatcher` for just that selector against the real source/target types already on hand (no new type scan, no new violation pass). This keeps `explain` fast and matches its existing "view over already-computed data" design note in `ArchitectureDependencyGraphBuilder`.

**Alternative considered:** thread expression results through `ArchitectureGraphEdge` for every graph build. Rejected — the graph is built once per `validate`/`graph`/`explain` invocation over the whole type universe; computing expression results for every edge regardless of whether `explain` is asked about it would add evaluation cost to `graph`/`validate` paths that never asked for it, violating "diagnostics must avoid dumping excessive codebase data" from the issue's required constraints.

### D4: Public CEL guide is a new page under Policy Authoring, not a new top-level nav section
`docs/policy-format/cel-expressions.md`, added to `mkdocs.yml` nav under **Policy Authoring** (after "Supported capabilities"), cross-linked from `docs/policy-format/supported-capabilities.md`, `docs/reference/yaml-schema.md`, `docs/policy-format/index.md`, and `README.md`. This matches how CEL currently surfaces only as sub-detail inside Policy Authoring/Contracts pages, and avoids over-elevating a cross-cutting feature into its own top-level nav section (see `docs-site` capability's existing nav structure).

### D5: AI CEL guidance expands the existing section rather than forking a new AI page
`docs/ai/policy-authoring-guide.md`'s existing `## CEL When Predicates` section (already linked from `docs/ai/capabilities.md`) is extended in place with anti-invention/anti-weakening/prefer-literal guidance and a link to the new public guide, rather than creating a parallel AI-only CEL page — satisfying the issue's explicit requirement that AI guidance "use the same public terminology... no hidden AI-only conventions."

### D6: Internal design docs stay internal; the public guide is written fresh from the public contract
`docs/internal/cel-policy-model.md` and `docs/internal/cel-engine-architecture.md` are mined for accurate facts (context schema, closed `when` locations, profile limits) but the public guide is authored as new prose targeting end users/AI agents, per `docs/internal/documentation-boundary.md`'s public/internal split. No internal file is linked from or moved into the public tree.

## Risks / Trade-offs

- **[Risk]** Re-evaluating a selector inside `explain` could drift from the result `validate` would produce for the same policy, if the two code paths diverge over time. → **Mitigation:** `explain`'s re-evaluation calls the exact same `ArchitectureContextSelectorMatcher.Matches(...)` overload `validate` uses, with no separate implementation.
- **[Risk]** Adding `when_expression` to JSON/SARIF could be mistaken for a schema-version bump by downstream consumers. → **Mitigation:** field is additive and omitted when absent, consistent with existing additive-field precedent; no `schemaVersion` field changes.
- **[Risk]** The new public guide could drift from the internal `cel-policy-model` spec as the profile evolves. → **Mitigation:** the guide explicitly states the profile version (v1) and links to this OpenSpec capability's scenarios as the source of truth for future revisions; profile version bumps are already a documented non-goal to solve generically here.
- **[Trade-off]** Scoping `explain` to only `when`-bearing selectors already on the resolved path (not a full "what-if" evaluator) means `explain` cannot answer "would this predicate match some other, unexplored type" — acceptable per the issue's explicit non-goal framing ("explain output can show expression-backed selector participation where the explain surface supports it," not a general predicate playground).

## Migration Plan

No data migration. Rollout is additive-field-only for JSON/SARIF and a new CLI behavior branch for `explain`; existing consumers of `validate`/`graph`/`explain` output are unaffected until they read the new fields. Docs ship in the same PR; GitHub Pages deployment remains the existing manual `workflow_dispatch` step, triggered by the user after merge — not part of this change's automated validation, only `make lint-docs` (`mkdocs build --strict`) is required to pass in CI.

## Open Questions

None blocking — the existing `cel-policy-model` spec, `ArchitectureContextSelector`, and `ArchitectureContextSelectorMatcher` already provide the exact data this change needs to surface; no upstream design decision is outstanding.
