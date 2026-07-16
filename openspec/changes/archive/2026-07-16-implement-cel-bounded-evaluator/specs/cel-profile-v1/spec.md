## ADDED Requirements

### Requirement: Bounded evaluator runtime implementation for Profile v1

`CelCompiledPredicate.Evaluate(...)` and `CelCompiledExpression.Evaluate(...)` SHALL execute the
 immutable bound-expression tree produced by the binder through one internal bounded evaluator rather
 than throwing `NotImplementedException`. The evaluator SHALL remain internal-only: no public type,
 member, or API surface may expose evaluator classes, delegates, execution plans, bytecode, or
 other backend-specific artifacts.

Evaluation SHALL be deterministic for the same compiled program, `CelEvaluationContext`, profile,
 and `CelEvaluationLimits`. Repeated evaluation of one compiled program SHALL perform no parsing,
 binding, or type-checking. One compiled program SHALL be concurrently reusable across independent
 evaluation contexts with no shared mutable evaluator state.

The evaluator SHALL enforce both evaluation budgets on every public evaluation path:

- one iteration unit per visited bound node against `MaxIterations`;
- one additional abstract-cost charge per built-in call using
  `CelBuiltinFunctionInvoker.ComputeCost(...)` against `MaxCostUnits`, plus charges for
  collection membership, map/object lookup, string comparison (including `ObjectTypeId`), and
  recursive structural comparison.

Exceeding either budget SHALL produce a failed `CelEvaluationResult` with a `BudgetExceeded`
 diagnostic carrying `limitName`, `observedValue`, and `profileId` parameters. The diagnostic
 SHALL carry the source span of the bound node whose visit or built-in charge exceeded the budget.

Supplying a `CelEvaluationContext` built for a different full compilation schema identity than the
 compiled program's `Schema` (including the object-schema catalog) SHALL NOT produce a CLR
 member/indexing exception or a wrong result. Evaluation SHALL fail deterministically with a
 `SchemaMismatch` diagnostic carrying logical `schemaId` and `expectedSchemaId`, structural
 `schemaIdentity` and `expectedSchemaIdentity`, and `profileId`. The context SHALL build its
 immutable variable-name lookup once and evaluation SHALL reuse it without rebuilding all
 assignments per call.

Normal runtime data failures SHALL surface as failed `CelEvaluationResult` values with code
 `EvaluationFailure`, never as uncaught CLR exceptions:

- map indexing on a missing key;
- list indexing with a negative or out-of-range index.

For those failures, the diagnostic SHALL include the source span of the failing index expression
 and structured parameters identifying the failure kind (`missingKey` or `invalidIndex`) plus the
 relevant observed key/index.

The evaluator SHALL support the full currently shipped bound-node set:

- literals and identifier lookup;
- unary `!`;
- binary logical, equality, ordering, and `in` operators;
- object member access;
- list/map indexing;
- closed built-in-function invocation through `CelBuiltinFunctionInvoker`.

#### Scenario: Successful predicate evaluation returns a boolean result

- **WHEN** a successfully compiled predicate is evaluated against a compatible context within the
  configured budgets
- **THEN** `CelEvaluationResult.IsSuccess` is `true`
- **AND** `CelEvaluationResult.AsBool()` returns the predicate result

#### Scenario: Successful general expression evaluation returns a typed CEL value

- **WHEN** a successfully compiled non-predicate expression is evaluated against a compatible
  context within the configured budgets
- **THEN** `CelEvaluationResult.IsSuccess` is `true`
- **AND** `CelEvaluationResult.Value` is the expected `CelValue`

#### Scenario: Different-schema evaluation context fails with SchemaMismatch

- **WHEN** a compiled program is evaluated with a `CelEvaluationContext` whose schema does not
  match the program's captured `Schema`
- **THEN** `CelEvaluationResult.IsSuccess` is `false`
- **AND** `CelEvaluationResult.Diagnostics` contains a `SchemaMismatch` diagnostic with
  `schemaId` and `expectedSchemaId` parameters

#### Scenario: Evaluation step budget exhaustion returns BudgetExceeded

- **WHEN** evaluation would visit more bound nodes than `limits.MaxIterations` allows
- **THEN** `CelEvaluationResult.IsSuccess` is `false`
- **AND** the failure diagnostic code is `BudgetExceeded`
- **AND** `diagnostic.Parameters["limitName"]` equals `"MaxIterations"`

#### Scenario: Built-in cost budget exhaustion returns BudgetExceeded

- **WHEN** evaluation stays within the step budget but a built-in call would exceed
  `limits.MaxCostUnits`
- **THEN** `CelEvaluationResult.IsSuccess` is `false`
- **AND** the failure diagnostic code is `BudgetExceeded`
- **AND** `diagnostic.Parameters["limitName"]` equals `"MaxCostUnits"`

#### Scenario: Concurrent reuse is side-effect free

- **WHEN** one compiled program is evaluated concurrently against multiple independent evaluation
  contexts
- **THEN** each call completes with the same result it would produce in isolation
- **AND** no shared mutable evaluator state is required

## MODIFIED Requirements

### Requirement: Immutable compiled programs are thread-safe and concurrently reusable

`CelCompiledPredicate` and `CelCompiledExpression` SHALL be immutable, thread-safe, side-effect-free, and concurrently reusable. They SHALL carry `Profile`, `Schema`, `CompilationKey`, and `CompilationLimits` as readable properties. They SHALL NOT expose parser, binder, or evaluator internals. They SHALL NOT be implicitly convertible to delegates or expression trees. Repeated evaluation SHALL perform no parsing, binding, or type-checking.

#### Scenario: Compiled predicate exposes profile and key

- **WHEN** a successful `CelCompilationResult<CelCompiledPredicate>` is obtained
- **THEN** `result.Program.Profile` equals the environment's profile
- **AND** `result.Program.CompilationKey` is a non-null structural cache-identity value

#### Scenario: Compiled predicate evaluates to a structured result

- **WHEN** a consumer calls `program.Evaluate(context, limits)` on a successfully compiled predicate
- **THEN** a `CelEvaluationResult` is returned with typed boolean access via `AsBool()`
- **AND** no delegate is exposed as an evaluation path
