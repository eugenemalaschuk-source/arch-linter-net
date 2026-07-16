# cel-profile-v1 Specification

## Purpose

Defines the normative ArchLinter CEL Profile v1 (`arch-linter/cel/v1`): the bounded CEL language
subset, public API surface, immutability and thread-safety contracts, structured diagnostic model,
compilation/evaluation limits, and cache identity design for `ArchLinterNet.CEL`. This spec is
the source of truth that implementation tasks #325–#329 must satisfy.
## Requirements
### Requirement: Profile identity and CEL baseline are pinned

ArchLinter CEL Profile v1 (`CelProfileId = "arch-linter/cel/v1"`) SHALL pin the normative CEL language specification at commit `59505c14f3187e6eb9684fbd3d07146f614c6148`, `doc/langdef.md`, in the canonical repository `https://github.com/cel-expr/cel-spec` (the historical address `google/cel-spec` redirects there; both resolve to the same commit). The profile identifier SHALL be a stable, versioned string that appears in `CelCompilationKey` and as a structured `profileId` parameter on compilation diagnostics. The profile SHALL NOT claim full CEL conformance; it defines a deliberate subset.

#### Scenario: Profile identity is accessible from public API

- **WHEN** a consumer reads `CelProfile.V1.Id`
- **THEN** it returns `CelProfileId` with value `"arch-linter/cel/v1"`

#### Scenario: Profile V1 is the only profile in v1

- **WHEN** a consumer inspects `CelProfile.V1`
- **THEN** it is a non-null, immutable singleton representing the only supported profile

#### Scenario: Compilation diagnostics carry the profile identifier as a structured parameter

- **WHEN** a compilation produces any diagnostic
- **THEN** `diagnostic.Parameters["profileId"]` equals `"arch-linter/cel/v1"`

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

Profile v1 SHALL support: logical negation (`!`); logical conjunction (`&&`) and disjunction (`||`) with CEL error-aware commutative semantics (see the logical-operator/error-semantics requirement); equality (`==`, `!=`); ordered comparison (`<`, `<=`, `>`, `>=`); set membership (`in`); member access via dot notation; map and list indexing via bracket notation. Arithmetic expressions, conditional (`? :`), and all other CEL operators are deferred and SHALL NOT be accepted by the compiler in v1.

#### Scenario: Operator/access coverage is documented in Profile v1 spec

- **WHEN** the `cel-profile-v1` spec is read
- **THEN** each supported operator is listed with its CEL semantics reference
- **AND** each deferred operator is listed as explicitly unsupported

### Requirement: Operator precedence, associativity, and per-operator type signatures are frozen

Profile v1 SHALL define a single, total precedence order for every supported operator, from lowest to highest binding power: (1) `||`; (2) `&&`; (3) `==`, `!=`, `<`, `<=`, `>`, `>=`, `in` (all non-associative — the grammar SHALL NOT accept chained comparisons such as `a < b < c` without explicit parentheses); (4) unary `!` (prefix); (5) member access (`.`) and indexing (`[]`) (left-to-right). Binary logical operators (`||`, `&&`) SHALL be left-associative. Parenthesized sub-expressions SHALL override precedence. This order matches the pinned `cel-expr/cel-spec` grammar restricted to the v1 subset and SHALL NOT change without a new profile version.

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

### Requirement: Logical-operator, error-propagation, missing-key, and invalid-index semantics are normative

`&&` and `||` SHALL follow the pinned CEL Language Definition's error-aware, commutative logical semantics. These are defined by **observable result**, not by evaluation order — an evaluator MAY evaluate operands in any order, may evaluate both, or may skip one, provided the observable outcome matches this table:

For `a && b` (commutative — the table is symmetric in `a` and `b`):

| One operand | Other operand | Result |
| --- | --- | --- |
| `false` | anything (`true`, `false`, or evaluation error) | `false` |
| `true` | `true` | `true` |
| `true` | evaluation error | that evaluation error |
| evaluation error | evaluation error | an evaluation error |

For `a || b` (commutative):

| One operand | Other operand | Result |
| --- | --- | --- |
| `true` | anything (`true`, `false`, or evaluation error) | `true` |
| `false` | `false` | `false` |
| `false` | evaluation error | that evaluation error |
| evaluation error | evaluation error | an evaluation error |

That is: a determining operand (`false` for `&&`, `true` for `||`) absorbs an evaluation error in the other operand; an error surfaces only when the non-erroring operand does not determine the result. Concretely, with `m[k]` failing on a missing key: `false && m[k]` is `false`; `true || m[k]` is `true`; `true && m[k]` and `false || m[k]` are evaluation errors. The pinned CEL spec notes that traditional McCarthy left-to-right short-circuit is expressible via the conditional operator — which is deferred in v1 — so v1 SHALL NOT guarantee that the right operand is unevaluated; conformance tests (#328) SHALL assert observable results only.

Because v1 performs full static type-checking at compile time (Requirement: "Immutable public environment and compilation lifecycle"), no operand of `&&`/`||`/`!`/comparison operators can have a type error surviving to evaluation — evaluation-time errors in v1 arise only from runtime data conditions below, not from type mismatches.

Map indexing (`Map[key]`) and `containsKey(key)` on a key absent from the map value at evaluation time SHALL NOT throw a CLR exception. `Map[key]` on a missing key SHALL produce a failed `CelEvaluationResult` (`IsSuccess = false`) carrying a `CelDiagnostic` with code `EvaluationFailure`. `containsKey(key)` on a missing key SHALL return `CelValue.Bool(false)` (it SHALL NOT fail). List indexing (`List[index]`) with `index < 0` or `index >= size()` SHALL likewise produce a failed `CelEvaluationResult` with an `EvaluationFailure` diagnostic rather than throwing. Evaluation errors propagate upward through every operator and function **except** `&&`/`||` with a determining operand as defined above, which absorb them; when an error reaches the top of the expression the result is that failure — v1 does not aggregate multiple evaluation-time failures into one result.

Profile v1 has no `null` value: `CelValueKind` and `CelTypeKind` do not include a null/none variant, `CelObjectSchema` members are always present with a value of their declared type, and there is no optional/nullable member concept in v1. Consequently v1 defines no null-propagation or null-coalescing semantics — every value that type-checks at compile time is guaranteed non-null at evaluation time, and "missing" is only observable for map keys and list indices (handled above), never for a declared variable or object member.

#### Scenario: Determining false operand absorbs an error in the other AND operand

- **WHEN** the evaluator is implemented and `a && b` is evaluated where `a` evaluates to `CelValue.Bool(false)` and `b` produces an evaluation error (e.g. a missing map key)
- **THEN** the result is a successful `CelEvaluationResult` with value `CelValue.Bool(false)`
- **AND** the same result is produced with the operands swapped (`b && a`)

#### Scenario: Determining true operand absorbs an error in the other OR operand

- **WHEN** the evaluator is implemented and `a || b` is evaluated where `a` evaluates to `CelValue.Bool(true)` and `b` produces an evaluation error
- **THEN** the result is a successful `CelEvaluationResult` with value `CelValue.Bool(true)`
- **AND** the same result is produced with the operands swapped (`b || a`)

#### Scenario: Non-determining operand does not absorb an error

- **WHEN** the evaluator is implemented and `a && b` is evaluated where `a` evaluates to `CelValue.Bool(true)` and `b` produces an evaluation error
- **THEN** `CelEvaluationResult.IsSuccess` is `false` with an `EvaluationFailure` diagnostic
- **AND** the analogous `a || b` case with `a` evaluating to `false` and `b` erroring also fails

#### Scenario: Error-free logical evaluation combines normally

- **WHEN** the evaluator is implemented and `a && b` is evaluated with both operands evaluating without error
- **THEN** the result is `CelValue.Bool(true)` if and only if both are `true`, otherwise `CelValue.Bool(false)`

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

Profile v1 SHALL support exactly the following built-in function overloads. This table is the complete, closed overload catalog for v1 — the binder (#326) SHALL resolve function calls against it and nothing else; no other receiver type, argument arity, or argument type is valid for these names:

| Function | Receiver type | Argument types | Result type | Diagnostic on mismatch |
| --- | --- | --- | --- | --- |
| `startsWith` | `String` | (`String`) | `Bool` | `TypeMismatch` (wrong receiver or argument type); `BindingError` (wrong arity) |
| `endsWith` | `String` | (`String`) | `Bool` | `TypeMismatch` (wrong receiver or argument type); `BindingError` (wrong arity) |
| `contains` | `String` | (`String`) | `Bool` | `TypeMismatch` (wrong receiver or argument type); `BindingError` (wrong arity) |
| `size` | `String` | () | `Int` (Unicode code-point count) | `TypeMismatch` (unsupported receiver); `BindingError` (any argument supplied) |
| `size` | `List` | () | `Int` (element count) | `TypeMismatch` (unsupported receiver); `BindingError` (any argument supplied) |
| `size` | `Map` | () | `Int` (entry count) | `TypeMismatch` (unsupported receiver); `BindingError` (any argument supplied) |
| `containsKey` | `Map` | (`String`) | `Bool` | `TypeMismatch` (wrong receiver or argument type); `BindingError` (wrong arity) |

Notes fixing ambiguity for #326:

- `contains` is a **string-only** receiver function in v1 (substring test). List membership is expressed with the `in` operator, not a `contains` overload; a `contains` call on a `List`/`Map`/`Object` receiver is a compile-time `TypeMismatch`.
- `size()` is supported on exactly `String`, `List`, and `Map` receivers. `size()` on `Bool`, `Int`, `Float`, or `Object` is a compile-time `TypeMismatch`. There is no free-function `size(x)` form in v1 — only receiver call syntax `x.size()`.
- `String.size()` counts **Unicode code points**, matching the pinned CEL Language Definition's model of a string as a sequence of code points — NOT .NET's `string.Length` (UTF-16 code units). A character outside the Basic Multilingual Plane (e.g. `"😀"`, one code point encoded as a surrogate pair) has `size() == 1`, not 2. Combining sequences are NOT collapsed: `"e" + U+0301` (combining acute) is two code points, so `size() == 2` — code points, not grapheme clusters. The built-in function invoker (#327) SHALL count Unicode scalar values (e.g. via `System.Text.Rune` enumeration), never `string.Length`.
- Malformed UTF-16 cannot reach `size()`: `CelValue.String()` rejects .NET strings containing unpaired surrogates at construction with `ArgumentException` (programmer misuse), because such strings do not represent a valid sequence of Unicode code points and therefore cannot be CEL string values.
- `containsKey` is a **map-only** receiver function; keys are `String` (v1 maps are string-keyed). Calling it on any other receiver is a compile-time `TypeMismatch`. Its evaluation-time behavior on a missing key (returns `false`, never fails) is defined in the logical-operator/error-semantics requirement.
- Calling an unknown function name is a compile-time `BindingError`; all mismatches in this table are compile-time diagnostics — no function-dispatch error can survive to evaluation time.

The `matches` function and all regex, timestamp, duration, protobuf, byte/string-conversion, and user-defined functions are deferred and SHALL NOT be resolvable in a Profile v1 compilation (compile-time `UnsupportedFeature` when the name is a known-deferred CEL built-in, `BindingError` otherwise).

#### Scenario: Supported function overloads are fully enumerated in spec

- **WHEN** the `cel-profile-v1` spec is read
- **THEN** each supported function overload is listed with its receiver type, argument types, result type, and mismatch diagnostic
- **AND** `matches` and regex are explicitly listed as deferred

#### Scenario: size() on an unsupported receiver is a compile-time type mismatch

- **WHEN** an expression calls `x.size()` where `x` is `Bool`, `Int`, `Float`, or `Object`-typed
- **THEN** compilation fails with a `TypeMismatch` diagnostic

#### Scenario: contains on a list receiver is a compile-time type mismatch

- **WHEN** an expression calls `list.contains(x)` on a `List`-typed receiver
- **THEN** compilation fails with a `TypeMismatch` diagnostic (list membership uses the `in` operator)

#### Scenario: size() counts code points for a BMP string

- **WHEN** the evaluator is implemented and `s.size()` is evaluated with `s` bound to `"abc"`
- **THEN** the result is `CelValue.Int(3)`

#### Scenario: size() counts a surrogate-pair character as one code point

- **WHEN** the evaluator is implemented and `s.size()` is evaluated with `s` bound to `"😀"` (U+1F600, one code point, two UTF-16 units)
- **THEN** the result is `CelValue.Int(1)`

#### Scenario: size() counts combining sequences as separate code points

- **WHEN** the evaluator is implemented and `s.size()` is evaluated with `s` bound to `"é"` (LATIN SMALL LETTER E followed by COMBINING ACUTE ACCENT)
- **THEN** the result is `CelValue.Int(2)`

#### Scenario: CelValue.String rejects malformed UTF-16

- **WHEN** `CelValue.String()` is called with a .NET string containing an unpaired surrogate (e.g. `"\ud83d"` alone)
- **THEN** an `ArgumentException` is thrown

### Requirement: Immutable public environment and compilation lifecycle

`CelEnvironment` SHALL be immutable and thread-safe after construction. It SHALL capture exactly: the profile (`CelProfile`), a closed `CelContextSchema`, `CelCompilationLimits`, environment-level `CelEvaluationLimits` (the evaluation ceiling), and the closed object-schema catalog (`ObjectSchemas`, frozen at `Build()`); Profile v1 defines no separate semantic-options object (see the cache-identity requirement). The builder SHALL expose `WithContextSchema`, `WithCompilationLimits`, `WithEvaluationLimits`, and `WithObjectSchema`. The environment SHALL expose `CompilePredicate(string source)` returning `CelCompilationResult<CelCompiledPredicate>`, `Compile(string source)` returning `CelCompilationResult<CelCompiledExpression>`, and `CreateEvaluationContextBuilder()` (catalog-aware; see the object-schema requirement). Normal compilation SHALL always parse, bind, and type-check the whole expression; parse-only and partial-check paths SHALL NOT exist as public API. No mutation or function registration SHALL be possible after build — mutating the builder after `Build()` SHALL NOT affect an already-built environment.

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

`CelContextSchema` SHALL be immutable after construction. It SHALL be identified structurally (a deterministic identity derived from profile, variable names, and type descriptors). `CelContextSchemaBuilder` SHALL return a `CelVariable` handle per `AddVariable()` call. Duplicate variable names during build SHALL produce a programmer-error exception. Variable and object-member names SHALL be validated at declaration time per the pinned CEL lexer (`IDENT = SELECTOR - RESERVED`, `SELECTOR = identifier-regex - KEYWORD`): a variable name SHALL be a valid `IDENT` — matching `[_a-zA-Z][_a-zA-Z0-9]*` and being neither a CEL keyword (`false`, `in`, `null`, `true`) nor a reserved identifier (`as`, `break`, `const`, `continue`, `else`, `for`, `function`, `if`, `import`, `let`, `loop`, `package`, `namespace`, `return`, `var`, `void`, `while`); an object-member name SHALL be a valid `SELECTOR` — the same grammar excluding only keywords, because the CEL grammar permits reserved identifiers in member-access position. A name failing its rule is rejected with `ArgumentException`, preserving the guarantee that every schema-declared name is reachable from a Profile v1 expression. `CelEvaluationContext` SHALL be built via `schema.CreateEvaluationContextBuilder()` and `builder.Set(variable, value)` using the variable handle (not a string key). `CelEvaluationContextBuilder` SHALL additionally expose `Set(string name, CelValue value)` as an ergonomic convenience overload that resolves `name` to its declared `CelVariable` handle via a single lookup and delegates to `Set(CelVariable, CelValue)`; an unknown name SHALL throw `ArgumentException`. The name-based overload exists for benchmarking and ergonomic call sites (see #168); the handle-based overload remains the recommended path for high-volume evaluation to avoid repeated string lookup. Missing variable assignments, duplicate assignments, and type mismatches SHALL produce deterministic errors.

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

### Requirement: Object schema model and strict object-value validation

`CelObjectSchema` SHALL be a sealed, immutable, structurally-identified description of one object type: it SHALL carry `ObjectTypeId` (string), `Members` (frozen `IReadOnlyList<CelObjectMember>` in declaration order), and a deterministic, collision-safe `Identity` string derived from the type id plus member names and types. `CelObjectMember` SHALL be sealed and immutable with `Name` and `Type` properties. `CelObjectSchemaBuilder` SHALL be created via `CelObjectSchema.CreateBuilder(objectTypeId)`; `AddMember(name, type)` SHALL reject duplicate member names with a programmer-error exception.

`CelEnvironmentBuilder.WithObjectSchema(objectSchema)` SHALL register an object schema in the environment's closed catalog; registering two schemas with the same `ObjectTypeId` SHALL throw `ArgumentException`. The catalog SHALL be frozen at `Build()` (defensively copied) and exposed as `CelEnvironment.ObjectSchemas`. Every registered object schema's identity SHALL participate in the compilation key's schema identity.

`CelEnvironment.CreateEvaluationContextBuilder()` SHALL be the standard way to build evaluation contexts when object-typed variables exist: it pre-loads the builder with the environment's object schema catalog. Object-value validation in `Set()` SHALL be strict, matching Profile v1's no-null/no-optional-members model: the supplied `CelObjectValue`'s member set SHALL exactly equal the registered schema's member set — a missing declared member, an extra undeclared member, and an object type id with no registered schema SHALL all be rejected with `ArgumentException`; each present member's value SHALL be recursively validated against its declared type. A builder created via `schema.CreateEvaluationContextBuilder()` (no catalog) SHALL throw `InvalidOperationException` when asked to validate an object value, rather than accepting it unvalidated — there is no path to construct a `CelEvaluationContext` containing an unvalidated object value.

#### Scenario: Object value with exactly the declared members passes validation

- **WHEN** `Set()` receives an object value whose members exactly match the registered `CelObjectSchema` (same names, valid types)
- **THEN** the assignment succeeds

#### Scenario: Object value missing a declared member is rejected

- **WHEN** `Set()` receives an object value that omits a member declared in its registered schema
- **THEN** an `ArgumentException` is thrown

#### Scenario: Object value with an undeclared extra member is rejected

- **WHEN** `Set()` receives an object value carrying a member name not declared in its registered schema
- **THEN** an `ArgumentException` is thrown

#### Scenario: Object value of an unregistered type is rejected

- **WHEN** `Set()` receives an object value whose `ObjectTypeId` has no registered `CelObjectSchema` in the catalog
- **THEN** an `ArgumentException` is thrown

#### Scenario: Nested object values are validated recursively

- **WHEN** `Set()` receives an object value containing a nested object member that is itself missing a declared member
- **THEN** an `ArgumentException` is thrown

#### Scenario: Catalog-less builder refuses to validate object values

- **WHEN** `Set()` is called with an object value on a builder created via `schema.CreateEvaluationContextBuilder()` without an object schema catalog
- **THEN** an `InvalidOperationException` is thrown directing the caller to `CelEnvironment.CreateEvaluationContextBuilder()`

#### Scenario: Duplicate object schema registration is a programmer error

- **WHEN** `WithObjectSchema` is called twice with schemas sharing the same `ObjectTypeId`
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

`CelDiagnostic` SHALL carry: `Code` (`CelDiagnosticCode` enum), `Category` (string), `Severity` (`CelDiagnosticSeverity` enum), `Span` (`CelSourceSpan` — nullable for non-source-locatable diagnostics), `Message` (display-only, not the machine contract), and `Parameters` (a frozen `IReadOnlyDictionary<string, string>` of structured, machine-readable values keyed by stable parameter names — e.g. `expectedType`, `actualType`, `identifier`, `limitName`, `observedValue` — empty when a diagnostic carries none). Machine consumers SHALL read `Code` and `Parameters`, never parse `Message`. `CelDiagnosticCode` SHALL distinguish at minimum: `SyntaxError`, `UnsupportedFeature`, `BindingError`, `TypeMismatch`, `SchemaMismatch`, `BudgetExceeded`, `EvaluationFailure`, `NotYetImplemented`. Display messages SHALL NOT be treated as machine-readable identifiers. The parser/binder/evaluator (#325, #326, #328) SHALL populate `Parameters` for `TypeMismatch` (expected/actual type), `BindingError` (identifier), `SchemaMismatch` (identifier), and `BudgetExceeded` (limit name, observed value) diagnostics.

#### Scenario: Compilation diagnostics carry stable codes

- **WHEN** a compilation returns `IsSuccess = false`
- **THEN** each diagnostic in `result.Diagnostics` has a non-null `Code` of type `CelDiagnosticCode`
- **AND** the `Code` is one of the declared enum values

#### Scenario: Source span is structurally defined

- **WHEN** a `CelDiagnostic` has a non-null `Span`
- **THEN** `Span.Start` and `Span.End` are non-negative integers representing character offsets

#### Scenario: BudgetExceeded diagnostics carry structured parameters

- **WHEN** a compilation fails with a `BudgetExceeded` diagnostic
- **THEN** `diagnostic.Parameters` contains `limitName` and `observedValue` entries
- **AND** `Parameters` cannot be cast back to a mutable dictionary

### Requirement: Compilation and evaluation limits are immutable with safe defaults

`CelCompilationLimits` and `CelEvaluationLimits` SHALL be sealed, immutable classes with all-or-nothing safe-default factories (`SafeDefaults`). Every public compilation and evaluation path SHALL be intrinsically bounded; no unbounded execution overload SHALL exist. Convenience overloads that omit limits SHALL use documented bounded defaults, not unrestricted execution.

The evaluation-limits enforcement model is: `CelCompiledPredicate` and `CelCompiledExpression` SHALL capture their environment's `CelEnvironment.EvaluationLimits` at compile time and expose it as a readable `EvaluationLimits` property (the ceiling). `Evaluate(context, limits)` SHALL throw `ArgumentException` (programmer misuse) when any budget in `limits` exceeds the corresponding budget in the captured ceiling — per-call limits may only tighten. `Evaluate(context)` SHALL be a convenience overload equivalent to `Evaluate(context, this.EvaluationLimits)` — it uses the captured environment ceiling, never a global default, so an environment built with a tight ceiling can never be evaluated with a looser budget through any overload. This satisfies the "safe-default evaluation overload" benchmarked by #168 without introducing an unbounded evaluation path. Because the captured ceiling is runtime policy owned by the compiled program, it SHALL participate in cache identity (see the cache-identity requirement).

The complete compilation-time budget surface that #325 (tokenizer/parser) and #326 (binder/checker) SHALL enforce comprises, at minimum: maximum expression source length in UTF-16 characters (`MaxExpressionLength`, already present); maximum token count produced by the tokenizer (`MaxTokenCount`); maximum AST node count produced by parsing (`MaxAstNodeCount`); maximum sub-expression nesting depth (`MaxNestingDepth`, already present); maximum literal size — the longest string/collection literal accepted in source (`MaxLiteralSize`); maximum distinct identifier references (`MaxIdentifierCount`, already present). Each of these limits SHALL be a positive-only field on `CelCompilationLimits`, SHALL be included as a named component of `CelCompilationLimits.ComputeIdentity()`, and a source exceeding any one of them SHALL produce a `BudgetExceeded` diagnostic before further processing of that phase. `MaxExpressionLength` remains the cheapest, first-checked gate (rejecting oversized source before tokenization even begins); the token/AST/literal limits are checked as each corresponding phase runs. Fields for limits not yet enforced by a landed phase (#325/#326 pending) SHALL still exist on `CelCompilationLimits` so the API shape does not change once those phases ship — an unenforced field SHALL be documented as "reserved, not yet enforced" in its XML doc until the enforcing phase lands.

The complete evaluation-time budget surface that #328 (evaluator) SHALL enforce comprises, at minimum: maximum evaluation steps (`MaxIterations`, already present); maximum accumulated abstract cost units (`MaxCostUnits`, already present); maximum input-value structural depth accepted by `CelEvaluationContextBuilder.Set()` (already enforced pre-evaluator via `MaxValidationDepth`, an internal constant — not yet a public per-environment field); maximum input collection size (element/entry count) accepted by `Set()` for `List`/`Map`/`Object` values (already enforced pre-evaluator via `MaxValidationCollectionSize`, an internal constant — not yet a public per-environment field); maximum cumulative input-value node count visited across one `Set()` call's recursive structural traversal (already enforced pre-evaluator via `MaxValidationNodeCount`, an internal constant — not yet a public per-environment field). The per-collection cap alone does not bound total validation work: a shallow structure such as a 1024-element list of 1024-element lists keeps every individual collection within `MaxValidationCollectionSize` while still visiting over one million value nodes, so `MaxValidationNodeCount` is a single counter shared across the entire recursive traversal of one `Set()` call, and validation stops immediately once it is exceeded. Exceeding any evaluation-time limit SHALL produce a failed `CelEvaluationResult` with a `BudgetExceeded` diagnostic, not a CLR exception, except where the limit is enforced synchronously inside `Set()` (structural depth, collection size, cumulative node count) — those SHALL continue to be reported as `ArgumentException` per the "Immutable context schema and schema-bound activation" requirement, since `Set()` is a builder-time programmer-facing call, not an evaluation call. Like `MaxValidationDepth` and `MaxValidationCollectionSize`, `MaxValidationNodeCount` is deliberately an immutable Profile v1 constant rather than a `CelEvaluationLimits` field in v1; if a future profile version makes it caller-configurable, it SHALL move onto `CelEvaluationLimits` and become a named component of `CelEvaluationLimits.ComputeIdentity()` / `CelCompilationKey.EvaluationLimitsIdentity`.

#### Scenario: SafeDefaults factories are accessible

- **WHEN** a consumer reads `CelCompilationLimits.SafeDefaults` or `CelEvaluationLimits.SafeDefaults`
- **THEN** a non-null limits instance is returned

#### Scenario: No unbounded execution overload exists

- **WHEN** the public API surface of `CelCompiledPredicate` and `CelCompiledExpression` is inspected
- **THEN** there is no `Evaluate()` overload that accepts no limits and performs unrestricted evaluation
- **AND** the `Evaluate(context)` overload that omits an explicit limits argument internally uses the compiled program's captured `EvaluationLimits` ceiling

#### Scenario: Default Evaluate overload delegates using the captured ceiling

- **WHEN** a consumer calls `predicate.Evaluate(context)`
- **THEN** the result is identical to calling `predicate.Evaluate(context, predicate.EvaluationLimits)`

#### Scenario: Per-call limits exceeding the captured ceiling are rejected

- **WHEN** a consumer calls `predicate.Evaluate(context, limits)` where `limits.MaxIterations` or `limits.MaxCostUnits` exceeds the corresponding budget in `predicate.EvaluationLimits`
- **THEN** an `ArgumentException` is thrown before any evaluation work occurs

#### Scenario: Tighter per-call limits are accepted

- **WHEN** a consumer calls `predicate.Evaluate(context, limits)` where every budget in `limits` is less than or equal to the corresponding budget in `predicate.EvaluationLimits`
- **THEN** evaluation proceeds under `limits`

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

#### Scenario: Set() rejects a structure exceeding the cumulative node budget despite every collection being within the size limit

- **WHEN** `CelEvaluationContextBuilder.Set()` is called with a value whose every individual `List`/`Map` collection is within `MaxValidationCollectionSize`, but the total number of value nodes visited across the full recursive structure exceeds the internal cumulative node budget
- **THEN** an `ArgumentException` is thrown

#### Scenario: Set() accepts a structure within the cumulative node budget

- **WHEN** `CelEvaluationContextBuilder.Set()` is called with a structurally-matching value whose total visited node count is within the internal cumulative node budget
- **THEN** the value is accepted

### Requirement: CEL value model rejects null and malformed structural inputs

`CelValue.List`, `CelValue.Map`, and the `CelObjectValue` constructor SHALL reject null elements/values at construction: Profile v1 defines no null CEL value, so a null could never be validly nested inside a `List`, `Map`, or object member. `CelValue.Map` SHALL additionally validate every key against the same UTF-16 well-formedness rule that `CelValue.String` enforces, since map keys are CEL strings. `CelEvaluationContextBuilder`'s internal structural-match validation SHALL treat a null `CelValue` node as a defensive validation failure (not a CLR exception), as a fail-closed guard in case this invariant is ever violated upstream of `Set()`.

#### Scenario: CelValue.List rejects a null element

- **WHEN** `CelValue.List` is called with a list containing a null element
- **THEN** an `ArgumentException` is thrown

#### Scenario: CelValue.Map rejects a null value

- **WHEN** `CelValue.Map` is called with a dictionary containing a null value
- **THEN** an `ArgumentException` is thrown

#### Scenario: CelValue.Map rejects a malformed UTF-16 key

- **WHEN** `CelValue.Map` is called with a dictionary whose key contains an unpaired UTF-16 surrogate
- **THEN** an `ArgumentException` is thrown

#### Scenario: CelObjectValue rejects a null member value

- **WHEN** the `CelObjectValue` constructor is called with a members dictionary containing a null value
- **THEN** an `ArgumentException` is thrown

### Requirement: Cache identity is deterministic and caller-owned

`CelCompilationKey` SHALL be a sealed, structurally-equal value object encoding: expression source, `CelProfileId`, structural schema identity, required result type (predicate vs general), compilation limits identity, and environment evaluation-limits identity (compiled programs capture their environment's evaluation ceiling, so two environments with different evaluation maximums must not share a cached program). It SHALL implement `IEquatable<CelCompilationKey>` and override `GetHashCode()`. No process-global mutable cache SHALL exist in `ArchLinterNet.CEL`; cache lifetime SHALL be caller-owned.

The source component (`NormalizedSource`) is currently the raw, unmodified source string: safe whitespace normalization requires tokenization (string literals must not be collapsed) and is reserved until the tokenizer (#325) lands. Until then, two sources differing only in whitespace produce different keys — a cache miss, never an incorrect hit. Any future normalization change alters cache identity and therefore SHALL require a profile-version or documented key-format revision.

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

### Requirement: Tokenizer and parser implementation scope for Profile v1

`ArchLinterNet.CEL`'s tokenizer and parser (internal `ArchLinterNet.CEL.Parsing` namespace) SHALL
implement exactly the following lexical and syntactic scope for Profile v1, in addition to the
precedence, associativity, and operator/type/function requirements already fixed elsewhere in
this spec:

- Numeric literals SHALL NOT include a sign; `-` SHALL always tokenize as a standalone operator
  token. A leading `-` before a numeric literal SHALL be reported as `UnsupportedFeature`
  (arithmetic/unary-minus is deferred), never accepted as part of the literal.
- The tokenizer SHALL accept `null` literals, `u`/`U`-suffixed unsigned-integer literals, and
  `b"..."`/`B"..."` byte-string literals as valid tokens (they are normative CEL syntax), and the
  parser SHALL reject each with `UnsupportedFeature` at the point the grammar would otherwise
  accept it — never `SyntaxError`. The same applies to the arithmetic operators (`+ - * / %`) and
  the conditional operator (`? :`) when encountered in an expression-forming position.
  A malformed instance of any of these tokens (e.g. an unterminated byte-string) SHALL still be
  reported as `SyntaxError`, since it is not valid CEL syntax to begin with.
- String literals SHALL support `'...'` and `"..."` quoting with the escape sequences
  `\n \t \r \\ \' \" \` \? \a \b \f \v`, `\xHH`, `\uHHHH`, and `\UHHHHHHHH`, plus `r"..."` /
  `R"..."` raw-string quoting (no escape processing). CEL has no standalone `\0` escape (only
  three-digit octal, which is out of scope below), so `\0` SHALL be rejected as an unknown escape
  sequence, not silently treated as NUL. `\uHHHH` and `\UHHHHHHHH` SHALL both reject a codepoint
  in the UTF-16 surrogate range (`0xD800`-`0xDFFF`) — neither is a valid standalone Unicode scalar
  value. Triple-quoted strings and octal escape sequences are out of scope for Profile v1 lexing;
  adding them is a pure lexer addition reserved for a future profile version, not a
  grammar-restructuring change. An unterminated string literal or a malformed escape sequence
  SHALL be reported as `SyntaxError`.
- Identifiers SHALL be restricted to the pinned grammar's ASCII `IDENT`/`SELECTOR` alphabet
  (`[_a-zA-Z][_a-zA-Z0-9]*`); a non-ASCII letter (e.g. `é`) is not part of any identifier and
  SHALL be reported as `SyntaxError`, not silently accepted as a Unicode identifier character.
  Numeric literals follow the same ASCII-digit restriction (`[0-9]`), and a decimal point SHALL
  only be consumed as part of a `FLOAT_LIT` when followed by at least one digit — `3.` alone is
  not a valid float literal and SHALL tokenize as an `IntLiteral` followed by a separate `.`.
- `SELECTOR ("." SELECTOR)* "{" ... "}"` (message/proto literal construction — every segment,
  including the first, is `SELECTOR`-governed, not `IDENT`-governed; see the reserved-word bullet
  below) and a leading `.` before an identifier (root/absolute-qualified name syntax, e.g.
  `.pkg.Type`) are both valid CEL primary
  forms under the pinned grammar; Profile v1 defers both, so the parser SHALL report
  `UnsupportedFeature` for each — never `SyntaxError` — at the point the grammar would otherwise
  accept them, and this SHALL hold regardless of nesting position (e.g. inside call arguments,
  parenthesized sub-expressions, or index expressions), not only at the top level of the
  expression being compiled. A `"{" ... "}"` immediately following an expression SHALL only be
  classified as a message literal when that expression is itself a qualified-name shape (an
  identifier, or a chain of pure member accesses rooted in one) — a call result, index result, or
  literal is never a valid message-literal receiver under the pinned grammar, so e.g. `1{}` SHALL
  be `SyntaxError`, not `UnsupportedFeature`.
- A deferred construct (arithmetic, the conditional operator, a list/map/message literal, a
  root-qualified name) SHALL only be classified as `UnsupportedFeature` after the parser has
  verified its own syntax is complete and well-formed under the pinned grammar; a dangling or
  incomplete instance (e.g. `a +` with no right-hand operand, `a ? b` with no `:` and false
  branch, a bare `.` with no following identifier, or an unterminated `[`/`{`) SHALL be
  `SyntaxError`, since it is not valid CEL syntax to begin with — only a fully-formed but deferred
  construct is `UnsupportedFeature`. This validation SHALL follow the pinned grammar's actual
  sub-structure, not a simplified approximation: the conditional operator's true branch is
  `ConditionalOr` precedence (an unparenthesized nested ternary there is `SyntaxError`, not
  `UnsupportedFeature`) while its false branch is the full recursive `Expr` (an unparenthesized
  nested ternary there is valid and SHALL also be fully validated); deferred arithmetic SHALL be
  absorbed at the `Relation = Addition [Relop Addition]` grammar level — i.e. as part of parsing
  each comparison operand — rather than only recognized as a flat trailer once a fully-reduced
  `ConditionalOr` has already returned, so arithmetic combined with a comparison anywhere in the
  expression (e.g. `a + b == c`, or nested inside a ternary branch as in `a ? b + c == d : e`) is
  classified correctly instead of producing a spurious `SyntaxError` about a missing `:`/`)`; a
  message literal's field keys (`SELECTOR ("." SELECTOR)* "{" field ":" value ...  "}"`) SHALL be bare
  identifiers, never an arbitrary expression — `Type{1: 2}` and `Type{'field': 1}` are
  `SyntaxError`, unlike a standalone map literal (`{1: 2}`) whose keys are arbitrary expressions
  and remains `UnsupportedFeature`; this bare-identifier-field-key requirement SHALL apply
  identically when the message-literal receiver is a root-qualified name (e.g.
  `.pkg.Type{1: 2}` SHALL be `SyntaxError` for the same reason `Type{1: 2}` is, not bypass field
  validation by virtue of being root-qualified). Message-literal-receiver eligibility SHALL be
  determined by parse state (whether the immediately preceding syntax was a qualified-name
  chain with no intervening call, index, or parenthesization), not by the shape of the resulting
  internal syntax node alone — a parenthesized qualified name (e.g. `(Type)`, `(pkg.Type)`) is
  syntactically indistinguishable in shape from an unparenthesized one once parsed, but per the
  pinned grammar the qualified-name prefix of a message literal is a primary-level production,
  not a generic postfix step usable after any expression that happens to evaluate to an
  identifier shape; `(Type){field: 1}` SHALL therefore be `SyntaxError`, even though
  `Type{field: 1}` is `UnsupportedFeature`.
- A root-qualified name (a leading `.` before an identifier) SHALL only consume its own leading
  `"." IDENT` (plus an immediately following call, e.g. `.f(...)`, mirroring a non-root-qualified
  free function call); any further `.member`, `.call(...)`, `[index]`, or message-literal step
  SHALL be handled by the same parsing path used for a non-root-qualified identifier chain, not a
  separate bespoke path — so `.pkg.f()` (root-qualified receiver call) and `.pkg.Type{field: 1}`
  (root-qualified message literal) parse completely instead of leaving a trailing `(`/`{`
  unconsumed, and every `.member`/`[index]` step in a root-qualified chain after the leading one
  is bounded by `MaxNestingDepth` through the same mechanism a non-root-qualified chain uses.
- A reserved identifier (see the existing `IDENT = SELECTOR - RESERVED` / `SELECTOR =
  identifier-regex - KEYWORD` distinction) that begins a primary expression is only valid as the
  root of a `SELECTOR ("." SELECTOR)*` chain that ITSELF terminates in a message literal
  (`"{" fieldInits "}"`) — a reserved root is never a valid plain qualified-name reference, call
  target, or index target on its own. This is a whole-chain requirement, not satisfied merely by
  the token immediately following the reserved root being `.`: the parser SHALL track this as
  pending state through the entire chain (every `.member` step the chain passes through, however
  many segments), resolving it only once the chain is known to have ended — satisfied the moment
  a message literal (`{...}`) is actually reached anywhere in the chain, and reported as
  `SyntaxError` if the chain instead ends via a call, an index, or simply no further tokens, with
  no message literal ever reached. This rule applies identically whether the reserved identifier
  is root-qualified or not: `package{field: 1}`, `package.Type{field: 1}`,
  `package.Type.Other{field: 1}`, and `.package{field: 1}` SHALL all be `UnsupportedFeature`
  (valid, deferred qualified-name/message-literal syntax), while `package()`, `package.Type`,
  `package.Type()`, `package.Type[0]`, `.package`, `.package()`, and `.package.Type` SHALL all be
  `SyntaxError` (a reserved word can never be a callable name, an index target, or a bare/plain
  qualified-name reference, root-qualified or not, unless the chain it roots eventually reaches a
  message literal). A reserved word used as an ordinary member-selector or receiver-call name on a
  non-reserved receiver (e.g. `x.package()`, `x.package`) is unaffected by this rule — it was
  already `SELECTOR`-governed under the existing member-access requirement, and the pending-state
  tracking above only ever originates from a reserved *root* token, never from a reserved word
  reached partway through a chain whose root was not itself reserved.
- The decision to classify an expression as `UnsupportedFeature` (as opposed to allowing parsing
  to continue toward a normal result) SHALL be deferred until the entire top-level expression has
  finished parsing successfully — every enclosing `(`/`[`/`{` matched with its closing
  `)`/`]`/`}`, every ternary's `:` and false branch present, full input consumed. A deferred
  construct's own syntax being valid SHALL NOT cause the parser to stop validating whatever
  encloses it; only the first such classification decision made SHALL be reported (first deferred
  construct encountered, in a diagnostic-stability sense — not necessarily the syntactically
  outermost one).
- A unary prefix chain (`"!" {"!"} Member` or `"-" {"-"} Member`) SHALL repeat only the same
  operator; mixing `!` and `-` in one prefix chain (e.g. `!-x`, `-!x`) has no valid CEL
  interpretation under the pinned grammar and SHALL be `SyntaxError`, not `UnsupportedFeature`.
- A reserved identifier (`as`, `break`, `const`, `continue`, `else`, `for`, `function`, `if`,
  `import`, `let`, `loop`, `package`, `namespace`, `return`, `var`, `void`, `while`) used as a bare
  primary expression reference SHALL be `SyntaxError`; the same word used in member-selector
  position (after `.`, as a member name or call name) SHALL parse successfully, per this spec's
  existing `IDENT = SELECTOR - RESERVED` / `SELECTOR = identifier-regex - KEYWORD` distinction —
  whether it resolves is compile-time binder territory (#326), not a parser concern.
- Free-function call syntax (`IDENT "(" args ")"` with no receiver) SHALL parse successfully even
  though Profile v1's function catalog declares no free functions; resolving whether the name is
  a known function is compile-time binder territory (#326), consistent with this spec's existing
  "calling an unknown function name is a compile-time `BindingError`" note.
- The tokenizer's whitespace token SHALL be restricted to exactly tab, newline, form-feed,
  carriage return, and space, matching the pinned grammar — not the broader .NET
  `char.IsWhiteSpace()` category, which also accepts non-ASCII Unicode space separators (e.g.
  U+00A0 NBSP) and other whitespace-category characters (e.g. U+000B vertical tab) the grammar
  does not include; such a character SHALL be reported as `SyntaxError`.
- An unescaped carriage return (`\r`), like an unescaped newline (`\n`), SHALL terminate a
  single/double-quoted or raw string literal with `SyntaxError` rather than being silently
  absorbed into the string's content.
- The tokenizer SHALL recognize the combined byte-plus-raw string prefix (`br"..."`/`Br"..."`/
  `bR"..."`/`BR"..."`, byte marker first per `BYTES_LIT : ("b"|"B") STRING_LIT` /
  `STRING_LIT : ["r"|"R"] STRING`), not only the single-marker forms. `\x`/`\X` (byte-value
  escapes) SHALL both be accepted as equivalent, and `\u` (4-digit Unicode escape) SHALL be
  accepted inside a byte-string literal exactly as in a string literal; only `\U` (8-digit
  Unicode escape) SHALL be rejected inside a byte-string literal — the pinned grammar's
  byte-string escape set includes `\u` but not `\U`.
- `MaxLiteralSize` SHALL bound element/entry count during list/map/message-literal syntax
  validation (each parsed element or `key : value` entry counted as it is validated), matching its
  documented "element count for list/map literals" contract, in addition to the already-enforced
  string/byte-string content-length bound.
- The parser SHALL enforce `MaxNestingDepth` against every postfix member-access (`.selector`) or
  indexing (`[...]`) step in a chain, not only against recursive constructs like parenthesized
  sub-expressions — the public `MaxNestingDepth` documentation explicitly lists "member access
  chains" as bounded by this limit.
- The parser SHALL enforce `MaxIdentifierCount` by counting each distinct identifier reference
  (a bare variable reference, a function name in a call, or a member-selector name) as it is
  consumed, producing `BudgetExceeded` the moment the count is exceeded — this is a purely
  syntactic count requiring no binder/schema information, so #325 enforces it directly rather than
  deferring it to #326's binder pass.
- On the first syntax error, unsupported-feature condition, or structural-limit violation, parsing
  SHALL stop and return exactly that one diagnostic (fail-fast, no error recovery).
- Every diagnostic produced by the tokenizer or parser SHALL use diagnostic category `"parser"`
  and SHALL carry `profileId` in `Parameters`, consistent with this spec's blanket diagnostic
  requirement. `BudgetExceeded` diagnostics from the tokenizer/parser SHALL additionally carry
  `limitName` and `observedValue`, matching the existing `MaxExpressionLength` `BudgetExceeded`
  parameter shape.

#### Scenario: Negative numeric literal is reported as a deferred feature, not accepted

- **WHEN** the expression `-5 == 5` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic (arithmetic/unary-minus is
  deferred in Profile v1)

#### Scenario: Deferred arithmetic operator is distinguished from invented syntax

- **WHEN** the expression `a + b` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: Invented operator syntax is a syntax error

- **WHEN** the expression `a => b` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: Deferred arithmetic is detected regardless of nesting position

- **WHEN** the expression `f(a + b)` (arithmetic inside a call argument) is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not a generic `SyntaxError`
  about an unexpected closing token

#### Scenario: A non-ASCII letter is rejected, not absorbed into an identifier

- **WHEN** the expression `é` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A decimal point with no following digit does not form a float literal

- **WHEN** the source `3.` is tokenized
- **THEN** it produces an `IntLiteral` token for `3` followed by a separate `.` (`Dot`) token, not
  a single `FloatLiteral` token

#### Scenario: Message literal syntax is deferred, not a syntax error

- **WHEN** the expression `Type{field: 1}` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: Root-qualified name syntax is deferred, not a syntax error

- **WHEN** the expression `.pkg.Type` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: A standalone \0 escape is rejected

- **WHEN** the string literal `'\0'` is tokenized
- **THEN** tokenization fails with a `SyntaxError` diagnostic (unknown escape sequence)

#### Scenario: A surrogate-range \u escape is rejected

- **WHEN** the string literal `'\uD800'` is tokenized
- **THEN** tokenization fails with a `SyntaxError` diagnostic

#### Scenario: Reserved identifier is rejected as a bare reference but accepted as a member name

- **WHEN** the expression `if == true` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic
- **WHEN** the expression `x.if` is parsed against a schema declaring variable `x`
- **THEN** the expression parses successfully (member-selector position is valid; whether `if`
  resolves against `x`'s schema is a binder concern)

#### Scenario: Unterminated string literal is a syntax error, not an unsupported feature

- **WHEN** the expression `'unterminated` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: Every parser diagnostic carries the parser category and profile identifier

- **WHEN** any tokenizer or parser diagnostic is produced
- **THEN** `diagnostic.Category` equals `"parser"`
- **AND** `diagnostic.Parameters["profileId"]` equals `"arch-linter/cel/v1"`

#### Scenario: A dangling deferred operator with no operand is a syntax error, not a deferred feature

- **WHEN** the expression `a +` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: An incomplete conditional expression is a syntax error

- **WHEN** the expression `a ? b` (missing `:` and false branch) is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: An unterminated list literal is a syntax error

- **WHEN** the expression `[` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A bare leading dot with no following identifier is a syntax error

- **WHEN** the expression `.` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A message literal on a non-qualified-name receiver is a syntax error

- **WHEN** the expression `1{}` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature` (an integer
  literal is never a valid message-literal receiver)

#### Scenario: MaxIdentifierCount is enforced by the parser

- **WHEN** `CelCompilationLimits.MaxIdentifierCount` is `1` and the expression `a || b` (two
  identifier references) is parsed
- **THEN** compilation fails with a `BudgetExceeded` diagnostic carrying
  `limitName = "MaxIdentifierCount"`

#### Scenario: MaxNestingDepth is enforced against a member-access chain

- **WHEN** `CelCompilationLimits.MaxNestingDepth` is exceeded by the length of a non-parenthesized
  member-access chain (e.g. `a.b.c.d.e`)
- **THEN** compilation fails with a `BudgetExceeded` diagnostic carrying
  `limitName = "MaxNestingDepth"`

#### Scenario: A non-ASCII whitespace character is rejected

- **WHEN** the expression `true` followed by U+00A0 (NBSP) followed by `== false` is tokenized
- **THEN** tokenization fails with a `SyntaxError` diagnostic

#### Scenario: An unescaped carriage return inside a string literal is rejected

- **WHEN** the string literal `'a` followed by an unescaped carriage return followed by `b'` is
  tokenized
- **THEN** tokenization fails with a `SyntaxError` diagnostic

#### Scenario: A ternary's true branch containing arithmetic is fully validated, not misreported as a missing colon

- **WHEN** the expression `a ? b + c : d` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not a `SyntaxError` about a
  missing `:`

#### Scenario: A ternary's false branch may contain an unparenthesized nested ternary

- **WHEN** the expression `a ? b : c ? d : e` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic

#### Scenario: A ternary's true branch may not contain an unparenthesized nested ternary

- **WHEN** the expression `a ? b ? c : d : e` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: Arithmetic combined with a comparison is classified correctly, not as a missing colon

- **WHEN** the expression `a ? b + c == d : e` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not a `SyntaxError` about a
  missing `:`

#### Scenario: Arithmetic inside a comparison operand is deferred, not a syntax error

- **WHEN** the expression `a + b == c` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic

#### Scenario: An unterminated parenthesized sub-expression containing arithmetic is a syntax error

- **WHEN** the expression `(a + b` (missing closing paren) is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature` — the
  missing `)` SHALL be caught before the deferred-arithmetic classification is ever considered

#### Scenario: An unterminated call containing an arithmetic argument is a syntax error

- **WHEN** the expression `f(a + b` (missing closing paren) is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A root-qualified message literal's field key is validated the same as a non-root-qualified one

- **WHEN** the expression `.pkg.Type{1: 2}` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A parenthesized qualified name is not a message-literal receiver

- **WHEN** the expression `(Type){field: 1}` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A parenthesized member-access chain is not a message-literal receiver

- **WHEN** the expression `(pkg.Type){field: 1}` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A root-qualified free function call parses completely

- **WHEN** the expression `.f()` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic (not a `SyntaxError` about
  trailing input), since the call is fully consumed

#### Scenario: A root-qualified receiver call parses completely

- **WHEN** the expression `.pkg.f()` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic (not a `SyntaxError` about
  trailing input)

#### Scenario: MaxNestingDepth bounds a root-qualified member-access chain

- **WHEN** `CelCompilationLimits.MaxNestingDepth` is exceeded by the length of a root-qualified
  member-access chain (e.g. `.a.b.c.d.e`)
- **THEN** compilation fails with a `BudgetExceeded` diagnostic carrying
  `limitName = "MaxNestingDepth"`

#### Scenario: A reserved word immediately followed by a message literal brace is deferred, not a syntax error

- **WHEN** the expression `package{field: 1}` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: A reserved word as the root of a multi-segment qualified name is deferred

- **WHEN** the expression `package.Type{field: 1}` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: A root-qualified reserved word immediately followed by a message literal brace is deferred

- **WHEN** the expression `.package{field: 1}` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: A reserved word used as a callable free-function name is a syntax error

- **WHEN** the expression `package()` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A root-qualified reserved word with nothing following is a syntax error

- **WHEN** the expression `.package` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A root-qualified reserved word used as a call name is a syntax error

- **WHEN** the expression `.package()` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A reserved word as a member/call selector on a non-reserved receiver is unaffected

- **WHEN** the expression `x.package()` is parsed
- **THEN** it parses successfully as a receiver call named `package` on `x`

#### Scenario: A reserved-word-rooted chain with no message literal anywhere is a syntax error

- **WHEN** the expression `package.Type` (no trailing `{...}`) is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic — a `.` immediately following the
  reserved root is not by itself sufficient; the chain must actually reach a message literal

#### Scenario: A reserved-word-rooted chain ending in a call is a syntax error

- **WHEN** the expression `package.Type()` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A reserved-word-rooted chain ending in an index is a syntax error

- **WHEN** the expression `package.Type[0]` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A root-qualified reserved-word-rooted chain with no message literal is a syntax error

- **WHEN** the expression `.package.Type` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A root-qualified reserved-word-rooted chain ending in a call is a syntax error

- **WHEN** the expression `.package.Type()` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A reserved-word-rooted chain reaching a message literal through multiple segments is deferred

- **WHEN** the expression `package.Type.Other{field: 1}` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: A message literal field key must be a bare identifier

- **WHEN** the expression `Type{1: 2}` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A map literal key may be an arbitrary expression

- **WHEN** the expression `{1: 2}` (standalone brace, no qualified-name receiver) is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic (still deferred, just not a
  syntax error)

#### Scenario: Mixing '!' and '-' in a unary prefix chain is a syntax error

- **WHEN** the expression `!-x` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A combined byte-and-raw string prefix is recognized

- **WHEN** the source `br'a\nb'` is tokenized
- **THEN** it produces a single `BytesLiteral` token whose decoded value is the four raw
  characters `a`, `\`, `n`, `b` (escapes are not processed)

#### Scenario: A 4-digit Unicode escape inside a byte-string literal is accepted

- **WHEN** the byte-string literal `b'A'` is tokenized
- **THEN** it produces a `BytesLiteral` token whose decoded value is `"A"`

#### Scenario: An 8-digit Unicode escape inside a byte-string literal is rejected

- **WHEN** the byte-string literal `b'\U00000041'` is tokenized
- **THEN** tokenization fails with a `SyntaxError` diagnostic

#### Scenario: MaxLiteralSize bounds list-literal element count during validation

- **WHEN** `CelCompilationLimits.MaxLiteralSize` is exceeded by the number of elements in a list
  literal being validated (e.g. `[1, 2, 3, 4, 5]` against a limit of `3`)
- **THEN** compilation fails with a `BudgetExceeded` diagnostic carrying
  `limitName = "MaxLiteralSize"`

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
  `UnsupportedFeature` when the name is a known-deferred CEL built-in (per this spec's blanket
  `matches`/deferred-function requirement) or `BindingError` otherwise; a name that matches a
  catalog entry but with a wrong receiver type, argument type, or arity SHALL produce `TypeMismatch`
  for a type mismatch or `BindingError` for an arity mismatch, exactly as this spec's function-
  catalog table already specifies per function. A `TypeMismatch` for a wrong argument type SHALL
  identify the specific mismatched argument — `Parameters["expectedType"]`/`Parameters["actualType"]`
  SHALL describe that argument's declared and actual type, not the receiver's type, when the
  receiver itself already matched a candidate overload.
- `CelIndexSyntax` resolution SHALL type-check its receiver first; a `List` receiver SHALL require
  an `Int`-typed index expression and resolve to the list's element type, a `Map` receiver SHALL
  require a `String`-typed index expression and resolve to the map's value type, and any other
  receiver type or wrong index/key expression type SHALL produce `TypeMismatch`.
- Operator type checking for `CelUnarySyntax`/`CelBinarySyntax` SHALL apply the frozen signature
  table fixed elsewhere in this spec with no implicit `Int`/`Float` widening; a signature violation
  SHALL produce `TypeMismatch` carrying `Parameters["expectedType"]` and `Parameters["actualType"]`,
  attributed to whichever specific operand actually violates the signature (e.g. for a binary
  numeric-ordering operator, `Parameters["actualType"]` SHALL describe the operand that does not
  match — the left operand when it is not numeric at all, or the right operand when the left is
  numeric but the two operands' kinds differ — never an operand that already satisfies the
  signature) rather than defaulting to a fixed operand position.
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
  `Evaluate` methods SHALL continue to throw `NotImplementedException` until the evaluator (#328)
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

#### Scenario: A known-deferred built-in function name produces UnsupportedFeature, not BindingError

- **WHEN** the expression `x.matches('a.*')` is compiled against a schema declaring `x` as `String`
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `BindingError` (`matches`
  is a known-deferred CEL built-in, distinct from an entirely unknown function name)

#### Scenario: A wrong function-argument type is attributed to the argument, not the receiver

- **WHEN** the expression `x.startsWith(1)` is compiled against a schema declaring `x` as `String`
  (a valid receiver for `startsWith` — only the argument type is wrong)
- **THEN** compilation fails with a `TypeMismatch` diagnostic
- **AND** `diagnostic.Parameters["expectedType"]` equals `"String"` and
  `diagnostic.Parameters["actualType"]` equals `"Int"` (the argument's expected/actual type, not
  the receiver's already-matching type)

#### Scenario: Wrong index expression type produces a TypeMismatch

- **WHEN** the expression `list["not-an-int"]` is compiled against a schema declaring `list` as
  `List<Int>`
- **THEN** compilation fails with a `TypeMismatch` diagnostic

#### Scenario: A numeric-ordering operator with a non-numeric left operand attributes the mismatch to the left operand

- **WHEN** the expression `s < s` is compiled against a schema declaring `s` as `String`
- **THEN** compilation fails with a `TypeMismatch` diagnostic
- **AND** `diagnostic.Parameters["actualType"]` equals `"String"` (the left operand's own type, not
  a copy of the right operand's type)

#### Scenario: A numeric-ordering operator with mixed Int/Float operands attributes the mismatch to the differing right operand

- **WHEN** the expression `i < f` is compiled against a schema declaring `i` as `Int` and `f` as
  `Float`
- **THEN** compilation fails with a `TypeMismatch` diagnostic
- **AND** `diagnostic.Parameters["expectedType"]` equals `"Int"` and
  `diagnostic.Parameters["actualType"]` equals `"Float"`

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
  `ComputeCost` SHALL return a fixed floor of `1` plus the UTF-16 length of every string operand the
  operation scans (`startsWith`/`endsWith`: the argument string; `contains`/`size` on `String`: the
  receiver string, `contains` additionally the argument string) — a linear approximation, not each
  operation's exact worst-case algorithmic complexity. `size` on `List`/`Map` and `containsKey`
  SHALL cost only the fixed floor, since both are backed by an O(1) count field or hash lookup.
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

#### Scenario: ComputeCost scales with the size of the string data an operation scans

- **WHEN** `CelBuiltinFunctionInvoker.ComputeCost` is called for `Contains` or `SizeString` with a
  large receiver string, then again with a small receiver string, all else equal
- **THEN** the large-receiver call returns a strictly greater cost

#### Scenario: ComputeCost is constant for O(1) operations

- **WHEN** `CelBuiltinFunctionInvoker.ComputeCost` is called for `SizeList`, `SizeMap`, or
  `ContainsKey` with a small collection, then again with a large collection, all else equal
- **THEN** both calls return the same cost (the fixed floor)

#### Scenario: Invoke never throws for a binder-guaranteed-correct call shape

- **WHEN** `CelBuiltinFunctionInvoker.Invoke` is called with a receiver/argument shape matching one
  of the seven catalog overloads exactly
- **THEN** it returns a `CelValue` and never throws

#### Scenario: Evaluate remains unimplemented after this change

- **WHEN** a `CelCompiledPredicate` or `CelCompiledExpression` is obtained from a successful
  compilation after this change ships
- **THEN** calling `Evaluate` still throws `NotImplementedException`

