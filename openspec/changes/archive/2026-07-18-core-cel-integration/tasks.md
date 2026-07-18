## 1. Contracts-layer CEL schemas

- [x] 1.1 Add `src/ArchLinterNet.Core/Contracts/Expressions/ArchitectureExpressionSchemas.cs`: closed `CelObjectSchema` for the shared subject/source/target shape (16 members) and the dependency shape (4 members), and three `CelContextSchema`/`CelEnvironment` pairs (selector `subject`; contextual source `source`; contextual target/exclusion `source`+`target`+`dependency`), all built once with `CelProfile.V1` and exposed as `static readonly`.

## 2. YAML model changes

- [x] 2.1 Add `When` (`[YamlMember(Alias = "when")] public string? When`) and `[YamlIgnore] internal CelCompiledPredicate? CompiledWhen { get; set; }` to `ArchitectureLayerSelector` (`Contracts/ArchitectureContractModels.cs`).
- [x] 2.2 Add the same `When`/`CompiledWhen` pair to `ArchitectureContextSelector` (`Contracts/ArchitectureContextSelector.cs`), with a comment noting the call-site-scoped acceptance (D4).

## 3. Raw-YAML key validation (call-site scoping)

- [x] 3.1 In `ArchitecturePolicyDocumentLoader.ValidateRawLayerYaml`'s selector-key loop, allow `"when"` alongside `"role"`/`"metadata"`.
- [x] 3.2 Add a `bool allowWhen` parameter to `ValidateContextualSelectorNodeKeys` and `ValidateContextualSelectorListKeys`, defaulting existing call sites to `false`.
- [x] 3.3 Pass `allowWhen: true` only from `ValidateContextualContractGroup`'s calls (source/target-list/exclude for `strict_context_dependencies`/`audit_context_dependencies`/`strict_context_allow_only`/`audit_context_allow_only`). Leave `ValidatePortBoundaryContractGroup`'s calls at the default `false`.

## 4. Compile-at-load validator

- [x] 4.1 Add `src/ArchLinterNet.Core/Contracts/Validators/ExpressionCompilationValidator.cs` implementing `IArchitecturePolicyDocumentValidator`: for each layer with `Selector?.When` set, compile via the selector-schema environment; for each `strict_context_dependencies`/`audit_context_dependencies`/`strict_context_allow_only`/`audit_context_allow_only` contract, compile `Source.When` via the contextual-source environment and each target-list/`Exclude[*].When` via the contextual-target environment.
- [x] 4.2 On any failed `CelCompilationResult`, throw `InvalidOperationException` with a deterministic message built from the returned `CelDiagnostic`(s) (code + message + span when present), following the same throw/catch/`EnrichValidationException` idiom as `ContextualContractValidator`.
- [x] 4.3 On success, set `CompiledWhen` on the owning selector. Do not touch selectors with no `When`.
- [x] 4.4 Register `new ExpressionCompilationValidator()` at the end of `ArchitecturePolicyDocumentValidatorPipeline.All`.

## 5. JSON Schema (composed-policy validation path)

- [x] 5.1 Add optional `when` (`{ "type": "string", "minLength": 1 }`) to `$defs/selector` in `schema/dependencies.arch.schema.json`.
- [x] 5.2 Add new `$defs/contextSourceSelectorWithExpression` and `$defs/contextSelectorWithExpression` (copies of `contextSourceSelector`/`contextSelector` plus optional `when`).
- [x] 5.3 Repoint `contextDependencyContract`'s `source`/`forbidden`/`exclude` and `contextAllowOnlyContract`'s `source`/`allowed`/`exclude` to the new `*WithExpression` defs. Leave `portBoundaryContract` and `adapterPortBinding` refs unchanged.

## 6. Execution-layer typed context factories

- [x] 6.1 Add `src/ArchLinterNet.Core/Execution/Expressions/ArchitectureExpressionSubjectFacts.cs` and `ArchitectureExpressionDependencyFacts.cs` (records matching the closed spec shapes).
- [x] 6.2 Add `src/ArchLinterNet.Core/Execution/Expressions/ArchitectureExpressionContextFactory.cs`: static pure mapping from the facts records to `CelValue`/`CelObjectValue`, and helpers building a `CelEvaluationContext` for each of the three context kinds, using the schemas from 1.1.
- [x] 6.3 Add `src/ArchLinterNet.Core/Execution/Expressions/ArchitectureExpressionEvaluationResult.cs` (wrapper: `IsMatch`, `IsError`, `ErrorMessage`) and a small static evaluator helper wrapping `CelCompiledPredicate.Evaluate`.
- [x] 6.4 Do not call these from any existing checker, matcher, or session class — no selector/contract wiring in this change (#164's job).

## 7. Test project wiring

- [x] 7.1 Add `<ProjectReference Include="..\..\src\ArchLinterNet.CEL\ArchLinterNet.CEL.csproj" />` to `tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj`.

## 8. Tests

- [x] 8.1 Successful load with `when` present at each of the 7 documented locations (layer selector; context-dependency/allow-only source/forbidden-or-allowed/exclude) — assert `CompiledWhen` is set.
- [x] 8.2 Compile failure cases: syntax error, unknown identifier/member, non-boolean result — assert `Load()` throws before returning a document.
- [x] 8.3 Context-schema-selection test: a `source.when` referencing `target` fails to compile; a `forbidden[*].when` comparing `source`/`target` compiles.
- [x] 8.4 Port-boundary/adapter-binding selector with `when` is rejected as an unknown property (raw-YAML key validation), unchanged from pre-existing behavior for any other unknown key.
- [x] 8.5 Cache reuse: after a successful load, the selector's `CompiledWhen` is non-null and reusable without recompilation (assert same instance across repeated access).
- [x] 8.6 Cache isolation: load the same policy text twice (two `Load()` calls) and assert the two documents' compiled predicates are independent instances (no shared static state).
- [x] 8.7 Literal-only fast path: a policy with no `when` fields leaves every `CompiledWhen` null.
- [x] 8.8 Typed context factory tests: subject-facts record maps to a schema-valid `CelValue`/`CelObjectValue`; dependency-facts record maps to a schema-valid `CelValue`/`CelObjectValue`; a compiled predicate evaluates correctly (true/false) against a factory-built context.
- [x] 8.9 Composed (imported) policy with a `when` field in an allowed location passes `ArchitecturePolicyEffectiveSchemaValidator` (JSON Schema) validation.

## 9. Spec sync and validation

- [x] 9.1 Run `openspec validate --all`.
- [ ] 9.2 Run `rtk make fmt`, inspect the diff.
- [ ] 9.3 Run `rtk make acceptance` and fix any issue-related failures.
- [ ] 9.4 Run `openspec archive core-cel-integration`.
