## Context

`cel-profile-v1` already normatively fixes Profile v1's type system (scalar/list/map/object types,
the closed operator signature table, the closed built-in function catalog, required-result-type
enforcement) and reserves `BindingError`/`TypeMismatch`/`SchemaMismatch` diagnostic codes for this
work. The tokenizer/parser (#325/#336) hands the binder a `CelSyntaxNode` tree on syntactically
valid input; `CelEnvironment.CompilePredicate`/`Compile` currently short-circuit that tree straight
to `NotYetImplemented`. This change's job is purely semantic analysis: resolve names, check types,
produce diagnostics or a bound plan — no evaluation (#327).

Constraints carried over from #324/#325 and `docs/internal/cel-engine-architecture.md`'s permanent
prohibited-shortcuts table: no reflection-based member/function discovery, no `dynamic`, no public
AST/bound-plan type, no static function registry, no `Expression<T>`/delegate escape hatch.

## Goals / Non-Goals

**Goals:**
- Resolve every identifier, member access, index, and call in a successfully parsed `CelSyntaxNode`
  tree against the compilation's `CelContextSchema` / `CelObjectSchema` catalog and the closed
  Profile v1 function catalog.
- Statically type-check every unary/binary operator, index expression, and call against the closed
  Profile v1 signature table, with no implicit numeric widening.
- Enforce the compilation request's required result type (`Predicate` → `Bool`; `General` → any).
- Bind the *whole* AST — both operands of `&&`/`||`, both sides of `in`, every branch — so a
  diagnostic surfaces even for a sub-expression runtime short-circuiting would never evaluate.
- Produce structured `CelDiagnostic`s (`BindingError`/`TypeMismatch`/`SchemaMismatch`) with the same
  category-scoped, `profileId`-carrying shape as `CelParseDiagnostics`.
- On success, produce an immutable internal bound-expression tree (one node per syntax node, each
  carrying its resolved `CelType`) that `CelCompiledPredicate`/`CelCompiledExpression` hold
  internally, replacing today's `NotYetImplemented` stub for valid input.

**Non-Goals:**
- No evaluator/runtime execution (#327) — `Evaluate()` on the compiled types remains
  `NotImplementedException`.
- No new public types beyond what #334 already defined; the bound plan is internal only.
- No new profile version, no arithmetic/ternary/list-map-literal support — those remain
  parser-level `UnsupportedFeature` per #325/#336 and never reach the binder.
- No change to `CelContextSchema`/`CelObjectSchema` identity semantics (already correct from #334).

## Decisions

**Whole-AST binding via unconditional recursive walk, not short-circuit-aware traversal.**
The binder recursively visits every child of every node regardless of operator semantics — e.g. for
`CelBinarySyntax(And, Left, Right)` it always binds both `Left` and `Right`, and if both have
errors, both diagnostics should be discoverable (though only the first-encountered one is returned,
matching the parser's fail-fast single-diagnostic contract per requirement below). Alternative
considered: a short-circuit-aware walker that skips binding a branch once the other branch is known
to determine the result — rejected because it cannot exist at bind time (values aren't known yet)
and would contradict the issue's explicit "whole AST, including branches runtime short-circuiting
may not evaluate" requirement.

**Fail-fast, single diagnostic per compilation, matching the parser's existing contract.**
The binder returns the first diagnostic encountered during a fixed left-to-right, depth-first walk,
mirroring `CelParser`'s fail-fast model and `CelCompilationResult<T>`'s single-diagnostic shape.
Alternative considered: collect all binder diagnostics and return a batch — rejected because
`CelCompilationResult<T>` and the diagnostic contract established by #334/#325 are single-diagnostic
per result, and batching would be a breaking shape change out of this issue's scope.

**Bound-expression tree mirrors `CelSyntaxNode` 1:1, internal-only, in a new `Binding` namespace.**
Each bound node type (e.g. `CelBoundLiteral`, `CelBoundIdentifier`, `CelBoundUnary`,
`CelBoundBinary`, `CelBoundMemberAccess`, `CelBoundIndex`, `CelBoundCall`) carries `Span` (from the
source node) and a resolved `CelType`. `CelIdentifierSyntax`/`CelMemberAccessSyntax` bind to a
`CelVariable`/`CelObjectMember` handle (already public, already stable) rather than a raw name
string, so downstream evaluation (#327) can use the same handle-based fast path the evaluation
context already uses. Alternative considered: skip a distinct bound tree and annotate/decorate the
existing `CelSyntaxNode` in place — rejected because `CelSyntaxNode` is a `Parsing`-owned immutable
record type with no mutation/annotation slot, and mixing binder state into the parser's namespace
would blur the internal ownership boundary the architecture doc establishes per phase.

**Binder diagnostic category `"binder"`, following the `CelParseDiagnostics` pattern exactly.**
A new internal `CelBindDiagnostics` static helper (mirroring `CelParseDiagnostics`) builds
`BindingError`/`TypeMismatch`/`SchemaMismatch`/`BudgetExceeded` diagnostics, always setting
`Category = "binder"` and `Parameters["profileId"]`, plus `identifier` for `BindingError`/
`SchemaMismatch` and `expectedType`/`actualType` for `TypeMismatch`. Alternative considered: reuse
`"parser"` category — rejected, it would make diagnostics from two different compiler phases
indistinguishable by category, contradicting the existing single-purpose-per-category convention.

**No implicit `Int`/`Float` widening anywhere — comparison and equality both require identical
`CelTypeKind` operands.** This mirrors the spec's frozen operator table exactly; the binder performs
a direct `CelTypeKind` equality check with no coercion table. Alternative considered: allow
`Int`/`Float` cross-comparison like some scripting languages — rejected, contradicts the frozen
Profile v1 signature table pinned in `cel-profile-v1` by #324.

**`CelEnvironment` wiring: binder runs unconditionally after a successful parse, inline, no new
public method.** `TryParse`'s existing pipeline (limits → tokenize → parse) gains one more inline
step (bind) before constructing the `CelCompilationResult<T>`. Alternative considered: expose an
internal `CelBinder.Bind(...)` seam callable independently for future tooling — deferred; nothing in
#326's acceptance criteria requires a public/internal seam beyond what `CelEnvironment` itself needs,
and the architecture doc explicitly defers a tooling-facing AST/bound-plan API to a separate future
story.

## Risks / Trade-offs

- **[Risk] Fail-fast single diagnostic can mask multiple independent binder errors in one
  expression, frustrating iterative fixing.** → Mitigation: this exactly matches the existing
  parser's fail-fast contract and `CelCompilationResult<T>` shape; consistency across compiler
  phases is preferred over a scope-expanding multi-diagnostic redesign in this issue.
- **[Risk] A 1:1 bound-tree duplication of `CelSyntaxNode` adds allocation on every successful
  compile, and `ArchLinterNet.CEL` performs no caching itself — every `CompilePredicate`/`Compile`
  call re-tokenizes, re-parses, and re-binds.** → Mitigation: this is the documented, deliberate
  contract from #334 (`CelCompilationKey` supplies deterministic structural identity so a *caller*
  can memoize compiled programs; "No process-global mutable cache SHALL exist in
  `ArchLinterNet.CEL`; cache lifetime SHALL be caller-owned" per the `cel-profile-v1` spec's cache-
  identity requirement). The bound-tree allocation cost is therefore paid once per call a caller
  chooses not to cache, not amortized by the library — callers evaluating the same expression
  repeatedly are expected to hold onto the returned `CelCompiledPredicate`/`CelCompiledExpression`
  themselves, keyed by `CompilationKey` if desired.
- **[Risk] Deep-structural equality for `==`/`!=` over `List`/`Map`/`Object` is a binder *type*
  check only (same `CelTypeKind` shape) — the actual deep comparison is evaluator territory (#327)
  and isn't exercised by this change.** → Mitigation: explicitly called out as a non-goal; tests in
  this change verify only that same-shape `List`/`Map`/`Object` operands type-check as `Bool`, not
  runtime comparison behavior.

## Open Questions

None — the type rules, function catalog, and error codes were already normatively pinned by #324;
this design only fills in implementation-scope details left open by the issue.
