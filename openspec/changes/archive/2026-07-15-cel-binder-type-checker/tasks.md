## 1. Bound-expression representation

- [x] 1.1 Add `src/ArchLinterNet.CEL/Binding/CelBoundNode.cs` — abstract internal base with `Span` (`CelSourceSpan`) and `ResolvedType` (`CelType`).
- [x] 1.2 Add bound node types mirroring `CelSyntaxNode`: `CelBoundBoolLiteral`/`CelBoundIntLiteral`/`CelBoundFloatLiteral`/`CelBoundStringLiteral` (one per syntax literal type), `CelBoundIdentifier` (carries `CelVariable`), `CelBoundUnary`, `CelBoundBinary`, `CelBoundMemberAccess` (carries `CelObjectMember`), `CelBoundIndex`, `CelBoundCall` (carries resolved catalog overload).
- [x] 1.3 Add `CelBoundExpression` — immutable wrapper carrying the root `CelBoundNode` plus the root's resolved `CelType`, held internally by `CelCompiledPredicate`/`CelCompiledExpression`.

## 2. Binder diagnostics

- [x] 2.1 Add `src/ArchLinterNet.CEL/Binding/CelBindDiagnostics.cs` mirroring `Parsing/CelParseDiagnostics.cs`: `BindingError(identifier, span, profileId)`, `TypeMismatch(expectedType, actualType, span, profileId)`, `SchemaMismatch(identifier, span, profileId)`, all with `Category = "binder"`.

## 3. Function catalog

- [x] 3.1 Add `src/ArchLinterNet.CEL/Binding/CelFunctionCatalog.cs` — internal closed table of the seven Profile v1 overloads (`startsWith`, `endsWith`, `contains`, `size` ×3 receivers, `containsKey`) with receiver type, argument types, result type, keyed for lookup by function name + receiver-type-kind.

## 4. Binder / type checker core

- [x] 4.1 Add `src/ArchLinterNet.CEL/Binding/CelBindResult.cs` — internal `IsSuccess`/`Bound` (`CelBoundExpression?`)/`Diagnostic` wrapper, mirroring `CelParseResult`.
- [x] 4.2 Add `src/ArchLinterNet.CEL/Binding/CelBinder.cs` — internal binder entry point `Bind(CelSyntaxNode root, CelContextSchema schema, IReadOnlyDictionary<string, CelObjectSchema> objectSchemas, CelRequiredResultType requiredResultType, CelProfileId profileId)` returning `CelBindResult` (keyed by `string` `ObjectTypeId`, matching `CelEnvironment.ObjectSchemas`'s actual shape rather than a nonexistent `CelObjectTypeId` type).
- [x] 4.3 Implement literal binding (Bool/Int/Float/String) — trivial, resolved type = corresponding `CelTypeKind`.
- [x] 4.4 Implement `CelIdentifierSyntax` binding — resolve against `CelContextSchema`, produce `BindingError` on miss.
- [x] 4.5 Implement `CelUnarySyntax` binding — bind operand first (whole-AST), then check `Not: Bool -> Bool` signature, `TypeMismatch` on violation.
- [x] 4.6 Implement `CelBinarySyntax` binding — bind both operands unconditionally (whole-AST, no short-circuit skip), then check `And`/`Or`/`Equal`/`NotEqual`/`Less`/`LessOrEqual`/`Greater`/`GreaterOrEqual`/`In` signatures per the frozen table, no implicit Int/Float widening.
- [x] 4.7 Implement `CelMemberAccessSyntax` binding — bind receiver first, require `Object` static type, resolve member against the object's `CelObjectSchema`, `SchemaMismatch` on undeclared member, `TypeMismatch` on non-object receiver.
- [x] 4.8 Implement `CelIndexSyntax` binding — bind receiver and index first, `List<T>[Int] -> T`, `Map<String,T>[String] -> T`, `TypeMismatch` otherwise.
- [x] 4.9 Implement `CelCallSyntax` binding — bind receiver (if any) and arguments first, resolve against `CelFunctionCatalog`, `BindingError` for unknown name/arity, `TypeMismatch` for wrong receiver/argument type.
- [x] 4.10 Implement fixed left-to-right depth-first traversal returning the first diagnostic encountered (fail-fast, single diagnostic).
- [x] 4.11 Implement required-result-type enforcement: after the whole tree binds successfully, check root type against `CelRequiredResultType` (`Predicate` requires `Bool`), `TypeMismatch` with `expectedType`/`actualType` on violation.

## 5. Wiring

- [x] 5.1 Update `src/ArchLinterNet.CEL/CelEnvironment.cs` (`ParseAndBind`/`CompilePredicate`/`Compile`): after a successful `CelParser.Parse`, call `CelBinder.Bind`; on success construct the real `CelCompiledPredicate`/`CelCompiledExpression` carrying the `CelBoundExpression`; on failure return `CelCompilationResult<T>` with the binder's diagnostic instead of `NotYetImplemented`.
- [x] 5.2 Add an internal constructor/field on `CelCompiledPredicate`/`CelCompiledExpression` to carry the bound plan without changing their public shape.
- [x] 5.3 Confirm `Evaluate()` on both compiled types still throws `NotImplementedException` after this wiring change.

## 6. Tests

- [x] 6.1 Add `tests/ArchLinterNet.CEL.Tests/CelBinderTests.cs` — identifier/member/index/call resolution success and failure cases, one test per requirement scenario in the delta spec.
- [x] 6.2 Add operator type-check tests: all seven binary signatures (`&&`, `||`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `in`) both positive and negative, `!` positive/negative, no implicit Int/Float widening.
- [x] 6.3 Add function-catalog tests: each of the seven overloads positive; wrong receiver, wrong argument type, wrong arity for each.
- [x] 6.4 Add whole-AST binding test: an expression with an invalid sub-expression behind a determining `&&`/`||`/`in` operand still fails to compile.
- [x] 6.5 Add required-result-type tests: `CompilePredicate` rejects non-`Bool` root, `Compile` accepts any `CelTypeKind` root.
- [x] 6.6 Add diagnostic-shape tests: `Category == "binder"`, `profileId` parameter present, `identifier`/`expectedType`/`actualType` parameters present where applicable.
- [x] 6.7 Update `CelEnvironmentParsingTests.cs`, `CelApiShapeTests.cs`, `CelExternalConsumerSampleTests.cs`, `CelInternalApiCoverageTests.cs` confirming `NotYetImplemented` is no longer returned for syntactically-and-semantically-valid input (repurposed every test that asserted the old blanket stub behavior for valid input).
- [x] 6.8 Add a public-API-shape test asserting no `ArchLinterNet.CEL.Binding` type or bound-node type is publicly visible.
- [x] 6.9 Run `rtk dotnet test tests/ArchLinterNet.CEL.Tests --no-restore` and fix failures.

## 7. Docs and spec sync

- [x] 7.1 Update `docs/internal/cel-engine-architecture.md` binder/pipeline row to reflect shipped scope and remove the stale `MaxIdentifierCount`-at-binder note if still present.
- [x] 7.2 Compare implementation against `design.md` and the delta spec; adjust the delta spec only if implementation reveals a genuine scope gap (do not under- or over-claim behavior).
- [x] 7.3 Run `openspec validate --all`.
- [x] 7.4 Run `openspec archive cel-binder-type-checker` and verify the archived spec.

## 8. Validation and PR

- [x] 8.1 Run `rtk make fmt` and inspect formatting changes.
- [x] 8.2 Run `rtk make acceptance` (lint + full test suite) and fix any issue-related failures.
- [ ] 8.3 Open the pull request closing #326.
