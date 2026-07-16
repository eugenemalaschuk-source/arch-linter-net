## Context

`ArchLinterNet.CEL` already has the public API shape, the tokenizer/parser, the binder, the closed
built-in function catalog, and the immutable input/value model. The remaining gap is evaluation:
successful compilation produces `CelCompiledPredicate` / `CelCompiledExpression`, but both public
entry points still throw `NotImplementedException`.

Issue #328 is cross-cutting rather than a local class edit. It changes the public behavior of
compiled programs, consumes the internal bound-tree model, needs deterministic runtime diagnostics
and budgets, and must keep the evaluator behind a non-public seam so #329 and future backend work
can optimize execution without changing the public contract.

## Goals / Non-Goals

**Goals:**

- Ship a deterministic internal evaluator for the current bound-node set.
- Return structured `CelEvaluationResult` failures for runtime data problems and exhausted budgets.
- Enforce both evaluation budgets on every public evaluation path.
- Preserve compile-once/evaluate-many behavior: no parse, bind, or type-check work during
  evaluation.
- Keep the evaluator internal and reusable from both compiled-program public entry points.
- Document the delivered seam in the internal CEL architecture blueprint.

**Non-Goals:**

- Adding new CEL language features, new value kinds, regex, macros, nullability, or user-defined
  functions.
- Introducing a public evaluator API, delegate conversion, bytecode format, or backend plugin
  system.
- Changing builder-time `Set()` validation behavior beyond what evaluation needs to consume.
- Implementing #329 performance/planner work beyond the internal seam needed to support it later.

## Decisions

### 1. One internal evaluator entry point serves both compiled program types

`CelCompiledPredicate` and `CelCompiledExpression` will both delegate to one internal evaluator
entry point that accepts the bound expression, evaluation context, limits, and expected root type.
This keeps runtime semantics centralized and prevents drift between the predicate and general
expression paths.

Alternative considered: separate predicate and expression evaluators. Rejected because the only
meaningful difference is the caller's expectation that the root result is `Bool`, which the binder
has already enforced for predicates.

### 2. Deterministic left-to-right traversal is the concrete v1 execution strategy

The spec fixes the observable result of `&&` / `||`, not the operand order. The implementation will
evaluate left first, then evaluate right only when the result cannot already be determined by the
left operand. This is deterministic, cheap to reason about, and still conforms to the spec's
observable-result contract.

Alternative considered: always evaluate both operands and reconcile later. Rejected because it
spends unnecessary budget, makes step accounting noisier, and offers no user-visible benefit in
Profile v1.

### 3. Runtime state is explicit and per-call

The evaluator will keep a small internal runtime state object per evaluation call:

- remaining / consumed iteration count;
- remaining / consumed abstract cost units;
- the schema-ordered variable bindings from `CelEvaluationContext`.

Every bound-node visit consumes one iteration unit. Built-in calls additionally consume the
operation-specific `ComputeCost(...)` value. This keeps budget enforcement local, deterministic, and
testable.

Alternative considered: hide counters in static helpers or implicit recursion-local state. Rejected
because it weakens thread-safety reasoning and makes concurrent-reuse testing harder.

### 4. Runtime failures become structured diagnostics, not thrown exceptions

Normal runtime problems are represented as failed `CelEvaluationResult` values:

- missing map key;
- out-of-range list index;
- exhausted `MaxIterations`;
- exhausted `MaxCostUnits`;
- incompatible evaluation context schema.

Each failure carries a stable `CelDiagnosticCode`, source span when available, and structured
parameters (`limitName`, `observedValue`, `schemaId`, `expectedSchemaId`, `identifier`, or index/key
details as applicable). Exceptions remain reserved for programmer misuse already covered by the
public API contract, such as null arguments or per-call limits above the compiled ceiling.

Alternative considered: let invalid index or missing key exceptions bubble from collection access.
Rejected because Profile v1 requires deterministic, machine-readable evaluation failures.

### 5. Runtime dispatch stays on bound-node types plus built-in operation ids

The evaluator will switch over the current immutable `CelBoundNode` hierarchy and invoke
`CelBuiltinFunctionInvoker` only for `CelBoundCall`, using `boundCall.Overload.OperationId`. No new
abstraction layer, service registry, or reflection-based dispatch is needed.

Alternative considered: add a visitor interface or public evaluator abstraction now. Rejected
because the current node set is small, internal, and closed; a new abstraction would add surface
area without solving a concrete issue requirement.

## Risks / Trade-offs

- [Budget accounting undercounts real work] -> Keep iteration and cost charging explicit, use the
  existing per-operation cost model, and add adversarial tests for repeated operations and large
  inputs.
- [Spec drift between shipped behavior and OpenSpec] -> Update the delta spec and sync/archive the
  change only after the implementation and tests reflect the actual runtime behavior.
- [Context compatibility bugs become silent wrong results] -> Fail fast when the supplied
  `CelEvaluationContext.Schema` does not match the compiled program's `Schema` reference/identity.
- [Evaluator grows into a de facto public contract] -> Keep all runtime helpers internal and route
  public evaluation exclusively through the existing compiled-program types.
