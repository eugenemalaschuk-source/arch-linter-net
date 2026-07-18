# CEL Engine Architecture Blueprint

This is internal project documentation for maintaining the `arch-linter-net` repository.
It is intentionally excluded from the public MkDocs/GitHub Pages product site.

This document originated as the target architecture of `ArchLinterNet.CEL` written before implementation tasks #325–#329 began, as the design/spec slice tracked by #324. Tasks #325–#329 have since shipped the tokenizer/parser, binder/type checker, built-in function catalog, bounded evaluator, and public compilation pipeline/cache identity described below. Task #330 reconciled and finalized this document against that shipped code: every section is now a description of implemented, tested behavior — not a pre-implementation plan — except where a section is explicitly labeled as a future extension direction.

The structure below follows the 9 required sections from #330, plus supporting material (comparative implementation review, performance baselines, versioning policy) that #330 also finalized.

______________________________________________________________________

## 1. Engine lifecycle and module graph

### Processing pipeline

```text
expression source (string)
        │
        ▼
  ┌─────────────────┐
  │   Tokenizer     │  (task #325 — shipped: ArchLinterNet.CEL.Parsing.CelTokenizer)
  │                 │  Converts source bytes to a token stream.
  │  Limits:        │  Enforces MaxExpressionLength (CelEnvironment, before tokenizing),
  │                 │  MaxTokenCount and MaxLiteralSize (tokenizer itself).
  └────────┬────────┘
           │  token stream
           ▼
  ┌─────────────────┐
  │  Parser / AST   │  (task #325 — shipped: ArchLinterNet.CEL.Parsing.CelParser +
  │  (internal)     │  CelSyntaxNode hierarchy). Precedence-climbing (Pratt) parser;
  │                 │  MaxNestingDepth and MaxAstNodeCount enforced here. Distinguishes
  │                 │  SyntaxError (invented/malformed syntax) from UnsupportedFeature
  │                 │  (valid CEL excluded from v1: arithmetic, `? :`, null/uint/bytes/
  │                 │  triple-quoted-string/octal-escape literals, list/map/message
  │                 │  literals). Fails fast — one diagnostic
  │                 │  per attempt, no error recovery. Full input consumption required.
  │  NOT public.    │  AST nodes are never exposed. Binder (#326) consumes CelSyntaxNode
  │                 │  internally; CelEnvironment.CompilePredicate/Compile call the
  │                 │  tokenizer+parser after the MaxExpressionLength gate — a syntax/
  │                 │  unsupported-feature/structural-limit diagnostic short-circuits
  │                 │  compilation with a real span; syntactically valid Profile v1
  │                 │  input proceeds to the binder.
  └────────┬────────┘
           │  internal syntax tree
           ▼
  ┌─────────────────┐
  │  Binder /       │  (task #326 — shipped: ArchLinterNet.CEL.Binding.CelBinder)
  │  Type Checker   │  Resolves identifiers against CelContextSchema, members against
  │  (internal)     │  CelObjectSchema, and calls against the closed Profile v1
  │                 │  built-in function catalog (CelFunctionCatalog) — no reflection,
  │                 │  no dynamic dispatch, no user-registered functions. Checks every
  │                 │  operator/index/call against the frozen signature table with no
  │                 │  implicit Int/Float widening. Binds the whole AST unconditionally
  │                 │  (both operands of &&/||/in are always bound, regardless of a
  │                 │  future evaluator's short-circuit semantics). Enforces the
  │                 │  compilation request's required result type (Predicate requires
  │                 │  Bool). Fails fast — one diagnostic per attempt, category
  │                 │  "binder", no aggregation, matching the parser's contract.
  │  NOT public.    │  MaxIdentifierCount is enforced by the parser (#325), not here —
  │                 │  it is a purely syntactic count needing no schema information;
  │                 │  the pipeline row above is stale on this point and was corrected
  │                 │  by #326. CelEnvironment.CompilePredicate/Compile run the binder
  │                 │  immediately after a successful parse: binder success now returns
  │                 │  a real CelCompiledPredicate/CelCompiledExpression instead of
  │                 │  NotYetImplemented; binder failure returns the structured
  │                 │  diagnostic instead of NotYetImplemented.
  └────────┬────────┘
           │  immutable bound plan
           ▼
  ┌─────────────────┐
  │  Bound Plan     │  Internal representation of a fully-checked expression
  │  (internal)     │  (ArchLinterNet.CEL.Binding.CelBoundExpression — one bound node
  │                 │  per syntax node, each carrying its resolved CelType).
  │                 │  Owned by CelCompiledPredicate / CelCompiledExpression.
  │                 │  Never exposed publicly. Consumed directly by the bounded
  │                 │  evaluator (#328); no public API exposes or serializes it.
  └────────┬────────┘
           │
    (compilation complete — CelCompiledPredicate / CelCompiledExpression returned)
           │
           ▼  (evaluation, triggered by .Evaluate(context, limits))
  ┌─────────────────┐
  │  Built-in       │  (task #327 — shipped: ArchLinterNet.CEL.Binding.CelBuiltinFunctionInvoker)
  │  function       │  Pure, stateless execution of the closed Profile v1 catalog
  │  execution      │  (startsWith/endsWith/contains/size/containsKey), keyed by the
  │  (internal)     │  CelFunctionOperationId each CelFunctionOverload now carries —
  │                 │  never by re-dispatching on the function name string. Every
  │                 │  overload is total given a binder-guaranteed-correct call shape,
  │                 │  so Invoke() returns a CelValue directly with no failure channel.
  │                 │  ComputeCost() sits alongside Invoke() with its own case per
  │                 │  operation id — cost is NOT a byproduct of Invoke() and must
  │                 │  never underestimate an operation's real work (e.g. Contains'
  │                 │  cost is receiverLength * argumentLength, a worst-case product,
  │                 │  not a sum — see the type's XML doc for why a linear estimate
  │                 │  would let a crafted input's real cost exceed its charged cost).
  │  NOT public.    │  Deviation from the row below (written before task numbering
  │                 │  settled): this task does NOT walk the bound plan or implement
  │                 │  short-circuit semantics — see the "Bounded Evaluator" row (#328).
  └────────┬────────┘
           │  invocable per-overload implementation + cost model, keyed by operation id
           ▼
  ┌─────────────────┐
  │  Bounded        │  (task #328 — shipped: ArchLinterNet.CEL.Evaluation.CelEvaluator)
  │  Evaluator      │  Walks the bound plan against a CelEvaluationContext.
  │                 │  Enforces MaxIterations and MaxCostUnits per call.
  │                 │  Uses a deterministic left-to-right traversal while still
  │                 │  producing CEL's observable short-circuit/error semantics
  │                 │  for && / || (determining operands absorb ordinary failures;
  │                 │  BudgetExceeded is terminal).
  │                 │  Calls CelBuiltinFunctionInvoker.Invoke AND ComputeCost for every
  │                 │  CelBoundCall, using boundCall.Overload.OperationId to select the
  │                 │  operation — charging ComputeCost's result against MaxCostUnits is
  │                 │  what makes the budget real; skipping it silently reintroduces the
  │                 │  fixed-unit-cost gap #327 was written to close. Also charges
  │                 │  non-call runtime work such as list/map membership, map/object
  │                 │  lookup, string equality (including ObjectTypeId), and recursive deep equality so
  │                 │  MaxCostUnits tracks real collection work instead of only
  │                 │  built-in invocations. Rejects incompatible evaluation
  │                 │  contexts using the full compilation schema identity
  │                 │  (CelContextSchema + object-schema catalog), returning
  │                 │  SchemaMismatch instead of allowing typed-plan/runtime-data
  │                 │  skew. Reports missing-key / invalid-index runtime failures
  │                 │  as structured CelEvaluationResult diagnostics, never CLR exceptions.
  │                 │  Contexts precompute their immutable name lookup at Build(), so
  │                 │  evaluation does not rebuild an activation-sized dictionary per call.
  └────────┬────────┘
           │  typed result or failure diagnostics
           ▼
  ┌─────────────────┐
  │  CelEvaluation  │  Public result surface.
  │  Result         │  IsSuccess / Value / Diagnostics.
  └─────────────────┘
```

______________________________________________________________________

### Component ownership

| Component | Owner | Notes |
|---|---|---|
| Profile identity (`CelProfile`, `CelProfileId`) | `ArchLinterNet.CEL` public | Stable across versions; v1 ID is `arch-linter/cel/v1` |
| Grammar gates (token set, operator set) | `ArchLinterNet.CEL.Parsing` internal (`CelTokenizer`, `CelParser`) | No public API. Deviation from the original design: the gate lives in the tokenizer/parser themselves (deferred tokens are lexed, then rejected by the parser with `UnsupportedFeature`), not in `CelEngine` — `CelEngine` remains an unused placeholder. Corrected description: today `CelTokenizer`/`CelParser` hard-code the Profile v1 grammar; `CelProfileId` is threaded through both purely to attribute structured diagnostics to a profile, not to select which grammar rules apply — there is no profile-dispatch mechanism yet, since only `CelProfile.V1` exists publicly. A real "controlled per-profile" gate is v2+ work (see the Language and profile expansion row below). #328 shipped the bounded evaluator as `ArchLinterNet.CEL.Evaluation.CelEvaluator`, a standalone type, not inside `CelEngine`; `CelEngine` is still an empty placeholder after #325–#329, matching the Function catalog row below |
| Value model (`CelValue`, `CelObjectValue`, `CelValueKind`) | `ArchLinterNet.CEL` public | No CLR reflection; all factories are typed |
| Type descriptors (`CelType`, `CelTypeKind`) | `ArchLinterNet.CEL` public | Static factories only |
| Context schema (`CelContextSchema`, `CelVariable`) | `ArchLinterNet.CEL` public | Structural identity is deterministic |
| Function catalog (declaration) | `ArchLinterNet.CEL.Binding.CelFunctionCatalog` internal | Immutable; no public registration; `CelEngine` remains an unused placeholder. `CelFunctionCatalog` is a single hard-coded Profile v1 list today, not parameterized by profile — a real per-profile catalog is v2+ work, matching the Grammar gates row above |
| Built-in function execution | `ArchLinterNet.CEL.Binding.CelBuiltinFunctionInvoker` internal | Shipped by #327. Pure, stateless, keyed by `CelFunctionOperationId` (carried on each `CelFunctionOverload`); every overload is total, no failure channel. `Invoke` and `ComputeCost` are two separate switches over the same enum — the compiler does NOT enforce that adding an operation id updates both (each has a `default` arm, so an omitted `ComputeCost` case is a silent budget-safety gap, not a build error); a code-review checklist item, not a compiler guarantee, is what closes this. Never exposed; `ArchLinterNet.CEL.Evaluation.CelEvaluator` (#328, shipped) is the only caller |
| Bound operations (bound plan, binding tables) | `ArchLinterNet.CEL.Binding` internal (`CelBinder`, `CelBoundExpression`, `CelBoundNode` hierarchy) | Never exposed |
| Bounded evaluator runtime | `ArchLinterNet.CEL.Evaluation.CelEvaluator` internal | Shipped by #328. One per-call runtime state, no shared mutable globals, source-span-aware diagnostics, schema-compatibility check against the full environment schema identity, and budget enforcement over bound-node visits, built-in cost charges, and collection/comparison runtime work |
| Evaluation budgets (`CelCompilationLimits`, `CelEvaluationLimits`) | `ArchLinterNet.CEL` public | SafeDefaults provided; no unbounded path |
| Compiled programs (`CelCompiledPredicate`, `CelCompiledExpression`) | `ArchLinterNet.CEL` public | Immutable; thread-safe; hold bound plan internally |
| Diagnostics (`CelDiagnostic`, `CelDiagnosticCode`, `CelSourceSpan`) | `ArchLinterNet.CEL` public | Stable codes; message is display-only |
| Cache identity (`CelCompilationKey`) | `ArchLinterNet.CEL` public | Deterministic; caller-owned cache lifetime |

______________________________________________________________________

## 2. Public API stability boundary

Profile v1 deliberately excludes 9 concepts from the public API surface. Each is a controlled
architectural boundary, not an oversight — an exclusion is not the same as "not supported" without
a plan. This section records why each is excluded today and where a future, reviewed implementation
would fit if ever approved. See [Prohibited shortcuts](#prohibited-shortcuts) below for the
enforcement side of the same boundary (the patterns a change must never introduce quietly).

| Excluded concept | Why excluded from v1 | Future architectural direction |
|---|---|---|
| Parser AST exposure | The internal `CelSyntaxNode` hierarchy would become a forever-versioned public contract the moment it is exposed; no concrete tooling consumer exists yet to justify that cost | A future `ArchLinterNet.CEL.Tooling` package could expose a stable, separately-versioned neutral syntax model — never the parser-generator's own AST types. See section 7 |
| Arbitrary CLR object binding | Binding directly to arbitrary `object`/POCO instances requires reflection at the value-construction boundary, defeating the closed, statically-typed value model (`CelValue`, `CelObjectSchema`) | Host adapters convert typed host data into `CelValue`/`CelObjectValue` before evaluation, in a separate package that owns any reflection. See section 4 |
| Reflection and `dynamic` | Both defeat nullable analysis, AOT compatibility, and the schema-declared type-safety contract; `Cel.NET`'s reflection-driven member access was explicitly rejected during the comparative review (see below) for this reason | Not planned for the core engine under any profile; confined permanently to optional adapter packages, never `ArchLinterNet.CEL` itself |
| Raw `IDictionary<string, object?>` activations | A raw dictionary of untyped values reintroduces the same reflection/type-safety problem as arbitrary CLR binding, one level up at the evaluation-context boundary | `CelEvaluationContextBuilder.Set` requires typed `CelValue` instances via `CelVariable` handles; a host adapter may build a `CelEvaluationContext` from a typed source, but never accepts a raw untyped dictionary as a public compilation/evaluation input |
| Unrestricted delegates/expression trees | A public compilation result exposing a raw `Func<...>` or `Expression<T>` would bypass `CelEvaluationLimits` and structured diagnostics entirely — the exact failure mode the "compiled-delegate experiments" comparative review row was rejected for | Permanently prohibited as a public compilation result shape. An internal, swappable evaluator backend (section 6) may use delegates internally, but `CelCompiledPredicate`/`CelCompiledExpression` must remain opaque |
| Public function registration | A mutable, caller-extensible function registry is both a thread-safety hazard (shared mutable state) and an arbitrary-code-execution surface — user-defined functions could run anything | Built-in expansion follows the closed catalog process in section 5 (new profile version, reviewed PR, no runtime registration API). Host-defined functions remain a permanent non-goal for the isolated CEL library; a product layer may wrap `ArchLinterNet.CEL` and interpret function-like syntax itself without exposing a registry |
| Process-global cache ownership | A static/global cache breaks testability, thread-safety reasoning, and cache-lifetime ownership — the same reasoning behind the "no static mutable registry" prohibited shortcut | Callers own cache lifetime today via a source-text-keyed cache; a future `ArchLinterNet.CEL.Caching` helper package could package this pattern without introducing global state. See section 8 |
| Unbounded evaluation | Any evaluation path without `CelEvaluationLimits` enforcement would allow a crafted expression to consume unbounded CPU/memory — this is the core safety property the bounded evaluator (#328) exists to guarantee | Not a direction with a future relaxation path; this is a permanent invariant of the engine, enforced structurally (every `Evaluate` overload requires or defaults to limits) |
| Mutable environments | Go/Java's environment-extension APIs (`env.Extend`) were the primary source of thread-safety caveats in their own documentation, per the comparative review below; `CelEnvironment` is closed at `Build()` specifically to avoid this | Not planned. A caller needing a different schema/profile/limits builds a new `CelEnvironment` via `CelEnvironment.CreateBuilder(...)`, which is cheap relative to compilation |

______________________________________________________________________

## Extension-direction matrix

For each capability excluded from Profile v1, this matrix records: classification, intended owner, existing seam, whether a new profile version is required, affected pipeline layers, safety/complexity implications, prohibited shortcut, and whether the direction is plausible future work or a permanent non-goal. Sections 3–8 below correspond to sections 3–8 of #330's required blueprint structure.

### 3. Language/profile evolution

Capabilities deferred: arithmetic (`+`, `-`, `*`, `/`, `%`), conditional expression (`? :`), new literals (uint, bytes), triple-quoted string literals (`'''...'''`/`"""..."""`), octal string escapes (`\NNN`), timestamp/duration types, optional/null support, comprehensions/macros (`all`, `exists`, `map`, `filter`), regex (`matches`), protobuf integration, unknown/partial evaluation.

Triple-quoted string literals and octal escapes are already tokenized today (`CelTokenizer.LexTripleQuotedString`, `CelTokenKind.TripleQuotedStringLiteral`/`StringLiteralWithOctalEscape`) — the tokenizer fully validates their well-formedness (matching closer; for octal, exactly three digits in the `\000`-`\377` range) and `CelParser.ParsePrimary` classifies a well-formed instance `UnsupportedFeature`, following exactly the same shipped pattern as `null`/`uint`/byte-string literals in the row directly below. A malformed instance (unterminated triple-quote, incomplete/out-of-range octal escape) is `SyntaxError`. What remains actually deferred to a future profile version is triple-quote *content* semantics beyond well-formedness (e.g. any quote-form-specific escape nuance the pinned grammar might define) and octal-escape *decoding into a CEL value* — the lexical recognition and diagnostic classification are shipped, not future work (see `#338`'s corpus-mining task and `docs/internal/cel-corpus-mining-manifest.md`).

| Field | Details |
|---|---|
| Classification | Standard CEL (normative spec features) |
| Intended owner | `ArchLinterNet.CEL` public API (new profile version) |
| Existing seam | `CelTokenizer`/`CelParser` (not `CelEngine` — see the Grammar gates row above), which today hard-code the Profile v1 grammar; a real per-profile dispatch gate does not exist yet and is v2+ work, not a shipped seam. Type-system additions shipped by task #326 (`ArchLinterNet.CEL.Binding`) |
| New profile version required? | Yes — Profile v2+ adds these; Profile v1 semantics remain frozen |
| Affected layers | Tokenizer, parser, type-checker, evaluator, function catalog, diagnostics |
| Safety implications | Arithmetic can overflow; timestamp/duration parsing can be malformed; regex can cause ReDoS if unbounded |
| Prohibited shortcut | Do NOT add unsupported operators by relaxing the grammar gate without a new profile. Do NOT accept a user expression containing arithmetic in Profile v1 and silently discard the unsupported sub-tree |
| Direction | Plausible future work per approved profile stories |

### 4. Host data adapter direction

Capabilities deferred: POCO/CLR type adapters, `System.Text.Json` adapter, protobuf descriptor adapter.

| Field | Details |
|---|---|
| Classification | Host adapter (converts host-native data into CEL values) |
| Intended owner | A separate `ArchLinterNet.CEL.Adapters.*` package or a Core-owned conversion layer — NOT `ArchLinterNet.CEL` itself |
| Existing seam | `CelValue` factory methods accept typed values; `CelObjectValue` accepts a `IReadOnlyDictionary<string, CelValue>` — adapters convert to this shape |
| New profile version required? | No — adapters are conversion utilities, not language changes |
| Affected layers | Value construction path only |
| Safety implications | CLR reflection must be contained in the adapter, never in `ArchLinterNet.CEL` core |
| Prohibited shortcut | Do NOT add reflection, `dynamic`, or automatic POCO member discovery to `CelValue`, `CelObjectValue`, or `CelContextSchema` |
| Direction | Plausible future work; likely Core-owned for architecture facts |

### 5. Function and extension catalog direction

Capabilities deferred: caller-defined functions, host-registered function bundles, operator overloads. The closed Profile v1 catalog itself (`startsWith`/`endsWith`/`contains`/`size`/`containsKey`) is shipped — declaration in `ArchLinterNet.CEL.Binding.CelFunctionCatalog`, execution in `ArchLinterNet.CEL.Binding.CelBuiltinFunctionInvoker` (#327).

A future standard built-in follows one controlled path: add a `CelFunctionOperationId` member, add its declaration row to `CelFunctionCatalog` (name, receiver kind, argument kinds, result type, operation id), add its pure implementation as one `case` in `CelBuiltinFunctionInvoker.Invoke` **and** its cost model as one `case` in `CelBuiltinFunctionInvoker.ComputeCost` — both are required; `ComputeCost`'s `default` arm means a missing case is not a compile error, only a test/review gap (see `CelFunctionCatalog.All`-driven coverage in `CelBuiltinFunctionInvokerTests`, which exercises every declared operation id through both methods). No parser, binder, or evaluator change is needed beyond that — the binder already resolves any catalog entry generically, and the evaluator (#328) already dispatches any `CelBoundCall` through `boundCall.Overload.OperationId`.

| Field | Details |
|---|---|
| Classification | Standard CEL (built-in) + canonical extension (host-defined) |
| Intended owner | `ArchLinterNet.CEL.Binding.CelFunctionCatalog` internal (declaration) + `ArchLinterNet.CEL.Binding.CelBuiltinFunctionInvoker` internal (execution); future profiles may add more built-ins |
| Existing seam | Internal function-catalog lookup inside `ArchLinterNet.CEL.Binding` (`CelFunctionCatalog`, consumed by `CelBinder`) plus operation-id-keyed execution (`CelBuiltinFunctionInvoker`, consumed by `CelEvaluator` — #328, shipped). `CelFunctionCatalog` is not profile-parameterized today — it is a single hard-coded Profile v1 list; a per-profile catalog gate is v2+ work, not existing wiring |
| New profile version required? | Adding new built-ins requires a new profile; host-defined functions require explicit API design (excluded from v1) |
| Affected layers | Binder, type-checker, evaluator |
| Safety implications | User-defined functions could execute arbitrary code; excluded from v1 for this reason |
| Prohibited shortcut | Do NOT add a public `RegisterFunction(...)` API or any mutable function registry — not even a static one |
| Direction | Built-in expansion is plausible (new profile); host-defined functions are a permanent non-goal for the isolated CEL library (Core/product layer may wrap) |

### 6. Execution backend direction

Capabilities deferred: optimized planner, JIT-style compiled backend, alternate interpreter.

| Field | Details |
|---|---|
| Classification | Optimization |
| Intended owner | `CelEngine` internal; pluggable behind the non-public bound plan |
| Existing seam | `CelCompiledPredicate.Evaluate` / `CelCompiledExpression.Evaluate` call the static `ArchLinterNet.CEL.Evaluation.CelEvaluator.Evaluate(...)`, passing the held bound plan, schema, and limits; there is no evaluator *instance* held by the compiled program today, and no swappable-backend abstraction exists yet — a backend swap would first require introducing one (e.g. an internal evaluator seam the compiled program holds instead of calling the static method directly). The bound plan itself is never exposed |
| New profile version required? | No — backend swap would be transparent to consumers once a swappable seam exists |
| Affected layers | Bound plan representation, evaluator |
| Safety implications | New backend must preserve CEL short-circuit semantics and budget enforcement |
| Prohibited shortcut | Do NOT expose the bound plan or evaluator internals publicly. Do NOT allow raw delegate escape (e.g., `Func<CelEvaluationContext, bool>` as a public compilation result) |
| Direction | Plausible future work; no dedicated performance-optimization task is currently scheduled — #329 shipped the public compilation pipeline and cache identity (`CelCompilationKey`), not a backend swap or optimized planner. A pluggable-evaluator seam is itself unbuilt; introducing one is a prerequisite for this direction, not an existing capability |

### 7. Tooling and AST direction

Capabilities deferred: public AST / neutral syntax model, formatter, pretty-printer, serialization.

| Field | Details |
|---|---|
| Classification | Tooling |
| Intended owner | A future separate `ArchLinterNet.CEL.Tooling` package |
| Existing seam | Internal syntax tree in the parser — never surfaced publicly |
| New profile version required? | No — tooling is additive and read-only |
| Affected layers | Parser (read-only export of syntax tree) |
| Safety implications | Low; read-only model |
| Prohibited shortcut | Do NOT expose parser-generator contexts, grammar rule objects, or internal AST nodes as public API. Do NOT add tooling types to `ArchLinterNet.CEL` until concrete requirements from a tooling story are approved |
| Direction | Plausible future work; requires a concrete story with stable neutral model design |

### 8. Cache and serialization direction

Capabilities deferred: caller-owned cache helper implementations, portable checked-expression format.

This row previously described `CelCompilationKey` as the recommended pre-compile cache-lookup key
without qualification, which #168's benchmarking work (`CacheIdentityBenchmarks`, `RESULTS.md`)
showed is not achievable through the public API: every component `CelCompilationKey` is built from
(`CelContextSchema.ComputeEnvironmentIdentity`, `CelCompilationLimits.ComputeIdentity`,
`CelEvaluationLimits.ComputeIdentity`) is internal, so a caller cannot construct or derive one
*before* a first `CompilePredicate`/`Compile` call — only receive one as that call's return value.
The two use cases are therefore genuinely different and must not be conflated:

- **Pre-compile "have I already compiled this?" lookup**, inside one long-lived, immutable
  `CelEnvironment` instance: key a caller-owned cache by the raw expression **source text**
  (a `string`, cheap and available before any compile call), *scoped to one compilation kind*.
  One `CelEnvironment` fixes schema, profile, and limits for its entire lifetime, but it does not
  fix the choice between `CompilePredicate` and `Compile` — the same source text compiled both ways
  from the same environment produces two different results (`CelCompiledPredicate` vs.
  `CelCompiledExpression`, distinguished by `CelCompilationKey.RequiredResultType`). Source text
  alone is therefore a sufficient key only when the cache itself is already scoped to one
  compilation kind — in practice, a distinct typed dictionary per kind (e.g.
  `Dictionary<string, CelCompiledPredicate>`, which cannot silently accept a `Compile()` result — a
  CLR type-safety property, not just a convention) or, if a caller genuinely needs one shared cache
  for both, a `(source, requiredResultType)` composite key instead of source text alone.
- **Post-compile semantic-identity verification**, e.g. confirming two compiled results (possibly
  from different `CelEnvironment` instances, or produced at different times) are truly equivalent
  before treating them as interchangeable, or invalidating a cache when an environment's schema or
  limits change: use the returned **`CelCompilationKey`**, which is available only after compiling
  and encodes source, profile, schema, result type, and both limit identities structurally.

| Field | Details |
|---|---|
| Classification | Optimization / product concern |
| Intended owner | Callers, using a source-text-keyed cache scoped to one `CelEnvironment` for pre-compile lookup, and `CelCompilationKey` for post-compile identity verification — or a future `ArchLinterNet.CEL.Caching` helper that packages this pattern |
| Existing seam | `CelCompilationKey` is public and structurally comparable, but is only obtainable as a `CompilePredicate`/`Compile` return value — it cannot serve as a pre-compile lookup key (see above). Callers supply their own source-text-keyed cache for that purpose. |
| New profile version required? | No — caching is a caller concern |
| Affected layers | None in the engine; `CelCompilationKey` provides the post-compile identity |
| Safety implications | Serialized expressions must include profile + schema identity to avoid cache poisoning across profile versions |
| Prohibited shortcut | Do NOT add any static mutable cache, `ConcurrentDictionary<CelCompilationKey, ...>`, or thread-static cache to `ArchLinterNet.CEL` |
| Direction | Plausible future work (caching helper); serialization requires compatibility rules before any story |

### Additional direction: Diagnostics and explainability

Capabilities deferred: evaluation trace/explain, intermediate value capture, step-by-step debug output.

| Field | Details |
|---|---|
| Classification | Tooling / product concern |
| Intended owner | `ArchLinterNet.CEL` (behind explicit bounded opt-in API in a future profile) |
| Existing seam | `CelEvaluationResult.Diagnostics` carries post-evaluation diagnostics; a future `CelEvaluationOptions.EnableTrace` flag could add trace entries without changing the core result shape |
| New profile version required? | No — tracing is additive; but the trace data shape must be approved before exposing |
| Affected layers | Evaluator (trace collection), `CelEvaluationResult` (trace payload) |
| Safety implications | Traces can expose intermediate data; must be opt-in and bounded |
| Prohibited shortcut | Do NOT expose engine-internal mutable state or `CelEngine` internals as part of any trace API. Do NOT add trace collection that is always-on (must be opt-in to avoid performance impact) |
| Direction | Plausible future work; requires concrete product story |

______________________________________________________________________

## 9. Extension governance checklist

Every future extension proposal touching `ArchLinterNet.CEL` — a new built-in, a new profile
version, a host adapter, a tooling feature, or an execution-backend change — must answer all 10
questions below before implementation begins. A proposal that cannot answer one of these questions
is not ready for an architecture decision, let alone a PR.

1. Is it standard CEL, a canonical CEL extension, a host adapter, tooling, or an ArchLinterNet
   product concern?
1. Which assembly/package owns it?
1. Does it require a new profile version?
1. Does it change syntax, typing, evaluation semantics, limits, diagnostics, or cache identity?
1. How is purity, determinism, termination, and bounded complexity preserved?
1. How is host escape prevented?
1. Which conformance fixtures and adversarial tests are required?
1. Which public/internal documentation changes are required?
1. Is the public API affected, and how is compatibility protected?
1. Can the change be implemented through an existing seam, or is an architecture decision
   required first?

An architecture decision required by question 10 goes through the repository's OpenSpec workflow
(`openspec/`), not a quiet code change — matching the rule already stated in
[Prohibited shortcuts](#prohibited-shortcuts) below.

______________________________________________________________________

## Prohibited shortcuts

The following patterns are permanently prohibited. A change that introduces any of them requires a new architecture decision approved through the OpenSpec workflow — not a quiet deviation.

| Prohibited shortcut | Reason |
|---|---|
| CLR reflection in `CelValue`, `CelContextSchema`, or any public CEL type | Violates host-isolation boundary; breaks CEL's type-safety contract |
| Static mutable registry or global cache in `ArchLinterNet.CEL` | Breaks thread-safety, testability, and cache-lifetime ownership |
| Public `RegisterFunction` or mutable function registry of any kind | User-defined functions are excluded from v1; any registration API exposes attack surface |
| Raw delegate or `Expression<T>` as a compilation result | Bypasses limits and diagnostics; enables unbounded execution |
| Exposing parser, binder, or evaluator internal types as public API | Leaks implementation details that would have to be versioned forever |
| `dynamic` or `object`-typed inputs to `CelValue` or `CelContextSchema` | Defeats nullable analysis, type safety, and the explicit-schema contract |
| `InternalsVisibleTo` for `ArchLinterNet.Core` in `ArchLinterNet.CEL.csproj` | Core must consume only the public assembly surface |
| Accepting a POCO or CLR type directly as a `CelObjectValue` member | Object values must be constructed from typed `CelValue` instances |
| Grammar gate relaxation without a new `CelProfile` | Silently accepting unsupported CEL syntax under Profile v1 would break the stability guarantee |
| Bypassing `CelEvaluationLimits` in any evaluation path | No unbounded execution path may exist |

______________________________________________________________________

## Versioning and compatibility policy

### Profile versioning

`CelProfile.V1` (ID `arch-linter/cel/v1`) is frozen: no syntax, typing, or evaluation-semantics
change may land under the v1 profile identity once shipped. Any capability from section 3
(language/profile evolution) or a new built-in from section 5 that changes what a Profile v1
expression means requires introducing a new `CelProfile` (e.g. `arch-linter/cel/v2`) with its own
ID, rather than silently changing what `CelProfile.V1` accepts. A caller that compiled against
`CelProfile.V1` must keep getting `CelProfile.V1` semantics for the lifetime of the package's
major/minor version line.

### Package release versioning

`ArchLinterNet.CEL` follows Semantic Versioning 2.0, calculated by the manual release workflow
from git tags — see `docs/reference/release-process.md` for the full version-calculation rules and
release scenarios (`preview`, `patch`, `minor`, `major`). Profile v1 semantics themselves are never
a version-bump lever — per "Profile versioning" above, `CelProfile.V1` is permanently frozen, and no
release, however versioned, may ship a change to what an already-shipped `CelProfile.V1` expression
means. Pre-1.0, breaking changes to the public API surface (see section 2) are expected to
accompany at least a `minor` version bump, given the `0.x` preview status noted in the package
README. Introducing a new `CelProfile` version (e.g. `arch-linter/cel/v2`) is additive at the
profile-identity level (existing profile IDs keep working) but is itself a new supported public
capability, so it accompanies at least a `minor` version bump; it becomes a breaking-change release
only if it also changes a previously-shipped public API shape.

### API compatibility baseline

`tests/ArchLinterNet.CEL.Tests/CelPublicApiSurfaceApprovalTests.cs` is the current, enforced
compatibility baseline mechanism: a reflection-based approval test that enumerates every public and
public-nested type, member, and signature in the `ArchLinterNet.CEL` assembly and compares it
against a committed baseline file. Any addition, removal, or signature change to the public API
surface fails this test, forcing an explicit, reviewed baseline update in the same PR — this is the
gate a section-2 exclusion or section-9 governance-checklist question 9 ("is the public API
affected") resolves against today. This is a hand-rolled, reflection-based mechanism, not
`Microsoft.DotNet.ApiCompat` or a NuGet-package-diffing tool; adopting a dedicated API-compat tool
is plausible future work but is not required while the package remains pre-1.0.

______________________________________________________________________

## Comparative CEL implementation review

The public API shape was derived from a comparative review of seven existing CEL implementations,
as required by [#324](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/324). Each
row records what was adopted, what was rejected, why, and which `ArchLinterNet.CEL` public type
carries the decision.

| Implementation | Lifecycle / API shape | Adopted pattern | Rejected pattern | Rationale | Affected public type(s) |
|---|---|---|---|---|---|
| **cel-java** (google/cel-java) | Immutable `CelCompiler`/`CelRuntime` built by builders; compile → plan → evaluate; structured `CelValidationResult` | Builder-constructed immutable environment; compile-once/evaluate-many; structured compilation result with diagnostics instead of exceptions | `Program.eval(Map<String,Object>)` accepting raw maps of arbitrary Java objects | Raw-object activation reintroduces reflection and defeats the closed value model | `CelEnvironment`, `CelEnvironmentBuilder`, `CelCompilationResult<T>` |
| **cel-go** (google/cel-go) | `cel.Env` with declarations; `Compile`/`Program` split; `EvalOptions` cost limits; `Ast` exposed publicly | Environment-owned declarations (context schema); explicit cost/step budgets as first-class API (`CelEvaluationLimits`) | Public `Ast` type and `env.Extend` runtime environment extension | Public AST becomes a forever-versioned contract; runtime extension breaks environment immutability | `CelContextSchema`, `CelEvaluationLimits`, `CelEnvironment` (no `Extend`, no public AST) |
| **Project Nessie cel-java bindings** | Thin wrapper pinning script/declaration pairs; caches compiled scripts keyed by expression text | Caller-owned caching of compiled programs | Cache keyed by expression text alone | Same text under different schemas/limits produces different programs; text-only keys cause wrong-hit bugs | `CelCompilationKey` (source + profile + schema + result type + both limit identities) |
| **Cel.NET** (early .NET port) | Direct port of Go structure; `object`-based value model; reflection-driven member access | Confirmation that a .NET CEL engine is feasible | `object`-typed values and reflection-driven member binding | Violates the no-reflection boundary; nullable analysis and AOT suffer; type errors surface at evaluation instead of compile time | `CelValue` (closed factory + typed accessors), `CelObjectSchema` (declared members) |
| **TELUS cel-dotnet** | Interpreter-style evaluate(text, context) one-shot API | Nothing structural | One-shot `Evaluate(string, IDictionary)` combining parse+bind+eval per call | Re-parses on every evaluation; incompatible with compile-once/evaluate-many linting workloads; no cache identity possible | `CelCompiledPredicate`/`CelCompiledExpression` (separate compiled artifacts) |
| **cel-compiled experiments** (compiled-delegate approaches) | Compile CEL to IL/delegates for speed | Goal of zero re-parse on warm path | Emitting delegates/`Expression<T>` as the public compilation output | Delegates bypass evaluation budgets and diagnostics; prohibited-shortcut list forbids this permanently | `CelCompiledPredicate` (opaque, non-convertible), prohibited-shortcuts table |
| **celdotnet** (community port) | Partial grammar; silent acceptance of unsupported syntax; ad-hoc error strings | Nothing structural | Accepting unsupported syntax with best-effort semantics; string-typed error reporting | Silent grammar drift breaks profile stability; display-string errors are not a machine contract | `CelProfile` (explicit versioned subset), `CelDiagnostic` (stable codes + structured parameters) |

Cross-cutting conclusions embedded in the API:

- Every reviewed implementation that exposed raw host objects (cel-java maps, Cel.NET reflection)
  pushed type errors to evaluation time; ArchLinterNet.CEL rejects host objects at the API
  boundary instead (`CelValue` factories, schema-declared object members).
- No reviewed implementation encoded evaluation policy in its cache identity; Nessie's text-keyed
  cache demonstrated the resulting wrong-hit class of bugs, which `CelCompilationKey` closes by
  including profile, schema, result-type, compilation-limits, and evaluation-limits identities.
- go/java's environment-extension APIs were the main source of thread-safety caveats in their
  docs; `CelEnvironment` is closed at `Build()` with defensive copies for that reason.

______________________________________________________________________

## Performance and allocation baselines

Task #168 added `benchmarks/ArchLinterNet.CEL.Benchmarks`, a BenchmarkDotNet suite covering
environment/schema construction, staged tokenize/parse/bind cost, the full public compilation
pipeline, context construction (stable-handle vs. name-based), compile-once/evaluate-many
evaluation broken out by operator/built-in category, `CelCompilationKey` cache-identity behavior,
concurrent reuse of one compiled predicate, and deterministic `BudgetExceeded`/`SchemaMismatch`
diagnostic paths. See `benchmarks/ArchLinterNet.CEL.Benchmarks/README.md` for what each class
measures and how to run it (`rtk make benchmark-cel`, optional — not part of `make acceptance`),
and `benchmarks/ArchLinterNet.CEL.Benchmarks/RESULTS.md` for the recorded baseline run (hardware,
runtime, per-class numbers, and allocation findings). Results are inputs to #330's
packaging/reconciliation pass and to #163's Core integration guidance, not a standing performance
gate.

Recorded baseline headlines (see RESULTS.md for full tables and methodology):

- Compile-once/evaluate-many holds: compilation (2.8–8.0 us) is ~9.6×–77× the cost of evaluating the
  resulting compiled predicate (0.1–0.3 us); for the same expression measured both ways, ~9.9×.
- Context-construction's object-vs-primitive gap is two distinct costs, not one — with
  builder-construction isolated from `Set()`+`Build()`: `Set()`+`Build()` together cost ~1.9× more
  for object-typed values than primitive-typed values (variable count held equal on both sides; this
  benchmark suite cannot isolate `Set()`'s own structural validation from `Build()`'s work without
  instrumenting `CelEvaluationContextBuilder` internally), while `CelEvaluationContextBuilder`'s
  *constructor* costs ~13.8× more when the environment has a registered object-schema catalog,
  because it uncachedly recomputes `schema.ComputeEnvironmentIdentity(...)` on every call. The
  larger factor is a construction-time cost, not a `Set()`/`Build()` cost. The measured name-based
  vs. stable-handle `Set()` overhead (~0.9%) was within this baseline's `ShortRun`-job noise floor —
  not a reliable magnitude claim.
- `CelCompilationKey` cannot serve as a pre-compile cache-lookup key through the public API (its
  identity components are internal); a caller-owned cache should key by source text instead,
  scoped to one compilation kind (`CompilePredicate` vs. `Compile` — see the "Caching and
  serialization" row above for why source text alone is not enough across both) — for the same
  expression, a cache hit that way is ~145× faster than a full miss-and-populate path (15.8 ns vs.
  2.29 us) and allocates nothing. Note also that `CelCompilationKey.GetHashCode()` itself is not
  cheap (~394 ns, since it string-hashes four separate identity components) — a further reason to
  prefer a source-text-keyed cache over one keyed by `CelCompilationKey`.
- Batch evaluation shows genuinely linear-scaling GC pressure — measured at three batch sizes
  (100/1,000/10,000 independent contexts against one compiled predicate), not asserted from a
  single point: allocation scales exactly linearly (10.00× per 10× batch-size step) and
  per-evaluation allocation (~696 B) and time (~305–326 ns) both stay constant across the whole
  100× range — no batch-specific hotspot emerged.
- Repeated evaluation performing no parser/binder/type-checker work is instrumented, not just
  asserted: `CelEvaluateCallGraphNeverReachesCompilePipelineTests` statically walks the CIL call
  graph reachable from all four `Evaluate` overloads (explicit-limits and safe-default, on both
  compiled types) and proves it never reaches the tokenizer, parser, or binder — fail-closed on any
  unresolved call-graph edge, on `calli` (a function-pointer call with no method token), and
  conservative for indirect dispatch: virtual/interface calls follow every override or
  implementation (implicit or explicit) of the resolved method found in the `ArchLinterNet.CEL`
  assembly, since the IL token alone cannot say which one actually runs; delegate invocations
  (`Invoke`/`BeginInvoke`/`EndInvoke` on any `Delegate` type, which carry no target information at
  the call site at all) are resolved by scanning the assembly for `ldftn`/`ldvirtftn` construction
  sites matching the delegate's signature — the pattern `CelEvaluator`'s own comparison/projection
  delegates use — and reported as unresolved if no matching construction site exists anywhere in
  the assembly, rather than silently treated as a dead end.

## References

- Parent story: [#322](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/322)
- This document: [#324](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/324)
- Implementation tasks that depend on the decisions above: #325 (tokenizer/parser), #326 (binder/type system), #327 (built-in function catalog execution), #328 (bounded evaluator/runtime semantics), #329 (compilation pipeline/cache identity), #330 (packaging/release readiness/reconciliation)
- Performance and allocation baselines: [#168](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/168)
- Core integration consuming the public API: [#163](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/163)
- Policy expression model using the public API: [#162](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/162)
