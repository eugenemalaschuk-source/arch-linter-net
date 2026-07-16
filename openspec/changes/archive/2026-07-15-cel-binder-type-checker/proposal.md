## Why

`cel-profile-v1` (#324) normatively fixes the Profile v1 type system: scalar/list/map/object
types, the closed operator signature table, the closed built-in function catalog, required-result-
type enforcement, and the `BindingError`/`TypeMismatch`/`SchemaMismatch` diagnostic codes. #325
(#336) implemented the tokenizer/parser so syntactically valid Profile v1 expressions produce a
`CelSyntaxNode` tree, but `CelEnvironment.CompilePredicate`/`Compile` still return a blanket
`NotYetImplemented` diagnostic for every syntactically valid expression — no identifier, member,
index, or call is actually resolved, and no operator or function signature is actually checked.
#326 implements the binder and static type checker so `CompilePredicate`/`Compile` produce a real,
diagnostic-carrying compilation result for every syntactically valid Profile v1 expression, without
yet performing evaluation (#327). The existing spec already normatively fixes what must be checked
(type rules, function catalog, error codes); this change adds the implementation-scope details the
existing spec left unstated (bound-plan shape, diagnostic parameter/category conventions for the
binder, whole-AST binding behavior for short-circuited branches) so the spec and
`docs/internal/cel-engine-architecture.md` stay traceable to what actually ships.

## What Changes

- New internal `ArchLinterNet.CEL.Binding` namespace (sibling to `Parsing`/`Compilation`/
  `Evaluation`, never public — consistent with the "no public AST/tooling model" constraint in
  `docs/internal/cel-engine-architecture.md`):
  - A binder that walks the entire `CelSyntaxNode` tree from a successful `CelParser.Parse`,
    including branches unreachable via runtime short-circuiting (e.g. both operands of `&&`/`||`,
    both branches condition-adjacent to `in`).
  - Identifier resolution against the compilation's `CelContextSchema`; member resolution against
    `CelObjectSchema` declarations; no reflection, no dynamic dispatch, no CLR type discovery.
  - Index-expression type checking: `List<T>[Int] -> T`, `Map<String,T>[String] -> T`; any other
    index/key type is a compile-time `TypeMismatch`.
  - Call resolution against the closed Profile v1 built-in function catalog only
    (`String.startsWith/endsWith/contains(String)->Bool`, `String.size()->Int`, `List.size()->Int`,
    `Map.size()->Int`, `Map.containsKey(String)->Bool`); no user-registered functions.
  - Operator type checking for `CelUnarySyntax`/`CelBinarySyntax` against the closed Profile v1
    signature table (logical `!`/`&&`/`||`; same-kind structural `==`/`!=`; same-kind numeric
    ordering `< <= > >=` with no implicit `Int`/`Float` widening; `in` over `List<T>`/`Map<String,T>`).
  - Required-result-type enforcement: `CelRequiredResultType.Predicate` requires the root
    expression's static type to be `Bool`; `General` allows any `CelTypeKind`.
  - An immutable internal "bound expression" representation — one node per `CelSyntaxNode`, each
    carrying its resolved `CelType` — held only by `CelCompiledPredicate`/`CelCompiledExpression`
    and never exposed publicly.
  - Structured `CelDiagnostic` production using the already-reserved `BindingError` (unresolved
    identifier/member/function), `TypeMismatch` (operator/function/index/result-type violations),
    and `SchemaMismatch` (undeclared object member) codes, following the same category-scoped
    diagnostic-builder pattern as `Parsing/CelParseDiagnostics.cs`.
- `CelEnvironment.CompilePredicate`/`Compile` now run the binder after a successful parse. Binder
  success replaces today's `NotYetImplemented(key)` with a real `CelCompiledPredicate`/
  `CelCompiledExpression` (still `Evaluate()`-stubbed, per #327). Binder failure returns a
  `CelCompilationResult<T>` carrying the structured diagnostic(s) instead of `NotYetImplemented`.
- New focused binder/type-checker tests plus negative-conformance tests (unresolved identifiers,
  unresolved members, unresolved functions, wrong index/key types, wrong operator operand types,
  mixed-kind numeric comparison, wrong required result type, hidden-branch violations) in
  `tests/ArchLinterNet.CEL.Tests/`.
- `docs/internal/cel-engine-architecture.md` updated to record actual binder ownership and the
  scope decisions this change locks in (see design.md).

## Capabilities

### Modified Capabilities

- `cel-profile-v1`: Adds ADDED requirements pinning the binder/type-checker implementation scope
  (bound-plan shape, whole-AST binding over short-circuited branches, binder diagnostic category
  and parameter conventions) that the existing spec left as an implementation detail. No existing
  requirement, scenario, or public API shape changes — the type rules, function catalog, and error
  codes were already normatively specified by #324/#325.

## Impact

- **`src/ArchLinterNet.CEL/Binding/`**: new internal-only namespace (binder, bound-expression
  representation, binder diagnostics).
- **`src/ArchLinterNet.CEL/CelEnvironment.cs`**: wires the binder into the two compile entry
  points; no public signature change.
- **`tests/ArchLinterNet.CEL.Tests/`**: new binder/type-checker test files.
- **`docs/internal/cel-engine-architecture.md`**: binder ownership section updated to reflect
  shipped scope.
- **Downstream #327 (evaluator)**: will consume the internal bound plan; no public API is added
  for it to depend on.
- No public API surface change — `ArchLinterNet.CEL.Binding` is internal; external consumers see
  only real diagnostics (or eventually a real compiled program) from the same
  `CompilePredicate`/`Compile` entry points.
