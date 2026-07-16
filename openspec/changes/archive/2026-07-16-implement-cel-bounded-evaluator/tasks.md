## 1. Evaluator runtime

- [x] 1.1 Add an internal evaluator/runtime state that walks `CelBoundExpression` and returns
  `CelEvaluationResult` with deterministic iteration and cost accounting.
- [x] 1.2 Wire `CelCompiledPredicate.Evaluate(...)` and `CelCompiledExpression.Evaluate(...)` to
  the evaluator while preserving null/ceiling argument guards.
- [x] 1.3 Implement runtime semantics for literals, identifiers, unary `!`, equality, ordering,
  `in`, object member access, list/map indexing, and built-in invocation via
  `CelBuiltinFunctionInvoker`.
- [x] 1.4 Implement logical `&&` / `||` runtime behavior so determining operands absorb errors and
  non-determining operands propagate them per the Profile v1 spec.

## 2. Diagnostics and contracts

- [x] 2.1 Add internal evaluation-diagnostic helpers for `BudgetExceeded`, `EvaluationFailure`,
  and incompatible-context `SchemaMismatch` results with stable parameters and source spans.
- [x] 2.2 Ensure evaluation rejects incompatible `CelEvaluationContext.Schema` values
  deterministically rather than producing incorrect results or CLR exceptions.
- [x] 2.3 Update `docs/internal/cel-engine-architecture.md` to document the shipped evaluator seam,
  budget model, and future backend direction after #328.

## 3. Tests and validation

- [x] 3.1 Add focused evaluator tests covering successful predicates/expressions, logical
  error-absorption cases, indexing failures, `in`, member access, and built-in calls.
- [x] 3.2 Add adversarial tests for `MaxIterations`, `MaxCostUnits`, warm evaluation
  reuse/no-compile-work, and concurrent reuse across independent contexts.
- [x] 3.3 Update existing stub-based tests to assert shipped evaluator behavior, then run
  `rtk make fmt`, `rtk make acceptance`, and `rtk openspec validate --all --strict`.
- [x] 3.4 Synchronize the canonical `cel-profile-v1` spec to the shipped behavior, archive the
  change, and re-run `rtk openspec validate --all`.
