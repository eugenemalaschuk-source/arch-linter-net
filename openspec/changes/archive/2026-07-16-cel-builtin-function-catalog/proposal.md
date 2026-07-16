## Why

`cel-profile-v1` (#324) normatively fixes the closed Profile v1 built-in function table
(`startsWith`/`endsWith`/`contains`/`size`/`containsKey`), including receiver/argument/result types
and the Unicode code-point counting contract for `String.size()`. #326 (#337) implemented the
binder, which resolves every call against `ArchLinterNet.CEL.Binding.CelFunctionCatalog` — the
closed *declaration* metadata — but the catalog carries no execution behavior: nothing in the
codebase can yet compute the *result* of calling `startsWith`, `size`, or `containsKey` against an
actual `CelValue`. #327 adds that missing piece: internal pure implementations of each catalog
overload, invocable by a stable internal operation identifier rather than by re-dispatching on the
function name string, so the tree-walking evaluator (#328) has a single controlled lookup path
(`CelFunctionOverload.OperationId` → `CelBuiltinFunctionInvoker.Invoke`) instead of needing its own
per-function switch statement. This keeps the three concerns the issue calls out — declaration
metadata, overload binding, and bounded evaluation implementation — in three separate, individually
testable places, and gives a future standard-function addition one reviewed inclusion point (a new
enum member, a new catalog row, a new `case` in the invoker) rather than scattered conditionals
across parser/binder/evaluator/docs.

This change does not build the general expression evaluator (#328) — no `CelBoundNode` tree-walker,
no `&&`/`||` short-circuit semantics, no map/list-index runtime failure handling, no wiring into
`CelCompiledPredicate.Evaluate`/`CelCompiledExpression.Evaluate` (both remain `NotImplementedException`
stubs after this change, exactly as they were after #326). It only makes the seven catalog overloads'
result computable in isolation, tested directly against `CelValue` inputs, ready for #328 to call.

## What Changes

- New `CelFunctionOperationId` enum (`ArchLinterNet.CEL.Binding`, internal) — one stable member per
  catalog overload (`StartsWith`, `EndsWith`, `Contains`, `SizeString`, `SizeList`, `SizeMap`,
  `ContainsKey`), replacing function-name-string dispatch as the execution lookup key.
- `CelFunctionOverload` gains an `OperationId` property, populated once per row in
  `CelFunctionCatalog`. `CelBoundCall.Overload` (already produced by the binder) therefore already
  carries the operation identity the evaluator will need — no binder change required beyond the one
  new catalog field.
- New `CelBuiltinFunctionInvoker` (`ArchLinterNet.CEL.Binding`, internal, static, no mutable state):
  `Invoke(CelFunctionOperationId operationId, CelValue? receiver, IReadOnlyList<CelValue> arguments)
  -> CelValue`. One pure, side-effect-free implementation per operation id:
  - `startsWith`/`endsWith`/`contains` — ordinal (code-unit-sequence) string matching.
  - `size` (String) — Unicode **code-point** count via `System.Text.Rune` enumeration, never
    `string.Length`, matching the spec's surrogate-pair and combining-sequence scenarios.
  - `size` (List/Map) — element/entry count.
  - `containsKey` — map key presence; **never fails** on a missing key (returns `false`), matching
    the spec's logical-operator/error-semantics requirement, which this change's tests cover for
    the function's own return value (the requirement's *evaluator-level* absorption/propagation
    behavior around `&&`/`||` remains #328 scope).
  All seven catalog overloads are total given a binder-guaranteed-correct receiver/argument shape —
  none can produce a runtime evaluation failure, so `Invoke` returns `CelValue` directly with no
  diagnostic/failure channel.
- `CelBuiltinFunctionInvoker` also gains `ComputeCost(CelFunctionOperationId, CelValue? receiver,
  IReadOnlyList<CelValue> arguments) -> long`, next to `Invoke` with one `case` per operation id, so
  a future evaluator (#328) charges each call's true, input-size-proportional cost against
  `CelEvaluationLimits.MaxCostUnits` instead of treating every built-in call as a fixed unit cost (an
  unbounded-relative-to-input-size gap the initial version of this change left open) or having to
  build its own second per-operation switch. The model is a linear approximation — a fixed floor plus
  the length of every string operand an operation scans — not each operation's exact worst-case
  complexity; `size`/`containsKey` on `List`/`Map` cost only the floor (O(1) count/lookup).
- `CelFunctionCatalog` gains a public-to-the-namespace `All` enumeration (already-internal type) so
  a security/conformance test can assert the catalog is exactly these seven overloads and no more.
- New `tests/ArchLinterNet.CEL.Tests/CelBuiltinFunctionInvokerTests.cs` — one positive test per
  overload plus the documented edge cases (empty string/list/map, BMP vs. surrogate-pair vs.
  combining-sequence `size()`, missing-key `containsKey`, substring/prefix/suffix boundary cases), a
  full-fidelity catalog-completeness test (function name, receiver kind, every argument kind in
  order, result type, AND operation id — strong enough to catch e.g. a `StartsWith`/`EndsWith`
  `OperationId` swap, which a name/receiver/arity-only comparison would miss), a
  per-overload round-trip invocation test, and `ComputeCost` coverage (proportional-to-input-size
  and constant-for-O(1)-operations cases).
- `docs/internal/cel-engine-architecture.md` updated: the pipeline diagram's "Bounded Evaluator"
  row was written before task numbering settled and currently mislabels #327 as the tree-walking
  evaluator; this change corrects it to describe #327's actual shipped scope (pure per-overload
  implementations keyed by operation id) and adds a "Built-in function execution" component
  ownership row, consistent with the "Function catalog" extension-direction section's existing
  description of the seam.
- Every remaining stale `#327` reference that meant "the evaluator" under the old task numbering
  (`CelCompiledPredicate`/`CelCompiledExpression` XML docs and `NotImplementedException` messages;
  four spots in the main `cel-profile-v1` spec) is corrected to `#328`, and the one spec reference
  that actually describes *this* change's own shipped behavior (Unicode code-point counting) is
  reattributed from "the evaluator" to "the built-in function invoker."

## Capabilities

### Modified Capabilities

- `cel-profile-v1`: Adds ADDED requirements pinning the built-in function catalog's *execution*
  implementation scope (operation-identifier seam, per-overload pure implementation contract,
  Unicode code-point counting mechanism, totality/no-failure-channel guarantee) that the existing
  spec's built-in-function-table requirement left as an implementation detail. No existing
  requirement, scenario, or public API shape changes — the function table, types, and Unicode
  counting contract were already normatively specified by #324.

## Impact

- **`src/ArchLinterNet.CEL/Binding/`**: new `CelFunctionOperationId.cs`, new
  `CelBuiltinFunctionInvoker.cs` (`Invoke` + `ComputeCost`); `CelFunctionOverload.cs` and
  `CelFunctionCatalog.cs` gain the `OperationId` field/values.
- **`src/ArchLinterNet.CEL/Compilation/CelCompiledPredicate.cs` and `CelCompiledExpression.cs`**:
  stale `#327` references (from before task numbering settled) corrected to `#328`.
- **`tests/ArchLinterNet.CEL.Tests/`**: new `CelBuiltinFunctionInvokerTests.cs`.
- **`docs/internal/cel-engine-architecture.md`**: pipeline row and component-ownership table
  corrected/extended.
- **Downstream #328 (bounded evaluator)**: will call `CelBuiltinFunctionInvoker.Invoke` from its
  `CelBoundCall` case using `boundCall.Overload.OperationId`; no public API is added for it to
  depend on, and no evaluator wiring happens in this change.
- No public API surface change — `ArchLinterNet.CEL.Binding` remains internal;
  `CelCompiledPredicate.Evaluate`/`CelCompiledExpression.Evaluate` remain `NotImplementedException`
  stubs.
