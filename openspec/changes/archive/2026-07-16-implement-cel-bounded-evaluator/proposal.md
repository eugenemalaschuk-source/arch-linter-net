## Why

`ArchLinterNet.CEL` can now parse, bind, type-check, and catalog built-in calls, but compiled
programs still stop at an internal bound tree and `Evaluate(...)` throws `NotImplementedException`.
Issue #328 closes that gap by shipping the bounded runtime semantics that make Profile v1 usable as
an actual compile-once/evaluate-many CEL engine while preserving the project's no-host-escape,
deterministic-budget, backend-neutral architecture.

## What Changes

- Add an internal bounded evaluator that walks the immutable bound plan and returns
  `CelEvaluationResult` instead of throwing for normal runtime failures.
- Wire `CelCompiledPredicate.Evaluate(...)` and `CelCompiledExpression.Evaluate(...)` to the
  evaluator while preserving the existing per-call ceiling checks on `CelEvaluationLimits`.
- Implement Profile v1 runtime semantics for unary/binary operators, deep equality, ordering,
  `in`, member access, list/map indexing, and built-in invocation through
  `CelBuiltinFunctionInvoker`.
- Enforce deterministic evaluation budgets (`MaxIterations`, `MaxCostUnits`) and emit structured
  `BudgetExceeded` or `EvaluationFailure` diagnostics with stable parameters and source spans.
- Reject incompatible evaluation contexts deterministically and document the shipped evaluator seam
  plus future backend directions in `docs/internal/cel-engine-architecture.md`.
- Add focused CEL evaluator tests, adversarial budget-limit tests, and concurrent reuse coverage.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `cel-profile-v1`: compiled programs gain real bounded evaluation behavior, structured
  evaluation-time diagnostics, and shipped runtime semantics instead of evaluator stubs.

## Impact

- `src/ArchLinterNet.CEL/Compilation/`: `CelCompiledPredicate` and `CelCompiledExpression`
  transition from stubs to evaluator-backed execution.
- `src/ArchLinterNet.CEL/Evaluation/`: new internal evaluator/runtime helpers and diagnostic
  creation logic.
- `src/ArchLinterNet.CEL/Binding/`: bound nodes and built-in invoker are consumed by the runtime.
- `tests/ArchLinterNet.CEL.Tests/`: new evaluator semantics, budget, and concurrency coverage;
  existing stub-based tests will be updated.
- `openspec/specs/cel-profile-v1/spec.md` and `docs/internal/cel-engine-architecture.md`:
  synchronized to the shipped evaluator behavior and architecture seam.
