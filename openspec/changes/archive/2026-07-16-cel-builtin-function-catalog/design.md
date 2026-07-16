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

### String matching is ordinal (UTF-16 code-unit sequence comparison)

The pinned CEL spec does not define culture-sensitive string comparison for `startsWith`/
`endsWith`/`contains`, and Profile v1 defines no locale/culture concept anywhere in its value or
context model. `StringComparison.Ordinal` is therefore the only comparison mode consistent with
CEL strings being plain Unicode code-point sequences with no implicit culture association — the
same reasoning the spec already applies to reject implicit numeric widening and to require exact
structural equality for `==`.

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
