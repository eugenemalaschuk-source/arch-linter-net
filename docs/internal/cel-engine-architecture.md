# CEL Engine Architecture Blueprint

This is internal project documentation for maintaining the `arch-linter-net` repository.
It is intentionally excluded from the public MkDocs/GitHub Pages product site.

This document defines the initial target architecture of `ArchLinterNet.CEL` before any implementation tasks (#325–#329) begin. It is the design/spec slice tracked by #324.

Tasks #325–#329 must maintain this blueprint as implementation decisions are made. Task #330 must reconcile and finalize it against shipped code.

______________________________________________________________________

## Processing pipeline

```text
expression source (string)
        │
        ▼
  ┌─────────────────┐
  │   Tokenizer     │  (task #325 — parser)
  │                 │  Converts source bytes to a token stream.
  │  Limits:        │  Enforces MaxExpressionLength before tokenizing.
  └────────┬────────┘
           │  token stream
           ▼
  ┌─────────────────┐
  │  Parser / AST   │  (task #325)
  │  (internal)     │  Builds an internal syntax representation.
  │                 │  MaxNestingDepth enforced here.
  │  NOT public.    │  AST nodes are never exposed.
  └────────┬────────┘
           │  internal syntax tree
           ▼
  ┌─────────────────┐
  │  Binder /       │  (task #326 — type system)
  │  Type Checker   │  Resolves identifiers against CelContextSchema.
  │                 │  Checks types against profile function catalog.
  │                 │  MaxIdentifierCount enforced here.
  │                 │  Produces structured CelDiagnostic on failure.
  └────────┬────────┘
           │  immutable bound plan
           ▼
  ┌─────────────────┐
  │  Bound Plan     │  Internal representation of a fully-checked expression.
  │  (internal)     │  Owned by CelCompiledPredicate / CelCompiledExpression.
  │                 │  Never exposed publicly.
  └────────┬────────┘
           │
    (compilation complete — CelCompiledPredicate / CelCompiledExpression returned)
           │
           ▼  (evaluation, triggered by .Evaluate(context, limits))
  ┌─────────────────┐
  │  Bounded        │  (task #327 — evaluator)
  │  Evaluator      │  Walks the bound plan against a CelEvaluationContext.
  │                 │  Enforces MaxIterations and MaxCostUnits per call.
  │                 │  Produces CEL short-circuit semantics for && / ||.
  └────────┬────────┘
           │  typed result or failure diagnostics
           ▼
  ┌─────────────────┐
  │  CelEvaluation  │  Public result surface.
  │  Result         │  IsSuccess / Value / Diagnostics.
  └─────────────────┘
```

______________________________________________________________________

## Component ownership

| Component | Owner | Notes |
|---|---|---|
| Profile identity (`CelProfile`, `CelProfileId`) | `ArchLinterNet.CEL` public | Stable across versions; v1 ID is `arch-linter/cel/v1` |
| Grammar gates (token set, operator set) | `CelEngine` internal | Controlled per-profile; no public API |
| Value model (`CelValue`, `CelObjectValue`, `CelValueKind`) | `ArchLinterNet.CEL` public | No CLR reflection; all factories are typed |
| Type descriptors (`CelType`, `CelTypeKind`) | `ArchLinterNet.CEL` public | Static factories only |
| Context schema (`CelContextSchema`, `CelVariable`) | `ArchLinterNet.CEL` public | Structural identity is deterministic |
| Function catalog | `CelEngine` internal | Declared per-profile; immutable; no public registration |
| Bound operations (bound plan, binding tables) | `CelEngine` internal | Never exposed |
| Evaluation budgets (`CelCompilationLimits`, `CelEvaluationLimits`) | `ArchLinterNet.CEL` public | SafeDefaults provided; no unbounded path |
| Compiled programs (`CelCompiledPredicate`, `CelCompiledExpression`) | `ArchLinterNet.CEL` public | Immutable; thread-safe; hold bound plan internally |
| Diagnostics (`CelDiagnostic`, `CelDiagnosticCode`, `CelSourceSpan`) | `ArchLinterNet.CEL` public | Stable codes; message is display-only |
| Cache identity (`CelCompilationKey`) | `ArchLinterNet.CEL` public | Deterministic; caller-owned cache lifetime |

______________________________________________________________________

## Extension-direction matrix

For each capability excluded from Profile v1, this matrix records: classification, intended owner, existing seam, whether a new profile version is required, affected pipeline layers, safety/complexity implications, prohibited shortcut, and whether the direction is plausible future work or a permanent non-goal.

### 1. Language and profile expansion

Capabilities deferred: arithmetic (`+`, `-`, `*`, `/`, `%`), conditional expression (`? :`), new literals (uint, bytes), timestamp/duration types, optional/null support, comprehensions/macros (`all`, `exists`, `map`, `filter`), regex (`matches`), protobuf integration, unknown/partial evaluation.

| Field | Details |
|---|---|
| Classification | Standard CEL (normative spec features) |
| Intended owner | `ArchLinterNet.CEL` public API (new profile version) |
| Existing seam | `CelProfile` identity gate in `CelEngine` grammar; type-system additions in task #326 |
| New profile version required? | Yes — Profile v2+ adds these; Profile v1 semantics remain frozen |
| Affected layers | Tokenizer, parser, type-checker, evaluator, function catalog, diagnostics |
| Safety implications | Arithmetic can overflow; timestamp/duration parsing can be malformed; regex can cause ReDoS if unbounded |
| Prohibited shortcut | Do NOT add unsupported operators by relaxing the grammar gate without a new profile. Do NOT accept a user expression containing arithmetic in Profile v1 and silently discard the unsupported sub-tree |
| Direction | Plausible future work per approved profile stories |

### 2. Host adapters

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

### 3. Function catalog

Capabilities deferred: caller-defined functions, host-registered function bundles, operator overloads.

| Field | Details |
|---|---|
| Classification | Standard CEL (built-in) + canonical extension (host-defined) |
| Intended owner | `CelEngine` internal (built-in functions); future profiles may add more built-ins |
| Existing seam | Internal function-catalog lookup inside `CelEngine`; profile gates which functions are available |
| New profile version required? | Adding new built-ins requires a new profile; host-defined functions require explicit API design (excluded from v1) |
| Affected layers | Binder, type-checker, evaluator |
| Safety implications | User-defined functions could execute arbitrary code; excluded from v1 for this reason |
| Prohibited shortcut | Do NOT add a public `RegisterFunction(...)` API or any mutable function registry — not even a static one |
| Direction | Built-in expansion is plausible (new profile); host-defined functions are a permanent non-goal for the isolated CEL library (Core/product layer may wrap) |

### 4. Execution backends

Capabilities deferred: optimized planner, JIT-style compiled backend, alternate interpreter.

| Field | Details |
|---|---|
| Classification | Optimization |
| Intended owner | `CelEngine` internal; pluggable behind the non-public bound plan |
| Existing seam | `CelCompiledPredicate.Evaluate` / `CelCompiledExpression.Evaluate` delegate to an internal evaluator held by the compiled program; the evaluator is never exposed |
| New profile version required? | No — backend swap is transparent to consumers |
| Affected layers | Bound plan representation, evaluator |
| Safety implications | New backend must preserve CEL short-circuit semantics and budget enforcement |
| Prohibited shortcut | Do NOT expose the bound plan or evaluator interface publicly. Do NOT allow raw delegate escape (e.g., `Func<CelEvaluationContext, bool>` as a public compilation result) |
| Direction | Plausible future work; task #329 covers performance |

### 5. Tooling and AST

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

### 6. Caching and serialization

Capabilities deferred: caller-owned cache helper implementations, portable checked-expression format.

| Field | Details |
|---|---|
| Classification | Optimization / product concern |
| Intended owner | Callers (using `CelCompilationKey`) or a future `ArchLinterNet.CEL.Caching` helper |
| Existing seam | `CelCompilationKey` is public and structurally comparable; callers supply their own cache |
| New profile version required? | No — caching is a caller concern |
| Affected layers | None in the engine; `CelCompilationKey` provides the identity |
| Safety implications | Serialized expressions must include profile + schema identity to avoid cache poisoning across profile versions |
| Prohibited shortcut | Do NOT add any static mutable cache, `ConcurrentDictionary<CelCompilationKey, ...>`, or thread-static cache to `ArchLinterNet.CEL` |
| Direction | Plausible future work (caching helper); serialization requires compatibility rules before any story |

### 7. Diagnostics and explainability

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

## References

- Parent story: [#322](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/322)
- This document: [#324](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/324)
- Implementation tasks that depend on the decisions above: #325 (tokenizer/parser), #326 (type system), #327 (evaluator), #328 (conformance), #329 (performance), #330 (reconciliation/finalization)
- Core integration consuming the public API: [#163](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/163)
- Policy expression model using the public API: [#162](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/162)
