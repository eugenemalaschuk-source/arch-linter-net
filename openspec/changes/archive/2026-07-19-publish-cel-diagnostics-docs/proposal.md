## Why

CEL `when` predicates are wired into layer selectors, contextual dependency/allow-only contracts, and layout conventions (#164, #170), and #169 shipped real fixtures for them — but the feature is not yet usable end-to-end. When a `when` predicate causes (or fails to cause) a match, nothing in the CLI's JSON, SARIF, or `explain` output says which expression ran or why it matched; `openspec/specs/cel-policy-model/spec.md` explicitly deferred both of those as "separately-scoped follow-up work." There is also no public documentation: the only CEL policy-model content lives under `docs/internal/`, which is excluded from the GitHub Pages build, so neither human authors nor AI agents have a public reference for the supported syntax, variables, or diagnostics. Issue #165 closes both gaps and is a hard prerequisite for calling CEL support "product-complete."

## What Changes

- Add CEL expression provenance (expression source text + match/no-match/error result) to human-readable and JSON violation/diagnostic output for context-dependency, context-allow-only, and layout-convention findings.
- Add the same expression provenance to SARIF `relatedLocations`/messages for the same diagnostic kinds.
- Extend the `explain` CLI verb so that when the explained path or participation involves a selector or contextual contract carrying a `when` predicate, the output shows the expression text and its match result.
- Add a new public GitHub Pages guide (`docs/policy-format/cel-expressions.md`) covering: CEL overview and AI-first rationale, a link to the official CEL spec, the ArchLinter CEL Profile v1 support matrix (types/operators/functions/limits/unsupported features), a typed authoring reference for every expression location, worked examples reusing #169 fixtures, diagnostics/troubleshooting, and AI authoring guidance — all without exposing internal engine design.
- Wire the new guide into `mkdocs.yml` navigation, `docs/policy-format/index.md`, `docs/policy-format/supported-capabilities.md` (reframed from pure limitation to supported-but-scoped), `docs/reference/yaml-schema.md` cross-links, and `README.md`'s Documentation section.
- Expand the existing "CEL When Predicates" section in `docs/ai/policy-authoring-guide.md` with explicit AI guidance: don't invent operators/syntax, don't weaken policy to pass generated code, prefer literal selectors when clearer, and link to the same public guide (no hidden AI-only conventions).
- Update `openspec/specs/cel-policy-model/spec.md`'s deferred-work language now that explain/SARIF provenance are delivered.

None of this changes the CEL parser/binder/evaluator diagnostic model itself (`CelDiagnostic`, `CelDiagnosticCode`, category-specific diagnostics classes) — that layer already produces actionable, human-readable messages with identifiers and source spans; the gap is entirely in surfacing already-available data (the `When` source text, `WhenLocation`, and evaluation result already stored on `ArchitectureContextSelector`) through CLI-facing output, plus documenting the feature publicly.

## Capabilities

### New Capabilities
- `cel-policy-guide`: the public GitHub Pages CEL guide's required content — overview, AI-first rationale, profile v1 support matrix, typed authoring reference, examples, diagnostics/troubleshooting, and AI authoring guidance, kept within the public/internal documentation boundary.

### Modified Capabilities
- `violation-reporting`: human-readable and CI JSON output for context-dependency, context-allow-only, and layout-convention diagnostics gains expression source text and match/error result, additively.
- `sarif-diagnostics-output`: SARIF results for the same diagnostic kinds gain expression provenance in related locations/messages, additively.
- `explain-command`: the `explain` verb gains CEL `when` predicate awareness for explained paths/participation that involve an expression-bearing selector or contract.
- `docs-site`: the required documentation pages list and MkDocs navigation gain the new CEL guide page; README links updated.
- `ai-policy-authoring`: AI-facing CEL guidance is expanded with explicit anti-invention, anti-weakening, and literal-selector-preference instructions, cross-linked to the public guide.
- `cel-policy-model`: the "not required in this wave" carve-outs for `explain` and SARIF expression provenance are removed/updated to reflect delivery.

## Impact

- Code: `src/ArchLinterNet.Cli/Commands/Validate/ValidateCommandHandler.cs` (JSON/SARIF formatters), `src/ArchLinterNet.Cli/Commands/Explain/*`, `ArchLinterNet.Core.Graph.ArchitectureExplainApplicationService`, and whatever shared diagnostic/violation-reporting formatter types carry the new provenance fields.
- Docs: new `docs/policy-format/cel-expressions.md`; edits to `mkdocs.yml`, `docs/policy-format/index.md`, `docs/policy-format/supported-capabilities.md`, `docs/reference/yaml-schema.md`, `docs/ai/policy-authoring-guide.md`, `README.md`.
- Tests: `tests/ArchLinterNet.Cli.Tests/*` (JSON/SARIF/explain assertions using existing #169 fixtures), `tests/ArchLinterNet.Core.Tests/*` where applicable.
- No changes to `ArchLinterNet.CEL` parser/binder/evaluator internals, YAML schema shape, or policy-load semantics.
- GitHub Pages deployment itself remains a manual, human-triggered `workflow_dispatch` step in `release-nuget.yml` — out of scope to trigger; in scope to ensure `make lint-docs` (`mkdocs build --strict`) passes with the new page wired in.
