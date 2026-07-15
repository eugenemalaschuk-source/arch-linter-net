## 1. Demote CelEngine and set up namespace

- [x] 1.1 Change `CelEngine` from `public sealed class` to `internal sealed class` and update its XML doc comment to reflect its internal engine role

## 2. Profile identity types

- [x] 2.1 Create `CelProfileId.cs` — readonly struct with `string Value`, `ToString()`, `Equals`, `GetHashCode`, implicit conversion from string
- [x] 2.2 Create `CelProfile.cs` — sealed class with `static readonly CelProfile V1`, `Id` property (`CelProfileId`), XML doc pinning CEL spec URL and profile ID value `"arch-linter/cel/v1"`

## 3. Type system types

- [x] 3.1 Create `CelValueKind.cs` — enum: `Bool`, `String`, `Int`, `Float`, `List`, `Map`, `Object`
- [x] 3.2 Create `CelType.cs` — sealed class with static properties `Bool`/`String`/`Int`/`Float` and static factory methods `ListOf(CelType elementType)`, `MapOf(CelType valueType)`, `ObjectOf(string schemaId)`. Properties `Kind` (CelTypeKind enum), `ElementType?`, `ValueType?`, `SchemaId?`
- [x] 3.3 Create `CelTypeKind.cs` — enum: `Bool`, `String`, `Int`, `Float`, `List`, `Map`, `Object`

## 4. Variable handle

- [x] 4.1 Create `CelVariable.cs` — sealed class with `string Name`, `CelType Type`, internal constructor (returned only by `CelContextSchemaBuilder.AddVariable`)

## 5. Context schema

- [x] 5.1 Create `CelContextSchema.cs` — sealed class with `string SchemaId`, `IReadOnlyList<CelVariable> Variables`, `string Identity` (deterministic), `static CelContextSchemaBuilder CreateBuilder(string schemaId)`, `CelEvaluationContextBuilder CreateEvaluationContextBuilder()`; XML doc declares immutability
- [x] 5.2 Create `CelContextSchemaBuilder.cs` — sealed class with `CelVariable AddVariable(string name, CelType type)` (throws `ArgumentException` on duplicate name), `CelContextSchema Build()`

## 6. CEL value model

- [x] 6.1 Create `CelObjectValue.cs` — sealed class with `string ObjectTypeId`, `IReadOnlyDictionary<string, CelValue> Members`, internal constructor; XML doc declares immutability
- [x] 6.2 Create `CelValue.cs` — sealed class with static factories `Bool(bool)`, `String(string)`, `Int(long)`, `Float(double)`, `List(IReadOnlyList<CelValue>)`, `Map(IReadOnlyDictionary<string, CelValue>)`, `Object(CelObjectValue)`; property `Kind` (`CelValueKind`); typed accessors `AsBool()`, `AsString()`, `AsInt()`, `AsFloat()`, `AsList()`, `AsMap()`, `AsObject()` throwing `InvalidOperationException` on kind mismatch; XML doc declares immutability; no `object`/`dynamic`/CLR-type constructor

## 7. Evaluation context

- [x] 7.1 Create `CelEvaluationContext.cs` — sealed class with `CelContextSchema Schema`, `IReadOnlyList<(CelVariable Variable, CelValue Value)> Assignments`; XML doc declares immutability
- [x] 7.2 Create `CelEvaluationContextBuilder.cs` — sealed class with `CelEvaluationContextBuilder Set(CelVariable variable, CelValue value)` (throws `ArgumentException` on duplicate, `ArgumentException` on type mismatch), `CelEvaluationContext Build()` (throws `InvalidOperationException` on missing required variable)

## 8. Diagnostics

- [x] 8.1 Create `CelDiagnosticSeverity.cs` — enum: `Error`, `Warning`, `Info`
- [x] 8.2 Create `CelDiagnosticCode.cs` — enum: `SyntaxError`, `UnsupportedFeature`, `BindingError`, `TypeMismatch`, `SchemaMismatch`, `BudgetExceeded`, `EvaluationFailure`, `NotYetImplemented`
- [x] 8.3 Create `CelSourceSpan.cs` — readonly struct with `int Start`, `int End`
- [x] 8.4 Create `CelDiagnostic.cs` — sealed class with `CelDiagnosticCode Code`, `string Category`, `CelDiagnosticSeverity Severity`, `CelSourceSpan? Span`, `string Message`; internal constructor

## 9. Limits

- [x] 9.1 Create `CelCompilationLimits.cs` — sealed class with `static readonly CelCompilationLimits SafeDefaults` (reasonable bounds), `int MaxExpressionLength`, `int MaxNestingDepth`, `int MaxIdentifierCount`; XML doc declares immutability
- [x] 9.2 Create `CelEvaluationLimits.cs` — sealed class with `static readonly CelEvaluationLimits SafeDefaults`, `int MaxIterations`, `long MaxCostUnits`; XML doc declares immutability

## 10. Cache identity

- [x] 10.1 Create `CelCompilationKey.cs` — sealed class implementing `IEquatable<CelCompilationKey>`, `Equals(object?)`, `GetHashCode()`; internal constructor; properties `NormalizedSource`, `ProfileId`, `SchemaIdentity`, `RequiredResultType` (enum: `Predicate`, `General`), `CompilationLimitsIdentity`; no mutable static cache anywhere in the assembly

## 11. Compiled programs

- [x] 11.1 Create `CelCompiledExpression.cs` — sealed class with `CelProfile Profile`, `CelContextSchema Schema`, `CelCompilationKey CompilationKey`, `CelCompilationLimits CompilationLimits`, `CelEvaluationResult Evaluate(CelEvaluationContext context, CelEvaluationLimits limits)` (stub: throws `NotImplementedException` with message "Evaluator not yet implemented — see #327"); XML doc declares immutable/thread-safe
- [x] 11.2 Create `CelCompiledPredicate.cs` — sealed class (same structure as `CelCompiledExpression` but `Evaluate` returns `CelEvaluationResult` that exposes `AsBool()`); XML doc declares immutable/thread-safe; no delegate conversion operator

## 12. Evaluation result

- [x] 12.1 Create `CelEvaluationResult.cs` — sealed class with `bool IsSuccess`, `CelValue? Value`, `IReadOnlyList<CelDiagnostic> Diagnostics`; typed accessor `AsBool()` returning `bool` (throws `InvalidOperationException` if `!IsSuccess` or `Value.Kind != Bool`); internal constructor

## 13. Compilation result

- [x] 13.1 Create `CelCompilationResult.cs` — sealed generic class `CelCompilationResult<T>` where `T : class`; properties `bool IsSuccess`, `T? Program`, `IReadOnlyList<CelDiagnostic> Diagnostics`, `CelCompilationKey CompilationKey`; static factory `static CelCompilationResult<T> NotYetImplemented(CelCompilationKey key)`; internal constructor

## 14. Environment and builder

- [x] 14.1 Create `CelEnvironmentBuilder.cs` — sealed class with `CelEnvironmentBuilder WithContextSchema(CelContextSchema schema)`, `CelEnvironmentBuilder WithCompilationLimits(CelCompilationLimits limits)`, `CelEnvironment Build()`
- [x] 14.2 Create `CelEnvironment.cs` — sealed class with `static CelEnvironmentBuilder CreateBuilder(CelProfile profile)`; `CelProfile Profile`, `CelContextSchema Schema`, `CelCompilationLimits CompilationLimits`; `CelCompilationResult<CelCompiledPredicate> CompilePredicate(string source)` (stub: returns `NotYetImplemented` result with `CelDiagnosticCode.NotYetImplemented` diagnostic); `CelCompilationResult<CelCompiledExpression> Compile(string source)` (same stub); XML doc declares immutable/thread-safe

## 15. Internal architecture blueprint

- [x] 15.1 Create `docs/internal/cel-engine-architecture.md` with: (a) processing pipeline diagram, (b) component ownership table, (c) extension-direction matrix covering all 7 required directions (language/profile, host adapters, function catalog, execution backends, tooling/AST, caching/serialization, diagnostics/explainability), (d) prohibited shortcuts section
- [x] 15.2 Add link to `docs/internal/cel-engine-architecture.md` in `docs/internal/README.md` under "Internal docs"

## 16. API shape and consumer tests

- [x] 16.1 Create `tests/ArchLinterNet.CEL.Tests/CelApiShapeTests.cs` — NUnit tests verifying: `CelProfile.V1` is non-null; builder chain returns correct types; `CompilePredicate` returns `CelCompilationResult<CelCompiledPredicate>`; result has `IsSuccess`, `Diagnostics`, `CompilationKey`; `CelValue` factories and accessors are correct kinds; `CelDiagnosticCode.NotYetImplemented` appears in stub compilation; `CelCompilationLimits.SafeDefaults` and `CelEvaluationLimits.SafeDefaults` are non-null; no `using ArchLinterNet.Core` directive
- [x] 16.2 Create `tests/ArchLinterNet.CEL.Tests/CelExternalConsumerSampleTests.cs` — single test showing the full interaction from the issue (`CreateBuilder → AddVariable → Build → CreateBuilder(V1) → WithContextSchema → Build → CompilePredicate → inspect result → CreateEvaluationContextBuilder → Set → Build`); no `using ArchLinterNet.Core` directive

## 17. Validation

- [x] 17.1 Run `rtk make fmt` and fix any formatting issues
- [x] 17.2 Run `rtk make acceptance` and fix any failures
- [x] 17.3 Run `rtk make lint-architecture` and verify no CEL → Core reverse dependency
- [x] 17.4 Verify `InternalsVisibleTo` in `ArchLinterNet.CEL.csproj` does not include `ArchLinterNet.Core`
- [x] 17.5 Verify `cel-engine-architecture.md` is not listed in `mkdocs.yml`
