## ADDED Requirements

### Requirement: Built-in function execution implementation scope for Profile v1

`ArchLinterNet.CEL`'s built-in function execution (internal `ArchLinterNet.CEL.Binding` namespace) SHALL implement exactly the following implementation scope, in addition to the built-in function
table, receiver/argument/result types, and Unicode code-point counting contract already fixed
elsewhere in this spec:

- Each of the seven catalog overloads fixed by the built-in-function-table requirement (`startsWith`,
  `endsWith`, `contains`, `size` on `String`/`List`/`Map`, `containsKey`) SHALL be identified by a
  stable internal `CelFunctionOperationId` enum member, carried on the overload's
  `CelFunctionOverload` declaration (`OperationId`). Execution SHALL be looked up by this operation
  identifier, never by re-parsing the function name string or receiver kind a second time — the
  binder's already-resolved `CelBoundCall.Overload` carries everything execution needs.
- A single internal, static, stateless `CelBuiltinFunctionInvoker.Invoke(CelFunctionOperationId,
  CelValue? receiver, IReadOnlyList<CelValue> arguments) -> CelValue` SHALL provide one pure,
  side-effect-free implementation per operation identifier. It SHALL NOT read or write any static
  mutable state, perform CLR reflection, or accept a caller-supplied delegate.
- All seven catalog overloads SHALL be total given a receiver/argument shape the binder has already
  proven correct: none SHALL have a runtime failure case. `Invoke` therefore SHALL return `CelValue`
  directly with no failure/diagnostic channel — a future evaluator (#328) is responsible for wrapping
  a successful `Invoke` call in whatever result shape its evaluation step uses.
- `startsWith`, `endsWith`, and `contains` SHALL compare strings using ordinal (UTF-16 code-unit
  sequence) comparison — Profile v1 defines no locale/culture concept, consistent with this spec's
  existing rejection of implicit numeric widening and its requirement of exact structural equality
  for `==`.
- `String.size()` SHALL count Unicode code points via `System.Text.Rune` enumeration over the
  receiver string, never `string.Length` (UTF-16 code units) — matching this spec's existing
  surrogate-pair and combining-sequence `size()` scenarios. Because `CelValue.String()` already
  rejects malformed UTF-16 at construction, every string `Invoke` receives is guaranteed
  well-formed and safe to enumerate as `Rune`s.
- `List.size()` and `Map.size()` SHALL return the receiver's element/entry count.
- `containsKey` SHALL return `CelValue.Bool(false)` for a key absent from the receiver map — it
  SHALL NOT throw or otherwise signal failure for a missing key, matching this spec's existing
  logical-operator/error-semantics requirement for `containsKey`'s evaluation-time behavior.
- `CelFunctionCatalog` SHALL expose its complete overload set as an enumerable collection suitable
  for a conformance/security test asserting the catalog is exactly these seven overloads and no
  more.
- Alongside `Invoke`, a single internal, static, stateless
  `CelBuiltinFunctionInvoker.ComputeCost(CelFunctionOperationId, CelValue? receiver,
  IReadOnlyList<CelValue> arguments) -> long` SHALL provide one cost-model implementation per
  operation identifier, so a future evaluator (#328) charges each built-in call's true,
  input-size-proportional cost against `CelEvaluationLimits.MaxCostUnits` instead of treating every
  built-in call as a fixed unit cost or duplicating a second per-function switch of its own.
  `ComputeCost` SHALL NOT underestimate an operation's actual work — this is the property that makes
  `MaxCostUnits` a real budget rather than a decoration — so its model differs per operation's actual
  execution mechanism rather than using one uniform formula: `startsWith`/`endsWith` compare exactly
  one aligned prefix/suffix window (a single memory comparison, never re-scanned from a different
  offset) and `size` on `String` is one linear `Rune` pass, so each costs a fixed floor of `1` plus
  the UTF-16 length of the string operand scanned (the argument for `startsWith`/`endsWith`, the
  receiver for `size`). `contains` is the exception: it delegates to a candidate-position substring
  search whose real-world cost on an adversarial receiver (e.g. a long repeating near-match prefix)
  approaches the *product* of both operand lengths, not their sum, so `ComputeCost` for `contains`
  SHALL be the fixed floor plus the product of the receiver string's and argument string's UTF-16
  lengths — a conservative worst-case estimate, deliberately not each operation's exact algorithmic
  complexity, but never an underestimate. `size` on `List`/`Map` SHALL cost only the fixed floor,
  since both are backed by an O(1) count field. `containsKey` is NOT O(1) despite being a dictionary
  lookup: hashing the key requires a linear pass over its content, and a hash collision can compare
  the key against other entries in the same bucket — `CelEvaluationContextBuilder.Set()` bounds
  map/list entry count but not individual string length, so a fixed floor would let an
  unbounded-length key be charged the same as a one-character key. `ComputeCost` for `containsKey`
  SHALL therefore be the fixed floor plus the key argument's UTF-16 length (the hash-computation
  pass) plus the receiver map's entry count (a conservative bound on worst-case collision-chain
  comparisons).
- This change SHALL NOT add, wire, or modify any `CelBoundNode` tree evaluation, `&&`/`||`
  short-circuit/error-absorption behavior, or map/list index runtime-failure handling — those
  remain the bounded evaluator's scope (#328). `CelCompiledPredicate.Evaluate` and
  `CelCompiledExpression.Evaluate` SHALL continue to throw `NotImplementedException` after this
  change ships, exactly as after the binder (#326).

#### Scenario: startsWith/endsWith/contains use ordinal string comparison

- **WHEN** `CelBuiltinFunctionInvoker.Invoke` is called for `StartsWith`/`EndsWith`/`Contains` with
  string operands that would compare differently under a culture-sensitive comparison than under
  ordinal comparison
- **THEN** the result matches ordinal (UTF-16 code-unit sequence) comparison semantics

#### Scenario: size() on a BMP string counts code points via Rune enumeration

- **WHEN** `CelBuiltinFunctionInvoker.Invoke` is called for `SizeString` with receiver
  `CelValue.String("abc")`
- **THEN** the result is `CelValue.Int(3)`

#### Scenario: size() on a surrogate-pair character counts one code point

- **WHEN** `CelBuiltinFunctionInvoker.Invoke` is called for `SizeString` with receiver
  `CelValue.String("😀")` (one code point, two UTF-16 code units)
- **THEN** the result is `CelValue.Int(1)`, not `CelValue.Int(2)`

#### Scenario: size() on a combining sequence counts separate code points

- **WHEN** `CelBuiltinFunctionInvoker.Invoke` is called for `SizeString` with receiver
  `CelValue.String("é")` (LATIN SMALL LETTER E followed by COMBINING ACUTE ACCENT)
- **THEN** the result is `CelValue.Int(2)`

#### Scenario: containsKey on a missing key returns false without failing

- **WHEN** `CelBuiltinFunctionInvoker.Invoke` is called for `ContainsKey` with a map receiver that
  does not contain the supplied key argument
- **THEN** the result is `CelValue.Bool(false)`
- **AND** no exception is thrown

#### Scenario: Every catalog overload carries a stable operation identifier

- **WHEN** `CelFunctionCatalog.All` is enumerated
- **THEN** it contains exactly seven overloads
- **AND** each overload's `OperationId` uniquely identifies it

#### Scenario: ComputeCost scales linearly for single-pass string operations

- **WHEN** `CelBuiltinFunctionInvoker.ComputeCost` is called for `StartsWith`, `EndsWith`, or
  `SizeString` with a large string operand, then again with a small one, all else equal
- **THEN** the large-operand call returns a strictly greater cost

#### Scenario: ComputeCost for contains is the worst-case product of both operand lengths, not their sum

- **WHEN** `CelBuiltinFunctionInvoker.ComputeCost` is called for `Contains` with a receiver of
  length `n` and an argument of length `m`
- **THEN** the returned cost equals `1 + n * m`
- **AND** for a receiver built from a long repeating near-match prefix (the classic naive-substring-
  search worst case), that cost is at least an order of magnitude greater than `1 + n + m` would
  have been

#### Scenario: ComputeCost is constant for O(1) operations

- **WHEN** `CelBuiltinFunctionInvoker.ComputeCost` is called for `SizeList` or `SizeMap` with a small
  collection, then again with a large collection, all else equal
- **THEN** both calls return the same cost (the fixed floor)

#### Scenario: ComputeCost for containsKey scales with key length and map entry count

- **WHEN** `CelBuiltinFunctionInvoker.ComputeCost` is called for `ContainsKey` with a long key
  argument, then again with a short key argument, the receiver map held constant
- **THEN** the long-key call returns a strictly greater cost
- **AND** when instead the receiver map's entry count is varied with the key argument held
  constant, the larger map returns a strictly greater cost

#### Scenario: Invoke never throws for a binder-guaranteed-correct call shape

- **WHEN** `CelBuiltinFunctionInvoker.Invoke` is called with a receiver/argument shape matching one
  of the seven catalog overloads exactly
- **THEN** it returns a `CelValue` and never throws

#### Scenario: Evaluate remains unimplemented after this change

- **WHEN** a `CelCompiledPredicate` or `CelCompiledExpression` is obtained from a successful
  compilation after this change ships
- **THEN** calling `Evaluate` still throws `NotImplementedException`
