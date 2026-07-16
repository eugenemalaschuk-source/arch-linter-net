## Context

`CelFunctionCatalog` (#326) already declares the closed set of seven Profile v1 built-in function
overloads and `CelBinder` already resolves every call against it, producing a `CelBoundCall` that
carries the matched `CelFunctionOverload`. What's missing is the actual computation: given a
receiver `CelValue` and argument `CelValue`s that the binder has already proven type-correct, what
value does calling `startsWith`/`size`/`containsKey`/etc. actually produce? The issue (#327)
requires this to be architecturally separate from both the declaration (#326's
`CelFunctionCatalog`) and the future tree-walking evaluator (#328), so that adding one more standard
CEL function later touches exactly one declaration row, one operation-id enum member, and one
`case` in the invoker — not a scattered set of conditionals across binder, evaluator, and docs.

## Goals / Non-Goals

**Goals**

- Make every catalog overload's result computable in isolation, directly against `CelValue`s,
  without requiring the general evaluator to exist.
- Give #328 a single, controlled lookup path from a bound call to its implementation.
- Preserve the closed/no-registration architecture: the invoker is a static, stateless, internal
  type with an exhaustive `switch` over a closed enum — no delegate storage, no dictionary of
  `Func<...>`, nothing a caller or future code could mutate at runtime.

**Non-Goals**

- Building `CelBoundNode` tree evaluation, `&&`/`||` short-circuit/error-absorption semantics, or
  map/list index runtime-failure handling — all #328.
- Wiring `Invoke` into `CelCompiledPredicate.Evaluate`/`CelCompiledExpression.Evaluate` — #328.
- Adding any new public API — everything in this change lives in the internal
  `ArchLinterNet.CEL.Binding` namespace.

## Decisions

### Operation identity: enum, not string re-dispatch

`CelFunctionOverload` gains `CelFunctionOperationId OperationId`. The evaluator (#328) will switch
on this enum, not re-parse `FunctionName` + receiver kind. This is the concrete form of the issue's
"internal pure operation identity" requirement: the enum is the single seam a future built-in
addition extends (one new member, one new catalog row referencing it, one new invoker `case`),
matching the extension-direction matrix's "Function catalog" row in
`docs/internal/cel-engine-architecture.md`, which already commits to "one reviewed inclusion point"
without a public registration API.

### Placement: `ArchLinterNet.CEL.Binding`, not a new namespace

The issue's own diagram places "internal pure operation identity or sealed implementation" between
"binder and overload resolver" and "bounded evaluator" — i.e. it is produced/known at bind time
(the enum value lives on the already-bound `CelFunctionOverload`) but *executed* later by the
evaluator. Keeping `CelBuiltinFunctionInvoker` in `Binding` next to `CelFunctionCatalog` and
`CelFunctionOverload` keeps the three artifacts (declaration, the operation-id link, and the pure
implementation) co-located and easy to review as one seam, rather than splitting an
`Evaluation`-namespace file that has no other dependency on the evaluator's not-yet-existing
tree-walking machinery.

### No failure channel: `Invoke` returns `CelValue`, not a result/diagnostic type

Every scenario in the spec's built-in-function-table requirement is total given a binder-guaranteed
receiver/argument shape: `startsWith`/`endsWith`/`contains`/`size` have no missing-input case at
all, and `containsKey`'s only edge case (a missing key) is spec-mandated to return `CelValue.Bool
(false)`, never fail. (Contrast with `Map[key]` *indexing*, which the spec does allow to fail with
`EvaluationFailure` — but that's the index operator, evaluated directly by #328's tree-walker, not
a catalog function.) Giving `Invoke` a `CelEvaluationResult`-shaped return now would invent a
failure path with no real producer, so `Invoke` returns `CelValue` directly; #328 wraps it in
whatever result shape the tree-walker uses for a successful step.

### `size()` counts Unicode code points via `System.Text.Rune`, never `string.Length`

Mandated directly by the existing spec requirement and its three scenarios (BMP string, a
surrogate-pair character, a combining sequence). `value.EnumerateRunes().Count()` is the
correct/simplest .NET mechanism — `CelValue.String()` already rejects unpaired surrogates at
construction, so every string reaching `Invoke` is guaranteed well-formed UTF-16 and therefore safe
to enumerate as `Rune`s without a malformed-input case.

### Cost model: `ComputeCost` sits next to `Invoke`, one `case` per operation

The issue's controlled-extension-seam requirement is explicit about "explicit complexity/budget
classification" as part of what a catalog entry must carry, and `CelEvaluationLimits.MaxCostUnits`
is already public API described as "an abstract measure of computational work" — but leaving cost
modeling entirely to #328 would force the evaluator to either treat every built-in call as a fixed
unit cost (unsafe: a `contains()` call against an arbitrarily large receiver string would cost the
same as one against an empty string, defeating the budget's purpose) or build its own second
per-`OperationId` switch duplicating knowledge only `CelBuiltinFunctionInvoker` has (what each
operation actually scans). `ComputeCost(operationId, receiver, arguments) -> long` closes that gap
now: same file, same enum, one `case` added alongside `Invoke`'s `case` whenever a future function
is added — no second seam to keep in sync.

The model must never *underestimate* real work — that's the property that makes a budget a budget —
so it is not one uniform formula across every operation, it follows each operation's actual
execution mechanism:

- `startsWith`/`endsWith` compare exactly one aligned prefix/suffix window (a single memory
  comparison at a fixed offset, never re-scanned from elsewhere) and `size()` on `String` is one
  linear `Rune` pass — each has a real, provable `O(operand length)` bound, so a fixed floor plus
  that length is exact enough.
- `contains` does **not** get the same linear treatment. `string.Contains(str,
  StringComparison.Ordinal)` is a candidate-position substring search: on a receiver built from a
  long repeating near-match prefix (e.g. searching `"aaa...ab"` for `"aaa...ab"`-shaped receivers),
  .NET's implementation re-compares a long overlapping run at many candidate offsets, and real cost
  approaches `O(receiverLength · argumentLength)`. An initial version of this cost model used
  `receiverLength + argumentLength` for `contains` too, discovered in review to be an *underestimate*
  on exactly this adversarial shape — a crafted receiver could do far more real CPU work than its
  charged cost, defeating `MaxCostUnits` as a budget rather than merely under-modeling it. The fix:
  `contains`'s cost is the **product** of both operand lengths (a conservative worst-case bound,
  deliberately not each operation's exact complexity, but never an underestimate). The product
  cannot overflow `long`: both lengths are `int`, and `int.MaxValue * (long)int.MaxValue` is well
  within `long.MaxValue`.
- `size()` on `List`/`Map` are O(1) (backed by a count field) and cost only the fixed floor.
- `containsKey` is **not** O(1) despite being a dictionary lookup, discovered in a third review
  round: `Dictionary<string,_>.ContainsKey` first computes `string.GetHashCode()` — .NET's string
  hash (Marvin32) is a linear pass over the key's content, not a cached or O(1) value — and a hash
  collision can then compare the key against other entries in the same bucket.
  `CelEvaluationContextBuilder.Set()` bounds map/list *entry count* (`MaxValidationCollectionSize`)
  but not individual *string length*, so an unbounded-length key would have been charged the same
  fixed floor as a one-character key while doing real linear work — the same never-underestimate
  defect the `contains` fix above corrected, just for a different operation.
- A **fourth** review round then caught that the round-3 fix (`keyLength + entryCount`) was itself
  still an underestimate: it charged for the *number* of collision-chain comparisons but not the
  *cost of each* — `string.Equals` against a near-match colliding key doesn't fail on the first
  character, it scans up to the full key length before failing. `entryCount` such comparisons at up
  to `keyLength` each is `keyLength * entryCount` real work, not `keyLength + entryCount`. The
  corrected formula is `keyLength * (entryCount + 1)` — the `+1` folds in the one guaranteed
  hash-computation pass over the key itself, and the multiplication is the conservative worst-case
  bound assuming every collision-chain comparison scans the full key (it can't overflow `long`
  given Profile v1's existing 1024-entry collection-size cap). Two review rounds needed to reach a
  correct `containsKey` cost is itself informative: a per-operation cost model is not "obviously
  right" the first time an operation *looks* O(1) — the number of sub-steps and the cost of each
  sub-step are two separate questions, and a cost model that only answers the first will still
  underestimate.

`CelBuiltinFunctionInvokerTests` includes an adversarial regression test constructing exactly this
repeating-near-match-prefix shape and asserting the charged cost is at least an order of magnitude
above what the old linear-sum model would have produced, so a future regression back to a linear
`contains` cost fails the suite rather than silently reopening the gap. A second adversarial
regression test does the same for `containsKey`'s multiplicative-vs-additive formula.

### String matching is ordinal (UTF-16 code-unit sequence comparison)

The pinned CEL spec does not define culture-sensitive string comparison for `startsWith`/
`endsWith`/`contains`, and Profile v1 defines no locale/culture concept anywhere in its value or
context model. `StringComparison.Ordinal` is therefore the only comparison mode consistent with
CEL strings being plain Unicode code-point sequences with no implicit culture association — the
same reasoning the spec already applies to reject implicit numeric widening and to require exact
structural equality for `==`.

### Documenting the Invoke/ComputeCost dual-switch gap explicitly

`Invoke` and `ComputeCost` are two independent `switch` expressions over the same
`CelFunctionOperationId` enum, each with a `default` arm that throws. That `default` arm means the
compiler does **not** enforce that adding an operation id updates both switches — a future author
could add a `case` to `Invoke` alone and the build would still succeed, silently reopening the
fixed-unit-cost gap this change closes. Rather than removing the `default` arm (which would require
every switch to be literally exhaustive over the enum, awkward given both are also called
defensively from generic/future code), `docs/internal/cel-engine-architecture.md`'s "Function
catalog" extension-direction row now says explicitly that both cases are required, and
`CelBuiltinFunctionInvokerTests` iterates `CelFunctionCatalog.All` — the same catalog a future
addition's declaration row must join — through *both* `Invoke` and `ComputeCost`, so a declared
overload with no matching `ComputeCost` case fails a test immediately rather than only being caught
by a doc a reviewer might skip.

## Risks / Trade-offs

- **Risk**: a future function needing a failure channel (e.g. a bounded-complexity function that
  could exceed a budget mid-computation) won't fit `Invoke`'s current `CelValue`-only signature.
  **Mitigation**: none of the seven v1 overloads need one; the invoker is a v1-scoped internal type
  that a future profile version is free to redesign — this is exactly the "future profile expansion
  must preserve Profile v1 behavior and identity" boundary the issue already draws, not a promise
  this shape is permanent.
- **Trade-off**: `OperationId` on `CelFunctionOverload` is data the binder never reads (it only
  matters to the not-yet-built evaluator). Accepted because splitting it into a *second* parallel
  table keyed by function name + receiver kind would reintroduce the "declaration answers know
  their own operation identity" duplication the issue explicitly asks to avoid.
