## 1. Subject and dependency fact population

- [x] 1.1 Implement reflection-based population of `ArchitectureExpressionSubjectFacts` (identity, classification, type, and path facts per the closed catalog) from a candidate `System.Type` plus its already-resolved role/metadata, memoized once per type per analysis session.
- [x] 1.2 Implement population of `ArchitectureExpressionDependencyFacts` (`kind`, `viaMethodBody`, `sourceMemberName`, `targetMemberName`) from a discovered dependency edge.
- [x] 1.3 Compute `sourceDirectoryPrefixes` as every repository-relative ancestor directory of every `sourcePaths` entry, deduplicated.
- [x] 1.4 Add focused unit tests for fact population covering: abstract/sealed types, generic base types/interfaces normalized to open generic definitions where applicable, multiple source paths (partial classes), and types with no attributes/interfaces/base types.
- [x] 1.5 Gate fact construction so it only runs lazily, on first access, for a type actually evaluated against a `when`-bearing selector — never for policies with no `when` anywhere, and never pre-emptively for every candidate type.

## 2. Layer selector matcher integration

- [x] 2.1 Wire `ArchitectureLayerTypeMatcher.Matches` to evaluate `Selector.CompiledWhen` (when present) via `ArchitectureExpressionEvaluator` and the `subject` context from `ArchitectureExpressionContextFactory`, AND-combined with existing literal `role`/`metadata` matching.
- [x] 2.2 Preserve the literal-only fast path: a selector with no `when` must not construct a CEL context or call the evaluator.
- [x] 2.3 Propagate `ArchitectureExpressionEvaluationResult.IsError` as a blocking policy/configuration error distinct from an ordinary non-match; surface it through the same reporting path `ExpressionCompilationValidator` uses for compile errors.
- [x] 2.4 Add tests: `when` refines a role match; well-typed `false` is an ordinary non-match; evaluation failure fails the run; selector without `when` is behaviorally unchanged.

## 3. Contextual selector matcher integration

- [x] 3.1 Wire `ArchitectureContextSelectorMatcher.Matches` to evaluate `CompiledWhen` for `source`/`forbidden`/`allowed`/`exclude` selectors, using `ContextualSourceEnvironment` (`source`) for `source` selectors and `ContextualTargetEnvironment` (`source`, `target`, `dependency`) for `forbidden`/`allowed`/`exclude` selectors.
- [x] 3.2 AND-combine `when` with existing literal `role`/metadata-operator matching; preserve the literal-only fast path.
- [x] 3.3 Propagate evaluation errors from contextual selector matching as blocking policy/configuration errors, for both `strict_context_dependencies`/`audit_context_dependencies` and `strict_context_allow_only`/`audit_context_allow_only`, never suppressed by baseline or downgraded by audit mode.
- [x] 3.4 Add tests per the contextual-dependency-contracts and contextual-allow-only-contracts delta specs: matching, non-matching, invalid/evaluation-failure (strict and audit), selector-without-`when` unaffected.

## 4. Analysis session error propagation

- [x] 4.1 Extend `ArchitectureAnalysisSession` checking (`CheckContextDependencyContract`, `CheckContextAllowOnlyContract`, layer-selector-based checks) to collect expression evaluation errors as run-failing configuration errors, deduplicated by selector identity + candidate identity + message.
- [x] 4.2 Verify baseline processing never suppresses an expression evaluation error (add a regression test loading a baseline alongside a failing `when`).
- [x] 4.3 Verify strict and audit contracts both fail the run on an evaluation error (add regression tests for both).

## 5. Coverage integration

- [x] 5.1 Confirm `ArchitectureAnalysisSession.SemanticCoverage.GetSemanticStaleItems` becomes correctly `when`-aware automatically once the matchers from sections 2–3 are `when`-aware (it delegates to the matcher); add a regression test proving a selector matching via `when` alone is not reported stale.
- [x] 5.2 Add a test proving a broad `when` expression (matches most/all candidates) remains visible through ordinary coverage evidence rather than being silently treated as fully covered with no signal.
- [x] 5.3 Add a test proving an expression evaluation failure encountered during coverage classification is reported as a blocking error, not misclassified as `stale` or `uncovered`.

## 6. Diagnostics: expression provenance

- [x] 6.1 Extend context-dependency violation diagnostic payload/formatter (`ArchitectureDiagnosticFormatter.Context.cs` and related model types) to carry the participating `when` expression's source text and YAML location, for both human and JSON output.
- [x] 6.2 Extend context-allow-only violation diagnostic payload/formatter the same way.
- [x] 6.3 Extend layer-selector configuration/empty-match diagnostics to identify `when` participation and source text per the `semantic-classification-model` delta.
- [x] 6.4 Add a dedicated expression-evaluation-error diagnostic path (reusing the existing configuration/validation error reporting pattern) carrying: selector/contract identity, YAML location, expression source text, and error message.
- [x] 6.5 Add JSON determinism tests proving repeated runs over the same fixtures produce byte-identical (or field-order-stable) diagnostic output for `when`-bearing selectors/contracts.

## 7. Integration fixtures

- [x] 7.1 Add matching/non-matching/stale/ambiguous fixtures for layer selectors with `when`.
- [x] 7.2 Add matching/non-matching/invalid(evaluation-failure)/strict/audit fixtures for contextual dependency contracts with `when`.
- [x] 7.3 Add the equivalent fixture set for contextual allow-only contracts with `when`.
- [x] 7.4 Add a modular-monolith Sales/Inventory/SharedKernel example policy exercising cross-context `when` comparison (`target.metadataText[...] == source.metadataText[...]`).
- [x] 7.5 Add a Unity/client namespace-convention example where a literal namespace-derived role is refined by a `when` expression.
- [x] 7.6 Follow the repo's existing fixture-file convention (`<Area>TestFixtures.cs` alongside `<Area>Tests.cs` in `tests/ArchLinterNet.Core.Tests/`, e.g. `CelSelectorTestFixtures.cs`, `CelContextualContractTestFixtures.cs`).

## 8. Documentation

- [x] 8.1 Update the public schema reference to document that `when` is now live-evaluated (not just accepted) for the seven closed locations.
- [x] 8.2 Update policy-authoring guidance (human and AI-agent-facing) with the modular-monolith and Unity/client worked examples and the negative examples already required by `cel-policy-model` (stale map access, broad `true` predicates, policy-weakening exclusions).
- [x] 8.3 Document the fail-closed evaluation-error behavior and that it is not baseline-suppressible.

## 9. Verification

- [x] 9.1 Run `rtk make restore` if any project references changed.
- [x] 9.2 Run `rtk make fmt`.
- [x] 9.3 Run `rtk make lint` (includes `lint-architecture`, `lint-code-size`, `lint-dotnet-format`) and resolve any findings, including file-size warnings/errors for touched files.
- [x] 9.4 Run `rtk make test` and confirm all suites pass, including the new CEL selector/contextual fixtures.
- [x] 9.5 Run `rtk make acceptance` and confirm it passes fully.
- [x] 9.6 Run `openspec validate --all` and confirm every spec still passes before archiving.
