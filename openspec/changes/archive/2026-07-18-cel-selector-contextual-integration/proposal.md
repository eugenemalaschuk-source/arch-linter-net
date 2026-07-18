## Why

Issues #111, #112, and #163 shipped the YAML shape, the compile-time validation, and the typed CEL context/evaluator building blocks for `when` predicates on semantic layer selectors and contextual dependency/allow-only contracts — but nothing in the matching/checking pipeline actually evaluates a compiled `when` yet. Every `Selector.CompiledWhen`/`ArchitectureContextSelector.CompiledWhen` field is populated at load time and then ignored by `ArchitectureLayerTypeMatcher` and `ArchitectureContextSelectorMatcher`. Until this gap closes, `when` is dead weight: it compiles, but has zero effect on which types match a layer or a contextual contract, and none of the explainability/coverage guarantees the `cel-policy-model` spec already promises (expression provenance in explain output, stale-selector awareness of `when`, fail-closed evaluation errors) exist in the running product.

## What Changes

- Populate `ArchitectureExpressionSubjectFacts`/`ArchitectureExpressionDependencyFacts` from real candidate types and dependency edges via reflection/source-fact lookups, matching the closed catalog in `cel-policy-model` exactly (no new fields, no numeric metadata).
- Wire `ArchitectureLayerTypeMatcher.Matches` to evaluate a selector's `CompiledWhen` (when present) against the `subject` context, combined with existing literal `role`/`metadata` matching. Selectors with no `when` remain on the existing literal-only fast path — zero behavior change for existing policies.
- Wire `ArchitectureContextSelectorMatcher.Matches` the same way for `source`/`forbidden`/`allowed`/`exclude` contextual selectors, using the `source`/`target`/`dependency` context shapes.
- Propagate CEL evaluation errors (e.g. missing map key, runtime evaluation failure) as run-failing policy/configuration errors — never as an ordinary violation, never treated as a non-match, never suppressed by baseline, and never downgraded by audit-mode's normally-non-blocking behavior.
- Extend semantic coverage's existing stale-selector detection so a selector with `when` is reported stale only when the combined literal+expression match set is empty.
- Extend context-dependency/allow-only diagnostics and layer-selector configuration/coverage diagnostics (human and JSON) to name the `when` expression source text and YAML location that participated in a match, violation, or evaluation failure. (The `explain` CLI verb's graph-path BFS is unaffected — it reports contract IDs on edges already, and does not require expression-evidence plumbing into the dependency graph edge model for this wave; that remains available as separately-scoped follow-up work.)
- Add integration fixtures/tests: matching, non-matching, invalid, ambiguous, stale, strict, audit; a modular-monolith Sales/Inventory/SharedKernel example; a Unity/client namespace-convention example where a literal namespace role is refined by `when`; fail-closed evaluation-error tests; determinism tests for JSON output.
- Update the public schema reference, policy-authoring docs (human and AI-agent guidance), and examples to describe the now-live selector/contextual `when` behavior.

No **BREAKING** changes: `when` was previously accepted (compiled) but functionally inert; making it functional is additive from the policy author's perspective, and every existing literal-only policy is unaffected by construction (the literal-only fast path is preserved).

## Capabilities

### New Capabilities

(none — this change activates and reports on behavior already specified by `cel-policy-model`, `semantic-classification-model`, `contextual-dependency-contracts`, `contextual-allow-only-contracts`, and `architecture-coverage-model`; it does not introduce a new capability domain)

### Modified Capabilities

- `cel-policy-model`: the "Reporting preserves provenance and expression explainability" requirement moves from describing "future explainable output for implemented predicates" to describing the now-implemented diagnostics-level behavior; the "Compatibility is preserved and runtime rollout remains fail-closed" requirement's pre-implementation-rejects-`when` scenario is replaced with a scenario describing live fail-closed evaluation.
- `semantic-classification-model`: selector matching (`layers.<name>.selector`) now evaluates `when` in addition to literal `role`/`metadata`, and the stale-selector scenario accounts for expression-augmented match sets; layer selector diagnostics gain expression provenance.
- `contextual-dependency-contracts`: `source`/`forbidden`/`exclude` selector matching now evaluates `when` in addition to literal `role`/`metadata` constraints; evaluation errors fail the run; diagnostics gain expression provenance.
- `contextual-allow-only-contracts`: `source`/`allowed`/`exclude` selector matching now evaluates `when` the same way; diagnostics gain expression provenance.
- `architecture-coverage-model`: stale-selector classification for selector-backed layers and contextual semantic references accounts for `when`-augmented matching rather than literal criteria alone.

## Impact

- Code: `src/ArchLinterNet.Core/Execution/ArchitectureLayerTypeMatcher.cs`, `src/ArchLinterNet.Core/Execution/ArchitectureContextSelectorMatcher.cs`, `src/ArchLinterNet.Core/Execution/Expressions/*` (fact population), `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession*.cs` (error propagation, coverage), `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticFormatter.Context.cs`, related diagnostic/payload model types. `src/ArchLinterNet.Core/Graph/ArchitectureExplainApplicationService.cs` is out of scope for this change.
- Tests: `tests/ArchLinterNet.Core.Tests/` (new selector/contextual CEL fixtures and tests), possibly `tests/ArchLinterNet.Cli.Tests/` if JSON output plumbing needs verification at the CLI boundary.
- Docs: schema reference, policy-authoring guidance (human + AI agent), examples under `docs/`.
- No changes to `ArchLinterNet.CEL` (engine stays untouched, consumed only through its existing public API) or to `ArchLinterNet.Testing`.
- No new YAML `when` locations; the seven-location allow-list in `ExpressionCompilationValidator` is unchanged.
