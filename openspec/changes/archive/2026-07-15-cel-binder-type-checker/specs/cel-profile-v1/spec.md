## ADDED Requirements

### Requirement: Binder and static type checker implementation scope for Profile v1

`ArchLinterNet.CEL`'s binder and static type checker (internal `ArchLinterNet.CEL.Binding` namespace) SHALL implement exactly the following implementation scope, in addition to the operator
signature table, built-in function catalog, required-result-type behavior, and error-code catalog
already fixed elsewhere in this spec:

- The binder SHALL run only against a `CelParseResult` with `IsSuccess = true` (a successfully
  parsed `CelSyntaxNode` tree). It SHALL visit every node type `CelParser.Parse` can produce on a
  successful parse: literal nodes, `CelIdentifierSyntax`, `CelUnarySyntax`, `CelBinarySyntax`,
  `CelMemberAccessSyntax`, `CelIndexSyntax`, and `CelCallSyntax`. `CelDeferredSyntax` SHALL never
  appear in a tree the binder receives, since `CelParser.Parse` only returns success when no
  pending deferred-feature diagnostic exists.
- The binder SHALL bind the *whole* syntax tree unconditionally — every child of every node,
  regardless of the parent operator's evaluation-time short-circuit semantics. A binding or
  type-check violation in a sub-expression SHALL be diagnosable even when that sub-expression sits
  in a position (e.g. either operand of `&&`/`||`, either side of `in`) that a future evaluator's
  short-circuit or error-absorbing behavior (defined elsewhere in this spec) might never actually
  evaluate at runtime.
- The binder SHALL follow a fixed left-to-right, depth-first traversal order and SHALL stop at the
  first binding or type-check violation encountered in that order, returning exactly that one
  `CelDiagnostic` — matching the tokenizer/parser's existing fail-fast, single-diagnostic contract
  and `CelCompilationResult<T>`'s single-diagnostic shape. The binder SHALL NOT aggregate or batch
  multiple diagnostics into one compilation result.
- `CelIdentifierSyntax` resolution SHALL look up the name against the compilation's
  `CelContextSchema` and bind to the schema's `CelVariable` handle. An unresolved name SHALL
  produce `BindingError` with `Parameters["identifier"]` set to the unresolved name.
- `CelMemberAccessSyntax` resolution SHALL type-check its receiver first; when the receiver's
  static type is `Object`, it SHALL look up the member name against that object type's
  `CelObjectSchema` and bind to the schema's `CelObjectMember` handle. An undeclared member name
  SHALL produce `SchemaMismatch` with `Parameters["identifier"]` set to the member name. A member
  access on a receiver whose static type is not `Object` SHALL produce `TypeMismatch`.
- `CelCallSyntax` resolution SHALL match the function name, receiver static type (or its absence,
  for a free-function-shaped call), and argument count/types against the closed built-in function
  catalog fixed elsewhere in this spec. A name that matches no catalog entry at all SHALL produce
  `BindingError`; a name that matches a catalog entry but with a wrong receiver type, argument type,
  or arity SHALL produce `TypeMismatch` for a type mismatch or `BindingError` for an arity mismatch,
  exactly as this spec's function-catalog table already specifies per function.
- `CelIndexSyntax` resolution SHALL type-check its receiver first; a `List` receiver SHALL require
  an `Int`-typed index expression and resolve to the list's element type, a `Map` receiver SHALL
  require a `String`-typed index expression and resolve to the map's value type, and any other
  receiver type or wrong index/key expression type SHALL produce `TypeMismatch`.
- Operator type checking for `CelUnarySyntax`/`CelBinarySyntax` SHALL apply the frozen signature
  table fixed elsewhere in this spec with no implicit `Int`/`Float` widening; a signature violation
  SHALL produce `TypeMismatch` carrying `Parameters["expectedType"]` and `Parameters["actualType"]`.
- The compiled result's required result type (`CelRequiredResultType.Predicate` or `General`) SHALL
  be checked against the root expression's resolved static type only after the entire tree has
  bound and type-checked successfully. `Predicate` SHALL require the root type to be `Bool`;
  `General` SHALL accept any `CelTypeKind`. A `Predicate` compilation whose root type is not `Bool`
  SHALL produce `TypeMismatch` carrying `Parameters["expectedType"] = "Bool"` and
  `Parameters["actualType"]` set to the root expression's actual resolved type name.
- On success, the binder SHALL produce an immutable internal bound-expression tree with exactly one
  bound node per input `CelSyntaxNode`, each carrying the source node's `CelSourceSpan` and its own
  resolved `CelType`. `CelIdentifierSyntax`/`CelMemberAccessSyntax` bound nodes SHALL reference the
  resolved `CelVariable`/`CelObjectMember` handle rather than only the source name string. This
  bound-expression tree SHALL be internal only; it SHALL NOT be exposed by any public type or
  member, and no public API SHALL allow constructing, inspecting, or serializing it.
- `CelCompiledPredicate` and `CelCompiledExpression` SHALL hold the bound-expression tree internally
  once binding succeeds, in addition to the properties this spec already fixes for them. Their
  `Evaluate` methods SHALL continue to throw `NotImplementedException` until the evaluator (#327)
  lands — binding success alone SHALL NOT enable evaluation.
- Every diagnostic produced by the binder SHALL use diagnostic category `"binder"` and SHALL carry
  `profileId` in `Parameters`, consistent with this spec's blanket diagnostic requirement.
  `BindingError`/`SchemaMismatch` diagnostics SHALL carry `identifier`; `TypeMismatch` diagnostics
  SHALL carry `expectedType` and `actualType`.
- `CelEnvironment.CompilePredicate` and `CelEnvironment.Compile` SHALL run the binder immediately
  after a successful parse. For a syntactically valid Profile v1 expression that also binds and
  type-checks successfully, they SHALL return a real, successful `CelCompilationResult<T>` wrapping
  a `CelCompiledPredicate`/`CelCompiledExpression` instead of `NotYetImplemented`. For a
  syntactically valid expression that fails to bind or type-check, they SHALL return a failed
  `CelCompilationResult<T>` carrying the binder's structured diagnostic instead of
  `NotYetImplemented`. `NotYetImplemented` SHALL no longer be produced for any input once this
  change ships, since binder implementation was the last remaining gap in the compile path;
  `CelDiagnosticCode.NotYetImplemented` itself remains declared on the enum only because removing a
  published enum member would be a breaking API change, not because any code path still produces it.

#### Scenario: Whole-AST binding surfaces a violation in a branch runtime short-circuiting would skip

- **WHEN** the expression `x || m["missing"]` is compiled against a schema declaring `x` as `Bool`
  and `m` as `Map<String, Bool>`, with the right-hand `m["missing"]` operand syntactically and
  type-wise valid (a `Map<String, Bool>` receiver indexed by a `String` literal)
- **THEN** compilation succeeds (both operands type-check as `Bool`, so `||` type-checks as `Bool`)
- **AND** a second expression `x || y` compiled against the same schema, where `y` is an
  undeclared identifier, fails with a `BindingError` diagnostic for `y` even though a future
  evaluator could determine `x || y` is `true` (and skip evaluating `y`) whenever `x` is `true`

#### Scenario: Unresolved identifier produces a BindingError with the identifier name

- **WHEN** the expression `undeclared` is compiled against a schema that does not declare a
  variable named `undeclared`
- **THEN** compilation fails with a `BindingError` diagnostic
- **AND** `diagnostic.Parameters["identifier"]` equals `"undeclared"`

#### Scenario: Undeclared object member produces a SchemaMismatch with the member name

- **WHEN** the expression `x.missingMember` is compiled against a schema declaring `x` as an object
  type whose `CelObjectSchema` does not declare a member named `missingMember`
- **THEN** compilation fails with a `SchemaMismatch` diagnostic
- **AND** `diagnostic.Parameters["identifier"]` equals `"missingMember"`

#### Scenario: Unknown function name produces a BindingError

- **WHEN** the expression `x.unknownFunction()` is compiled against a schema declaring `x` as
  `String`
- **THEN** compilation fails with a `BindingError` diagnostic

#### Scenario: Wrong index expression type produces a TypeMismatch

- **WHEN** the expression `list["not-an-int"]` is compiled against a schema declaring `list` as
  `List<Int>`
- **THEN** compilation fails with a `TypeMismatch` diagnostic

#### Scenario: A Predicate compilation whose root type is not Bool fails with TypeMismatch

- **WHEN** an expression whose statically resolved type is `Int` is compiled via
  `CompilePredicate`
- **THEN** compilation fails with a `TypeMismatch` diagnostic
- **AND** `diagnostic.Parameters["expectedType"]` equals `"Bool"`

#### Scenario: A General compilation accepts a non-Bool root type

- **WHEN** an expression whose statically resolved type is `Int` is compiled via `Compile`
  (`CelRequiredResultType.General`)
- **THEN** compilation succeeds

#### Scenario: A syntactically and semantically valid expression no longer returns NotYetImplemented

- **WHEN** a syntactically valid Profile v1 expression that resolves and type-checks successfully
  is compiled
- **THEN** `CelCompilationResult<T>.IsSuccess` is `true`
- **AND** the result does not carry a `NotYetImplemented` diagnostic

#### Scenario: Every binder diagnostic carries the binder category and profile identifier

- **WHEN** any binder diagnostic is produced
- **THEN** `diagnostic.Category` equals `"binder"`
- **AND** `diagnostic.Parameters["profileId"]` equals `"arch-linter/cel/v1"`

#### Scenario: The bound-expression tree is never exposed publicly

- **WHEN** the public API surface of `ArchLinterNet.CEL` is inspected
- **THEN** no public type, member, or method exposes a bound-expression node, tree, or any type
  from the internal `ArchLinterNet.CEL.Binding` namespace

#### Scenario: Successful binding does not enable evaluation

- **WHEN** a `CelCompiledPredicate` or `CelCompiledExpression` is obtained from a successful
  compilation
- **THEN** calling `Evaluate` still throws `NotImplementedException`
