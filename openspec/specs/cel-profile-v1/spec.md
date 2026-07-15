# cel-profile-v1 Specification

## Purpose

Defines the normative ArchLinter CEL Profile v1 (`arch-linter/cel/v1`): the bounded CEL language
subset, public API surface, immutability and thread-safety contracts, structured diagnostic model,
compilation/evaluation limits, and cache identity design for `ArchLinterNet.CEL`. This spec is
the source of truth that implementation tasks #325–#329 must satisfy.
## Requirements
### Requirement: Profile identity and CEL baseline are pinned

ArchLinter CEL Profile v1 (`CelProfileId = "arch-linter/cel/v1"`) SHALL pin the normative CEL language specification at commit `59505c14f3187e6eb9684fbd3d07146f614c6148` of `https://github.com/google/cel-spec` (`doc/langdef.md` at that commit). The repository is `google/cel-spec`, not `cel-expr/cel-spec`. The profile identifier SHALL be a stable, versioned string that appears in `CelCompilationKey` and diagnostic messages. The profile SHALL NOT claim full CEL conformance; it defines a deliberate subset.

#### Scenario: Profile identity is accessible from public API

- **WHEN** a consumer reads `CelProfile.V1.Id`
- **THEN** it returns `CelProfileId` with value `"arch-linter/cel/v1"`

#### Scenario: Profile V1 is the only profile in v1

- **WHEN** a consumer inspects `CelProfile.V1`
- **THEN** it is a non-null, immutable singleton representing the only supported profile

### Requirement: Supported value types for Profile v1

Profile v1 SHALL support the following value types in `CelType`: `Bool`, `String`, `Int` (signed 64-bit), `Float` (IEEE 754 double), immutable `List` (homogeneous element type), immutable string-keyed `Map` (homogeneous value type), and schema-defined `Object` values. Object values SHALL be defined through `CelContextSchema` and `CelType.ObjectOf`, never through CLR reflection or automatic POCO discovery.

#### Scenario: Primitive CelType constants are accessible

- **WHEN** a consumer reads `CelType.Bool`, `CelType.String`, `CelType.Int`, `CelType.Float`
- **THEN** each returns a non-null, distinct `CelType` instance with correct kind

#### Scenario: Composite CelType factories produce typed descriptors

- **WHEN** a consumer calls `CelType.ListOf(CelType.String)` or `CelType.MapOf(CelType.String)`
- **THEN** a non-null `CelType` is returned whose `ElementType` or `ValueType` matches the argument

#### Scenario: Object type is defined through schema, not reflection

- **WHEN** a consumer calls `CelType.ObjectOf("assembly")` with a schema identifier
- **THEN** a non-null `CelType` is returned without any CLR reflection occurring

### Requirement: Supported operators and access forms for Profile v1

Profile v1 SHALL support: logical negation (`!`); logical conjunction (`&&`) and disjunction (`||`) with CEL short-circuit semantics; equality (`==`, `!=`); ordered comparison (`<`, `<=`, `>`, `>=`); set membership (`in`); member access via dot notation; map and list indexing via bracket notation. Arithmetic expressions, conditional (`? :`), and all other CEL operators are deferred and SHALL NOT be accepted by the compiler in v1.

#### Scenario: Operator/access coverage is documented in Profile v1 spec

- **WHEN** the `cel-profile-v1` spec is read
- **THEN** each supported operator is listed with its CEL semantics reference
- **AND** each deferred operator is listed as explicitly unsupported

### Requirement: Operator precedence, associativity, and per-operator type signatures are frozen

Profile v1 SHALL define a single, total precedence order for every supported operator, from lowest to highest binding power: (1) `||`; (2) `&&`; (3) `==`, `!=`, `<`, `<=`, `>`, `>=`, `in` (all non-associative — the grammar SHALL NOT accept chained comparisons such as `a < b < c` without explicit parentheses); (4) unary `!` (prefix); (5) member access (`.`) and indexing (`[]`) (left-to-right). Binary logical operators (`||`, `&&`) SHALL be left-associative. Parenthesized sub-expressions SHALL override precedence. This order matches the pinned `google/cel-spec` grammar restricted to the v1 subset and SHALL NOT change without a new profile version.

Each supported operator SHALL have the following frozen type signature and static (compile-time) type-check behavior — a signature violation SHALL produce a `TypeMismatch` diagnostic at compile time, not a runtime error:

- `!bool -> bool`. Any other operand type is a compile-time `TypeMismatch`.
- `bool && bool -> bool`, `bool || bool -> bool`. Non-`bool` operands are a compile-time `TypeMismatch`.
- `T == T -> bool`, `T != T -> bool` for any two operands of the same supported type `T` (`Bool`, `String`, `Int`, `Float`, `List`, `Map`, `Object`). Comparing operands of two different `CelTypeKind`s SHALL be a compile-time `TypeMismatch` (v1 performs no implicit numeric widening between `Int` and `Float`). `List`/`Map`/`Object` equality SHALL be deep structural equality.
- `Int|Float <op> Int|Float -> bool` for `<`, `<=`, `>`, `>=` where both operands are the same `CelTypeKind` (`Int`-to-`Int` or `Float`-to-`Float`; no implicit widening). Any other operand type or a mixed `Int`/`Float` pair is a compile-time `TypeMismatch`.
- `T in List<T> -> bool` and `String in Map<String, T> -> bool` (key membership). A mismatched element/key type is a compile-time `TypeMismatch`.
- `Object.member -> <member's declared CelType>` where `member` is declared in the `CelObjectSchema` registered for that object's `ObjectTypeId`. Accessing an undeclared member name is a compile-time `SchemaMismatch`.
- `List[Int] -> <element type>`, `Map[String] -> <value type>`. An index/key expression of the wrong type (e.g. `List[String]`) is a compile-time `TypeMismatch`.

#### Scenario: Precedence table resolves an unparenthesized mixed expression deterministically

- **WHEN** the expression `a || b && c` is parsed
- **THEN** it is equivalent to `a || (b && c)` because `&&` binds tighter than `||`

#### Scenario: Chained comparisons require explicit parentheses

- **WHEN** the expression `a < b < c` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic (comparison operators are non-associative in v1)

#### Scenario: Cross-kind equality is a compile-time type mismatch

- **WHEN** an expression compares a `String`-typed operand with an `Int`-typed operand using `==`
- **THEN** compilation fails with a `TypeMismatch` diagnostic; no implicit conversion occurs

#### Scenario: Mixed Int/Float comparison is a compile-time type mismatch

- **WHEN** an expression compares an `Int`-typed operand with a `Float`-typed operand using `<`
- **THEN** compilation fails with a `TypeMismatch` diagnostic; v1 performs no implicit numeric widening

### Requirement: Short-circuit, error-propagation, missing-key, and invalid-index semantics are normative

`&&` and `||` SHALL short-circuit at evaluation time per the pinned CEL spec: for `a && b`, if `a` evaluates to `false`, `b` SHALL NOT be evaluated and the result SHALL be `false`; for `a || b`, if `a` evaluates to `true`, `b` SHALL NOT be evaluated and the result SHALL be `true`. Otherwise both operands SHALL be evaluated and combined normally. Because v1 performs full static type-checking at compile time (Requirement: "Immutable public environment and compilation lifecycle"), no operand of `&&`/`||`/`!`/comparison operators can have a type error surviving to evaluation — evaluation-time errors in v1 arise only from runtime data conditions below, not from type mismatches.

Map indexing (`Map[key]`) and `containsKey(key)` on a key absent from the map value at evaluation time SHALL NOT throw a CLR exception. `Map[key]` on a missing key SHALL produce a failed `CelEvaluationResult` (`IsSuccess = false`) carrying a `CelDiagnostic` with code `EvaluationFailure`. `containsKey(key)` on a missing key SHALL return `CelValue.Bool(false)` (it SHALL NOT fail). List indexing (`List[index]`) with `index < 0` or `index >= size()` SHALL likewise produce a failed `CelEvaluationResult` with an `EvaluationFailure` diagnostic rather than throwing. Once evaluation of an expression has produced any `EvaluationFailure`, evaluation of that expression SHALL stop and the result SHALL be that failure — v1 does not continue evaluating and does not aggregate multiple evaluation-time failures into one result.

Profile v1 has no `null` value: `CelValueKind` and `CelTypeKind` do not include a null/none variant, `CelObjectSchema` members are always present with a value of their declared type, and there is no optional/nullable member concept in v1. Consequently v1 defines no null-propagation or null-coalescing semantics — every value that type-checks at compile time is guaranteed non-null at evaluation time, and "missing" is only observable for map keys and list indices (handled above), never for a declared variable or object member.

#### Scenario: Logical AND short-circuits on a false left operand

- **WHEN** the evaluator is implemented and `a && b` is evaluated with `a` evaluating to `CelValue.Bool(false)`
- **THEN** `b` is not evaluated
- **AND** the result is `CelValue.Bool(false)`

#### Scenario: Logical OR short-circuits on a true left operand

- **WHEN** the evaluator is implemented and `a || b` is evaluated with `a` evaluating to `CelValue.Bool(true)`
- **THEN** `b` is not evaluated
- **AND** the result is `CelValue.Bool(true)`

#### Scenario: Missing map key produces a structured evaluation failure, not an exception

- **WHEN** the evaluator is implemented and `m[key]` is evaluated with `key` absent from `m`
- **THEN** `CelEvaluationResult.IsSuccess` is `false`
- **AND** `CelEvaluationResult.Diagnostics` contains a `CelDiagnostic` with code `EvaluationFailure`
- **AND** no CLR exception is thrown

#### Scenario: containsKey on a missing key returns false, not a failure

- **WHEN** the evaluator is implemented and `m.containsKey(key)` is evaluated with `key` absent from `m`
- **THEN** the result is a successful `CelEvaluationResult` with value `CelValue.Bool(false)`

#### Scenario: Out-of-range list index produces a structured evaluation failure

- **WHEN** the evaluator is implemented and `list[i]` is evaluated with `i` outside `[0, list.size())`
- **THEN** `CelEvaluationResult.IsSuccess` is `false`
- **AND** `CelEvaluationResult.Diagnostics` contains a `CelDiagnostic` with code `EvaluationFailure`
- **AND** no CLR exception is thrown

#### Scenario: Profile v1 has no null value

- **WHEN** `CelValueKind` and `CelTypeKind` are inspected
- **THEN** neither enum declares a null/none member

### Requirement: Supported built-in functions for Profile v1

Profile v1 SHALL support the following string and map receiver functions: `startsWith(string)`, `endsWith(string)`, `contains(string)`, `size()`, `containsKey(string)`. The `matches` function and all regex, timestamp, duration, protobuf, and user-defined functions are deferred and SHALL NOT be resolvable in a Profile v1 compilation.

#### Scenario: Supported function names are listed in spec

- **WHEN** the `cel-profile-v1` spec is read
- **THEN** each supported function is listed with its argument types and return type
- **AND** `matches` and regex are explicitly listed as deferred

### Requirement: Immutable public environment and compilation lifecycle

`CelEnvironment` SHALL be immutable and thread-safe after construction. It SHALL capture exactly: the profile (`CelProfile`), a closed `CelContextSchema`, `CelCompilationLimits`, and semantic options. It SHALL expose `CompilePredicate(string source)` returning `CelCompilationResult<CelCompiledPredicate>` and `Compile(string source)` returning `CelCompilationResult<CelCompiledExpression>`. Normal compilation SHALL always parse, bind, and type-check the whole expression; parse-only and partial-check paths SHALL NOT exist as public API. No mutation or function registration SHALL be possible after build.

#### Scenario: CelEnvironment is constructed through builder

- **WHEN** a consumer calls `CelEnvironment.CreateBuilder(CelProfile.V1).WithContextSchema(schema).WithCompilationLimits(CelCompilationLimits.SafeDefaults).Build()`
- **THEN** a non-null `CelEnvironment` is returned

#### Scenario: CompilePredicate returns a structured result

- **WHEN** a consumer calls `environment.CompilePredicate(anyString)`
- **THEN** a non-null `CelCompilationResult<CelCompiledPredicate>` is returned
- **AND** `result.IsSuccess` is a readable boolean
- **AND** `result.Diagnostics` is a non-null readable collection

#### Scenario: Invalid user expression produces structured diagnostics not exception

- **WHEN** the parser is implemented and a consumer compiles a syntactically invalid expression
- **THEN** `result.IsSuccess` is `false`
- **AND** `result.Diagnostics` contains at least one `CelDiagnostic` with a source span
- **AND** no exception is thrown

### Requirement: Immutable compiled programs are thread-safe and concurrently reusable

`CelCompiledPredicate` and `CelCompiledExpression` SHALL be immutable, thread-safe, side-effect-free, and concurrently reusable. They SHALL carry `Profile`, `Schema`, `CompilationKey`, and `CompilationLimits` as readable properties. They SHALL NOT expose parser, binder, or evaluator internals. They SHALL NOT be implicitly convertible to delegates or expression trees. Repeated evaluation SHALL perform no parsing, binding, or type-checking.

#### Scenario: Compiled predicate exposes profile and key

- **WHEN** a successful `CelCompilationResult<CelCompiledPredicate>` is obtained
- **THEN** `result.Program.Profile` equals the environment's profile
- **AND** `result.Program.CompilationKey` is a non-null structural cache-identity value

#### Scenario: Compiled predicate evaluates to a structured result

- **WHEN** the evaluator is implemented and a consumer calls `program.Evaluate(context, limits)`
- **THEN** a `CelEvaluationResult` is returned with typed boolean access via `AsBool()`
- **AND** no delegate is exposed as an evaluation path

### Requirement: Immutable context schema and schema-bound activation

`CelContextSchema` SHALL be immutable after construction. It SHALL be identified structurally (a deterministic identity derived from profile, variable names, and type descriptors). `CelContextSchemaBuilder` SHALL return a `CelVariable` handle per `AddVariable()` call. Duplicate variable names during build SHALL produce a programmer-error exception. `CelEvaluationContext` SHALL be built via `schema.CreateEvaluationContextBuilder()` and `builder.Set(variable, value)` using the variable handle (not a string key). `CelEvaluationContextBuilder` SHALL additionally expose `Set(string name, CelValue value)` as an ergonomic convenience overload that resolves `name` to its declared `CelVariable` handle via a single lookup and delegates to `Set(CelVariable, CelValue)`; an unknown name SHALL throw `ArgumentException`. The name-based overload exists for benchmarking and ergonomic call sites (see #168); the handle-based overload remains the recommended path for high-volume evaluation to avoid repeated string lookup. Missing variable assignments, duplicate assignments, and type mismatches SHALL produce deterministic errors.

#### Scenario: Schema builder returns typed variable handles

- **WHEN** a consumer calls `schemaBuilder.AddVariable("source", CelType.String)`
- **THEN** a non-null `CelVariable` handle is returned
- **AND** calling `Set(handle, CelValue.String("x"))` on the context builder compiles without reflection

#### Scenario: Schema has deterministic structural identity

- **WHEN** two schemas are built with the same variable names and types
- **THEN** their identity representations are equal

#### Scenario: Duplicate variable names are programmer errors

- **WHEN** a consumer calls `AddVariable` twice with the same name on the same builder
- **THEN** an `ArgumentException` or `InvalidOperationException` is thrown

#### Scenario: Name-based Set() resolves to the same assignment as handle-based Set()

- **WHEN** a consumer calls `builder.Set("x", value)` where `"x"` is a declared variable name
- **THEN** the resulting `CelEvaluationContext.Assignments` entry is identical to what `builder.Set(handle, value)` would have produced

#### Scenario: Name-based Set() with an unknown name is a programmer error

- **WHEN** a consumer calls `builder.Set("undeclared", value)` where no variable named `"undeclared"` exists in the schema
- **THEN** an `ArgumentException` is thrown

### Requirement: Immutable CEL value model without CLR reflection

`CelValue` SHALL be constructed only through typed factory methods: `CelValue.Bool(bool)`, `CelValue.String(string)`, `CelValue.Int(long)`, `CelValue.Float(double)`, `CelValue.List(IReadOnlyList<CelValue>)`, `CelValue.Map(IReadOnlyDictionary<string, CelValue>)`, `CelValue.Object(CelObjectValue)`. `CelValue` SHALL expose a `Kind` property of type `CelValueKind` and typed accessor methods (`AsBool()`, `AsString()`, `AsInt()`, `AsFloat()`, `AsList()`, `AsMap()`, `AsObject()`) that throw `InvalidOperationException` on kind mismatch. No arbitrary CLR object, POCO, `dynamic`, or reflection-based member discovery SHALL be accepted as a CEL value.

#### Scenario: CelValue factories produce kind-correct values

- **WHEN** a consumer calls `CelValue.Bool(true)`
- **THEN** `value.Kind == CelValueKind.Bool` and `value.AsBool() == true`
- **AND** `value.AsString()` throws `InvalidOperationException`

#### Scenario: CelValue rejects CLR objects

- **WHEN** the public API is inspected
- **THEN** there is no overload accepting `object`, `dynamic`, or an arbitrary CLR type

### Requirement: Structured diagnostics with stable codes and source spans

`CelDiagnostic` SHALL carry: `Code` (`CelDiagnosticCode` enum), `Category` (string), `Severity` (`CelDiagnosticSeverity` enum), `Span` (`CelSourceSpan` — nullable for non-source-locatable diagnostics), and `Message` (display-only, not the machine contract). `CelDiagnosticCode` SHALL distinguish at minimum: `SyntaxError`, `UnsupportedFeature`, `BindingError`, `TypeMismatch`, `SchemaMismatch`, `BudgetExceeded`, `EvaluationFailure`, `NotYetImplemented`. Display messages SHALL NOT be treated as machine-readable identifiers.

#### Scenario: Compilation diagnostics carry stable codes

- **WHEN** a compilation returns `IsSuccess = false`
- **THEN** each diagnostic in `result.Diagnostics` has a non-null `Code` of type `CelDiagnosticCode`
- **AND** the `Code` is one of the declared enum values

#### Scenario: Source span is structurally defined

- **WHEN** a `CelDiagnostic` has a non-null `Span`
- **THEN** `Span.Start` and `Span.End` are non-negative integers representing character offsets

### Requirement: Compilation and evaluation limits are immutable with safe defaults

`CelCompilationLimits` and `CelEvaluationLimits` SHALL be sealed, immutable classes with all-or-nothing safe-default factories (`SafeDefaults`). Per-call evaluation limits SHALL be allowed to tighten but not exceed the environment/profile maximums. Every public compilation and evaluation path SHALL be intrinsically bounded; no unbounded execution overload SHALL exist. Convenience overloads that omit limits SHALL use documented safe defaults, not unrestricted execution. `CelCompiledPredicate` and `CelCompiledExpression` SHALL each expose `Evaluate(CelEvaluationContext context)` as a convenience overload equivalent to `Evaluate(context, CelEvaluationLimits.SafeDefaults)` — this satisfies the "safe-default evaluation overload" benchmarked by #168 without introducing an unbounded evaluation path.

The complete compilation-time budget surface that #325 (tokenizer/parser) and #326 (binder/checker) SHALL enforce comprises, at minimum: maximum expression source length in UTF-16 characters (`MaxExpressionLength`, already present); maximum token count produced by the tokenizer (`MaxTokenCount`); maximum AST node count produced by parsing (`MaxAstNodeCount`); maximum sub-expression nesting depth (`MaxNestingDepth`, already present); maximum literal size — the longest string/collection literal accepted in source (`MaxLiteralSize`); maximum distinct identifier references (`MaxIdentifierCount`, already present). Each of these limits SHALL be a positive-only field on `CelCompilationLimits`, SHALL be included as a named component of `CelCompilationLimits.ComputeIdentity()`, and a source exceeding any one of them SHALL produce a `BudgetExceeded` diagnostic before further processing of that phase. `MaxExpressionLength` remains the cheapest, first-checked gate (rejecting oversized source before tokenization even begins); the token/AST/literal limits are checked as each corresponding phase runs. Fields for limits not yet enforced by a landed phase (#325/#326 pending) SHALL still exist on `CelCompilationLimits` so the API shape does not change once those phases ship — an unenforced field SHALL be documented as "reserved, not yet enforced" in its XML doc until the enforcing phase lands.

The complete evaluation-time budget surface that #327 (evaluator) SHALL enforce comprises, at minimum: maximum evaluation steps (`MaxIterations`, already present); maximum accumulated abstract cost units (`MaxCostUnits`, already present); maximum input-value structural depth accepted by `CelEvaluationContextBuilder.Set()` (already enforced pre-evaluator via `MaxValidationDepth`, an internal constant — not yet a public per-environment field); maximum input collection size (element/entry count) accepted by `Set()` for `List`/`Map`/`Object` values (already enforced pre-evaluator via `MaxValidationCollectionSize`, an internal constant — not yet a public per-environment field). Exceeding any evaluation-time limit SHALL produce a failed `CelEvaluationResult` with a `BudgetExceeded` diagnostic, not a CLR exception, except where the limit is enforced synchronously inside `Set()` (structural depth, collection size) — those SHALL continue to be reported as `ArgumentException` per the "Immutable context schema and schema-bound activation" requirement, since `Set()` is a builder-time programmer-facing call, not an evaluation call.

#### Scenario: SafeDefaults factories are accessible

- **WHEN** a consumer reads `CelCompilationLimits.SafeDefaults` or `CelEvaluationLimits.SafeDefaults`
- **THEN** a non-null limits instance is returned

#### Scenario: No unbounded execution overload exists

- **WHEN** the public API surface of `CelCompiledPredicate` and `CelCompiledExpression` is inspected
- **THEN** there is no `Evaluate()` overload that accepts no limits and performs unrestricted evaluation
- **AND** the `Evaluate(context)` overload that omits an explicit limits argument internally uses `CelEvaluationLimits.SafeDefaults`

#### Scenario: Safe-default Evaluate overload delegates to the explicit-limits overload

- **WHEN** a consumer calls `predicate.Evaluate(context)`
- **THEN** the result is identical to calling `predicate.Evaluate(context, CelEvaluationLimits.SafeDefaults)`

#### Scenario: Compilation limits identity incorporates every enforced limit field

- **WHEN** `CelCompilationLimits.ComputeIdentity()` is called
- **THEN** the returned string changes if any field on `CelCompilationLimits` changes
- **AND** two `CelCompilationLimits` instances with identical field values produce identical identity strings

#### Scenario: Set() rejects a list exceeding the collection-size limit

- **WHEN** `CelEvaluationContextBuilder.Set()` is called with a `List` value whose element count exceeds the internal collection-size limit
- **THEN** an `ArgumentException` is thrown
- **AND** no CPU time is spent validating individual elements beyond the size check

#### Scenario: Set() rejects a map exceeding the collection-size limit

- **WHEN** `CelEvaluationContextBuilder.Set()` is called with a `Map` value whose entry count exceeds the internal collection-size limit
- **THEN** an `ArgumentException` is thrown

### Requirement: Cache identity is deterministic and caller-owned

`CelCompilationKey` SHALL be a sealed, structurally-equal value object encoding: normalized expression source, `CelProfileId`, structural schema identity, required result type (predicate vs general), compilation limits identity. It SHALL implement `IEquatable<CelCompilationKey>` and override `GetHashCode()`. No process-global mutable cache SHALL exist in `ArchLinterNet.CEL`; cache lifetime SHALL be caller-owned.

Profile v1 defines no independently-configurable semantic options (there is no case-sensitivity flag, numeric-widening toggle, or similar knob) — the profile identity plus schema plus limits fully determine compilation behavior. `CelCompilationKey` therefore SHALL NOT carry a separate semantic-options identity component in v1; introducing one is deferred until a v2+ profile actually defines a configurable semantic axis. The registered object-schema catalog SHALL continue to be folded into `SchemaIdentity` (as `CelExternalConsumerSampleTests.HappyPath_BuildEnvironmentAndInspectCompilationResult` already verifies via `key.SchemaIdentity Does.StartWith(schema.Identity)` plus catalog content) rather than exposed as a separate `CelCompilationKey` field, since the catalog is inseparable from what "the schema" means for binding purposes in v1.

#### Scenario: Two compilations with identical inputs produce equal keys

- **WHEN** the evaluator is implemented and two identical expressions are compiled against the same environment
- **THEN** their `CompilationKey` values are equal under `Equals` and have the same `GetHashCode`

#### Scenario: No static mutable cache exists in ArchLinterNet.CEL

- **WHEN** the assembly is inspected
- **THEN** no static mutable field or property stores compiled expressions or compilation results across calls

### Requirement: Public API contains no Core, YAML, Roslyn, or product-specific terminology

All public types, members, parameters, and XML doc comments in `ArchLinterNet.CEL` SHALL be expressed in generic CEL and .NET terminology. No reference to `ArchLinterNet.Core`, YAML fields, Roslyn, MSBuild, architecture contracts, policy files, or dependency-linting domain concepts SHALL appear in any public-facing identifier or documentation string.

#### Scenario: External consumer requires no Core assembly reference

- **WHEN** a file uses only `ArchLinterNet.CEL` types in its `using` directives
- **THEN** it compiles successfully without `ArchLinterNet.Core` being referenced

#### Scenario: No Core concepts in public member names

- **WHEN** the public API surface of `ArchLinterNet.CEL` is inspected
- **THEN** no type name, method name, property name, or parameter name contains "Architecture", "Policy", "Contract", "Layer", "Assembly", "Namespace", "YAML", "Roslyn", or "MSBuild"

### Requirement: Internal engine blueprint exists before implementation tasks begin

`docs/internal/cel-engine-architecture.md` SHALL exist and be linked from `docs/internal/README.md` before any of #325–#329 begins. It SHALL document: the processing pipeline (source → tokenizer → internal syntax → binder/type-checker → immutable bound plan → bounded evaluator → public typed result/diagnostics); component ownership (profiles, grammar gates, values/types, schemas, catalogs, bound operations, budgets, compiled programs, diagnostics, cache identity); a full extension-direction matrix for all excluded v1 capabilities; and prohibited shortcuts that future tasks may not bypass. It SHALL NOT appear in MkDocs site navigation.

#### Scenario: Blueprint file exists and is linked

- **WHEN** `docs/internal/README.md` is read
- **THEN** it contains a Markdown link to `cel-engine-architecture.md`

#### Scenario: Blueprint covers all seven required extension directions

- **WHEN** `docs/internal/cel-engine-architecture.md` is read
- **THEN** it documents language/profile expansion, host adapters, function catalog, execution backends, tooling/AST, caching/serialization, and diagnostics/explainability directions

#### Scenario: Blueprint is excluded from MkDocs navigation

- **WHEN** `mkdocs.yml` is inspected
- **THEN** `cel-engine-architecture.md` does not appear in the `nav` section

### Requirement: Compile-time API shape tests pass under make acceptance

`tests/ArchLinterNet.CEL.Tests/` SHALL contain `CelApiShapeTests.cs` verifying the builder chain types and result property accessibility, and `CelExternalConsumerSampleTests.cs` showing the complete happy-path interaction model using only `ArchLinterNet.CEL`. Both files SHALL compile and all tests SHALL pass under `rtk make acceptance`. Neither file SHALL contain a `using` directive for `ArchLinterNet.Core`.

#### Scenario: API shape tests compile and pass

- **WHEN** `rtk make acceptance` is run
- **THEN** all tests in `CelApiShapeTests.cs` and `CelExternalConsumerSampleTests.cs` pass
- **AND** the test assembly does not reference `ArchLinterNet.Core` for any test in those files

