## 1. Operation identity

- [x] 1.1 Add `src/ArchLinterNet.CEL/Binding/CelFunctionOperationId.cs` — internal enum with one member per catalog overload: `StartsWith`, `EndsWith`, `Contains`, `SizeString`, `SizeList`, `SizeMap`, `ContainsKey`.
- [x] 1.2 Add `OperationId` property to `CelFunctionOverload` (new constructor parameter); update its XML doc.
- [x] 1.3 Update `CelFunctionCatalog`'s seven overload declarations to pass the matching `CelFunctionOperationId`.

## 2. Built-in function invoker

- [x] 2.1 Add `src/ArchLinterNet.CEL/Binding/CelBuiltinFunctionInvoker.cs` — internal static class, `Invoke(CelFunctionOperationId, CelValue? receiver, IReadOnlyList<CelValue> arguments) -> CelValue`.
- [x] 2.2 Implement `StartsWith`/`EndsWith`/`Contains` via `string.StartsWith`/`EndsWith`/`Contains` with `StringComparison.Ordinal`.
- [x] 2.3 Implement `SizeString` via `receiver.AsString().EnumerateRunes().Count()` (Unicode code points, not `string.Length`).
- [x] 2.4 Implement `SizeList`/`SizeMap` via `.Count`.
- [x] 2.5 Implement `ContainsKey` via `IReadOnlyDictionary<string, CelValue>.ContainsKey` — no failure path, missing key returns `CelValue.Bool(false)`.
- [x] 2.6 Add `CelFunctionCatalog.All` — internal read-only enumeration of every declared overload, for a catalog-completeness/security test.

## 3. Tests

- [x] 3.1 Add `tests/ArchLinterNet.CEL.Tests/CelBuiltinFunctionInvokerTests.cs` covering: positive case for each of the seven operation ids; `startsWith`/`endsWith`/`contains` boundary cases (empty needle, needle longer than haystack, exact match, ordinal case sensitivity); `size()` on an empty string/list/map; `size()` on a BMP string (`"abc"` → 3); `size()` on a single surrogate-pair character (`"😀"` → 1); `size()` on a combining sequence (`"é"` as `e` + combining acute → 2); `containsKey` on a present key (`true`) and a missing key (`false`, no exception).
- [x] 3.2 Add a catalog-completeness test: `CelFunctionCatalog.All` contains exactly the seven documented overloads (function name, receiver kind, argument kinds, result type, operation id) and no more — proves the closed surface for #327's security-test acceptance criterion.
- [x] 3.3 Run `rtk dotnet test tests/ArchLinterNet.CEL.Tests --no-restore` and fix failures.

## 4. Docs and spec sync

- [x] 4.1 Update `docs/internal/cel-engine-architecture.md`: correct the pipeline diagram's "Bounded Evaluator" row, which currently mislabels task #327 as the tree-walking evaluator (that is #328) — describe #327's actual shipped scope (operation-id seam, pure per-overload implementations) instead; add a "Built-in function execution" row to the component-ownership table.
- [x] 4.2 Compare implementation against `design.md` and the delta spec; adjust the delta spec only if implementation reveals a genuine scope gap.
- [x] 4.3 Run `openspec validate --all`.
- [ ] 4.4 Run `openspec archive cel-builtin-function-catalog` and verify the archived spec.

## 5. Validation and PR

- [x] 5.1 Run `rtk make fmt` and inspect formatting changes.
- [x] 5.2 Run `rtk make acceptance` (lint + full test suite) and fix any issue-related failures.
- [x] 5.3 Open the pull request closing #327.

## 6. Review follow-up (PR #339, round 1)

- [x] 6.1 Add `CelBuiltinFunctionInvoker.ComputeCost(CelFunctionOperationId, CelValue? receiver, IReadOnlyList<CelValue> arguments) -> long` — linear, input-size-proportional cost model, one `case` per operation id, next to `Invoke`; add `ComputeCost` test coverage.
- [x] 6.2 Strengthen `Catalog_MatchesTheDocumentedOverloadSet` to compare every field (argument kinds in order, result type, operation id), not just name/receiver/arity, plus a per-overload round-trip invocation test — catches a mismatched `OperationId` a name/receiver/arity-only comparison would miss.
- [x] 6.3 Correct remaining stale `#327` references that meant "the evaluator" under the old task numbering: `CelCompiledPredicate.cs`/`CelCompiledExpression.cs` XML docs and `NotImplementedException` messages, and four spots in the main `cel-profile-v1` spec (`openspec/specs/cel-profile-v1/spec.md`) — three renumbered to `#328`, one (`String.size()` Unicode counting, which this change actually implements) reattributed from "the evaluator" to "the built-in function invoker."
- [x] 6.4 Re-run `rtk dotnet test tests/ArchLinterNet.CEL.Tests --no-restore`, `rtk make fmt`, `rtk make acceptance`, and `rtk openspec validate --all --strict`.

## 7. Review follow-up (PR #339, round 2)

- [x] 7.1 Fix `contains`'s `ComputeCost` from a linear sum (`receiverLength + argumentLength`) to the worst-case product (`receiverLength * argumentLength`) — the linear form underestimated real cost on an adversarial repeating-near-match-prefix receiver, since `string.Contains(Ordinal)` is a candidate-position search, not a single aligned comparison. Add a test asserting the exact `1 + n*m` formula and an adversarial regression test proving the charged cost is at least an order of magnitude above what the old linear-sum model would have produced.
- [x] 7.2 Update `docs/internal/cel-engine-architecture.md`'s "Function catalog" extension-direction row and pipeline/component-ownership rows to state that adding a future built-in requires a `case` in **both** `Invoke` and `ComputeCost` (the two are independent switches with `default` arms, so the compiler does not enforce this — it's a test/review responsibility, backed by `CelFunctionCatalog.All`-driven coverage exercising every declared operation id through both methods).
- [x] 7.3 Re-run `rtk dotnet test tests/ArchLinterNet.CEL.Tests --no-restore`, `rtk make fmt`, `rtk make acceptance`, and `rtk openspec validate --all --strict`.

## 8. Review follow-up (PR #339, round 3)

- [x] 8.1 Fix `containsKey`'s `ComputeCost`: it was charged the flat fixed floor as if it were O(1), but `Dictionary<string,_>.ContainsKey` hashes the key's entire content first (a linear pass, not cached/O(1)) and can compare against multiple entries on a hash collision. Since `CelEvaluationContextBuilder.Set()` bounds map/list entry count but not individual string length, an unbounded-length key would have done real linear work for a charged cost of `1`. New cost: fixed floor + key argument length + receiver map entry count.
- [x] 8.2 Rename the now-inaccurate `ComputeCost_SizeListSizeMapContainsKey_AreConstant` test to `ComputeCost_SizeListAndSizeMap_AreConstant` (drop `ContainsKey`, which is no longer constant); add `ComputeCost_ContainsKey_IsProportionalToKeyLength`, `ComputeCost_ContainsKey_IsProportionalToMapEntryCount`, and `ComputeCost_ContainsKey_MatchesExactFormula`.
- [x] 8.3 Re-run `rtk dotnet test tests/ArchLinterNet.CEL.Tests --no-restore`, `rtk make fmt`, `rtk make acceptance`, and `rtk openspec validate --all --strict`.
