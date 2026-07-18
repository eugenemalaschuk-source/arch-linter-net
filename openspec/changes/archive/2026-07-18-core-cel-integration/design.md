## Context

`ArchLinterNet.CEL` is a complete, immutable, thread-safe CEL engine (`CelEnvironment`,
`CelContextSchema`/`CelObjectSchema`, `CelValue`/`CelObjectValue`,
`CelCompiledPredicate`, `CelDiagnostic`) that Core already has a `ProjectReference` to
but never calls. `openspec/specs/cel-policy-model/spec.md` (#162) already normatively
fixes: the seven allowed `when` locations, the three closed context shapes (`subject`;
`source`; `source`+`target`+`dependency`), the 16-member shared subject/source/target
object shape, the 4-member `dependency` object shape, and fail-closed compile/evaluate
semantics. This design does not redefine any of that — it implements it.

`docs/internal/core-architecture-blueprint.md` fixes Core's internal module boundaries:
`Contracts` depends on nothing else in Core (pure schema) and must not depend on
`Execution`; `Execution` may depend on `Contracts`. `ArchitecturePolicyDocumentLoader.Load`
runs an ordered `IArchitecturePolicyDocumentValidator` pipeline
(`ArchitecturePolicyDocumentValidatorPipeline.All`) after deserialization — this is the
documented extension point for load-time validation, and every existing validator in it
throws a plain `InvalidOperationException` that the loader enriches with policy-location
context via `ArchitecturePolicyProvenanceIndex`.

`ArchitectureContextSelector` (`role`+`metadata` today) is reused by four contract
shapes: `strict_context_dependencies`/`strict_context_allow_only` (in scope for `when`
per #162) and `strict_port_boundaries`/its `adapter_bindings` (explicitly NOT in the
first-wave `when` location list). Any change to this shared type or its raw-YAML
key-validation helpers must not accidentally open `when` on the port-boundary family.

## Goals / Non-Goals

**Goals:**
- Compile every `when` field at policy-load time through the public `ArchLinterNet.CEL`
  API, against the exact context schema `cel-policy-model` specifies for that location.
- Fail policy loading, with an actionable message, on any compile diagnostic — before
  any contract executes.
- Cache each compiled predicate on the selector node that owns it, so evaluation
  (added later, by #164) never reparses.
- Provide typed-context-factory and evaluation-result-wrapper building blocks in
  `Execution` for #164 to consume, without performing any selector/contract wiring.
- Keep literal-only policies (`when` absent) free of any CEL engine construction.

**Non-Goals:**
- Implementing CEL parsing/binding/evaluation semantics (owned by `ArchLinterNet.CEL`).
- Evaluating `when` against real candidates during contract execution (#164).
- Changing `ArchitectureContextSelectorMatcher`, `ArchitectureAnalysisSession` checking
  logic, or any contract-family checker.
- Opening `when` on port-boundary/adapter-binding selectors.
- Public docs/examples (#161 story-level non-goal).

## Decisions

### D1: Field-level compiled-predicate cache on the selector node, not a session dictionary

`ArchitectureLayerSelector`/`ArchitectureContextSelector` gain a `[YamlIgnore] internal
CelCompiledPredicate? CompiledWhen { get; internal set; }` alongside the new `When`
YAML field, mirroring the existing `ArchitectureLayer._cachedGlobPattern`/`GlobPattern`
lazy-field idiom. The compiling validator sets it once during `Load()`.

**Alternative considered**: a `Dictionary<CelCompilationKey, CelCompiledPredicate>`
owned by `ArchitectureContractDocument` or `ArchitectureAnalysisSession`. Rejected:
`CelCompilationKey` already carries the full cache-identity (source + profile +
schema identity + result type + limits identity — see `ArchLinterNet.CEL`'s own
doc comment: "cache lifetime is caller-owned"); a document-level dictionary would
require plumbing lookups back through every YAML node instead of just reading a field
on the object already at hand, and the issue's "cache per loaded policy" requirement
does not require cross-node dedup of identical expression text — only that no
candidate-by-candidate reparse happens during evaluation. The field-level cache
satisfies that with the least new surface area, is trivially session-isolated (each
`Load()` deserializes fresh selector instances — no static state anywhere), and matches
an idiom this codebase already uses for exactly this kind of "parse once, reuse many"
case.

### D2: Shared static schema/environment definitions live in `Contracts.Expressions`

`CelEvaluationContextBuilder.Set(CelVariable, CelValue)` requires the exact `CelVariable`
handle instances from the `CelContextSchema` a predicate was compiled against (checked
by reference equality). Both the load-time compiler (must live in `Contracts`, since
`Contracts` owns `ArchitecturePolicyDocumentLoader`/its validator pipeline and must not
depend on `Execution`) and the future evaluation-time context builder (must live in
`Execution`, since only `Execution` may depend on `Model`/`Scanning` fact types) need
the identical schema/variable instances. Since `Execution` is allowed to depend on
`Contracts` (not the reverse), the single source of truth —
`ArchLinterNet.Core.Contracts.Expressions.ArchitectureExpressionSchemas` — lives in
`Contracts`. It depends only on `ArchLinterNet.CEL` (an external package reference, not
an internal Core module — does not violate "Contracts depends on nothing else in Core").
The three `CelEnvironment`s it exposes are `static readonly`: this is a deterministic,
stateless, closed catalog (identical every process run, like `CelProfile.V1` itself),
not the kind of "static mutable cache shared across unrelated policy sessions" the
issue prohibits — no expression-specific data is ever stored on it.

### D3: Compile-time errors are plain `InvalidOperationException`, not a new diagnostic type

Every existing `IArchitecturePolicyDocumentValidator` (`ContextualContractValidator`,
`LayerNamespacesValidator`, etc.) throws `InvalidOperationException` with a descriptive
message; `ArchitecturePolicyDocumentLoader.Load` catches it and enriches it via
`ArchitecturePolicyProvenanceIndex.EnrichValidationException`/`SetValidationSubject`/
`Track` for composed-policy location context. The new
`ExpressionCompilationValidator` follows this exact pattern: it builds a deterministic
message from each `CelDiagnostic` (`Code`, `Message`, `Span` when present) and throws.

**Alternative considered**: introduce an `ArchitecturePolicyDiagnostic`-based exception
(the pattern `ArchitecturePolicyImportException`/`ArchitecturePolicyDiagnosticFactory`
use). Rejected: that pattern is import-resolution-specific (its error category enum —
`MissingFile`, `Cycle`, `OutOfBoundary`, etc. — has no CEL-shaped members, and inventing
CEL-specific categories there would duplicate `CelDiagnosticCode`, which already exists
as the machine-readable code). Following the validator-pipeline idiom instead keeps one
error-reporting pattern for one mechanism (the validator pipeline) rather than mixing
two, and every sibling validator's tests already assert against this same shape.

### D4: `ArchitectureContextSelector`'s `when` acceptance is call-site-scoped, not type-scoped

Adding a `When` property to `ArchitectureContextSelector` makes it structurally
available everywhere that type is used — including `ArchitecturePortBoundaryContract`
and `ArchitectureAdapterPortBinding`, which `cel-policy-model`'s closed location list
does not include. `ArchitecturePolicyDocumentLoader`'s raw-YAML key validators
(`ValidateContextualSelectorNodeKeys`, `ValidateContextualSelectorListKeys`) gain a
`bool allowWhen` parameter, defaulting to `false`; only the call sites inside
`ValidateContextualContractGroup` (context-dependency/allow-only groups) pass `true`.
`ValidatePortBoundaryContractGroup`'s call sites are left unchanged (`allowWhen: false`
implicit), so `when` on a port-boundary/adapter-binding selector continues to be
rejected as an unknown property, exactly as every other undeclared key already is.

### D5: Typed context factories take a Core-owned facts DTO, not a live reflection pipeline

`ArchLinterNet.Core.Execution.Expressions.ArchitectureExpressionSubjectFacts`/
`ArchitectureExpressionDependencyFacts` are new records shaped exactly like
`cel-policy-model`'s closed subject/dependency member lists.
`ArchitectureExpressionContextFactory` maps them to `CelValue`/`CelObjectValue`/
`CelEvaluationContext` using the schemas from D2.

**Alternative considered**: build the factory directly from
`ArchitectureDeclaredTypeFact` + `ArchitectureTypeClassificationResult` +
live reflection (`Type.IsAbstract`, base types, interfaces, attributes). Rejected as
out of scope for #163: no existing Core fact-gathering pipeline currently assembles all
16 subject-shape fields (kind/isAbstract/isSealed/baseTypeNames/interfaceTypeNames/
attributeTypeNames require reflection Core doesn't do today outside specific checkers;
dependency-edge kind/viaMethodBody/member names aren't modeled anywhere yet per the
current `ContextDependencyPayload`). Assembling that pipeline is real, non-trivial
scope that belongs with #164's actual selector/contract wiring (where the caller
knows which facts are already at hand for a given candidate). #163's job is the
CEL-value-construction boundary; the DTO's shape is exactly the "documented
architecture facts" contract the issue requires — how it is populated is #164's
concern.

## Risks / Trade-offs

- **[Risk]** A future contract family reusing `ArchitectureContextSelector` might
  forget to explicitly opt into `allowWhen`, silently keeping `when` fail-closed there.
  → **Mitigation**: this is the safe default (fail-closed matches the spec's default
  posture); opting in is one explicit boolean per call site, not a structural barrier.
- **[Risk]** `schema/dependencies.arch.schema.json` duplicating `contextSourceSelector`/
  `contextSelector` into `*WithExpression` variants could drift from the originals over
  time. → **Mitigation**: the duplication is minimal (one added optional `when`
  property); a follow-up JSON-Schema refactor (e.g. `allOf` composition) is possible
  later but not required for correctness now.
- **[Risk]** Field-level `CompiledWhen` mutation on a nominally "pure schema" `Contracts`
  model could be mistaken for a broader precedent to add behavior to models.
  → **Mitigation**: this exactly mirrors the already-reviewed `ArchitectureLayer.GlobPattern`
  lazy-cache idiom; no new precedent.

## Migration Plan

Additive only. Existing policies with no `when` fields are byte-for-byte unaffected
(no new required fields, no behavior change on the literal-only path). No rollback
concerns beyond a normal revert.

## Open Questions

None — `cel-policy-model` already closes every product-facing design question; this
change is implementation against an already-approved spec.
