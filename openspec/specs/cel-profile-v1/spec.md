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

`CelContextSchema` SHALL be immutable after construction. It SHALL be identified structurally (a deterministic identity derived from profile, variable names, and type descriptors). `CelContextSchemaBuilder` SHALL return a `CelVariable` handle per `AddVariable()` call. Duplicate variable names during build SHALL produce a programmer-error exception. `CelEvaluationContext` SHALL be built via `schema.CreateEvaluationContextBuilder()` and `builder.Set(variable, value)` using the variable handle (not a string key). Missing variable assignments, duplicate assignments, and type mismatches SHALL produce deterministic errors.

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

`CelCompilationLimits` and `CelEvaluationLimits` SHALL be sealed, immutable classes with all-or-nothing safe-default factories (`SafeDefaults`). Per-call evaluation limits SHALL be allowed to tighten but not exceed the environment/profile maximums. Every public compilation and evaluation path SHALL be intrinsically bounded; no unbounded execution overload SHALL exist. Convenience overloads that omit limits SHALL use documented safe defaults, not unrestricted execution.

#### Scenario: SafeDefaults factories are accessible

- **WHEN** a consumer reads `CelCompilationLimits.SafeDefaults` or `CelEvaluationLimits.SafeDefaults`
- **THEN** a non-null limits instance is returned

#### Scenario: No unbounded execution overload exists

- **WHEN** the public API surface of `CelCompiledPredicate` and `CelCompiledExpression` is inspected
- **THEN** there is no `Evaluate()` overload that accepts no limits and performs unrestricted evaluation

### Requirement: Cache identity is deterministic and caller-owned

`CelCompilationKey` SHALL be a sealed, structurally-equal value object encoding: normalized expression source, `CelProfileId`, structural schema identity, required result type (predicate vs general), compilation limits identity. It SHALL implement `IEquatable<CelCompilationKey>` and override `GetHashCode()`. No process-global mutable cache SHALL exist in `ArchLinterNet.CEL`; cache lifetime SHALL be caller-owned.

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

