## 1. Model layer

- [x] 1.1 Add `Inheritance` and `Namespace` members to `ArchitectureClassificationSource` in `src/ArchLinterNet.Core/Model/ArchitectureClassificationFacts.cs`.

## 2. Contracts layer

- [x] 2.1 Add `ArchitectureInheritanceClassificationMapping` (`base_type`/`role`/`metadata`) to `src/ArchLinterNet.Core/Contracts/ArchitectureClassificationModels.cs`.
- [x] 2.2 Add `ArchitectureNamespaceClassificationMapping` (`namespace`/`namespace_suffix`/`role`/`metadata`) to the same file.
- [x] 2.3 Bind `Inheritance`/`Namespace` list properties on `ArchitectureClassificationConfiguration` with the matching `YamlMember` aliases.
- [x] 2.4 Update the class-level comment describing which `classification.*` sections are bound vs. inert.

## 3. Metadata extraction (no attribute instance)

- [x] 3.1 Factor the existing `const:`/literal-fallback logic in `ArchitectureAttributeMetadataExtraction.Extract` into a shared private helper.
- [x] 3.2 Add `ExtractWithoutAttributeInstance(object rawYamlValue, Func<string, Type?> resolveType)` reusing that helper, rejecting `constructor[`/`property:`-prefixed values with a clear extraction-failure reason (defense in depth).

## 4. Extractor: precedence generalization

- [x] 4.1 Replace `ArchitectureAttributeRoleExtractor.Combine()`'s hardcoded 2-source logic with an N-tier walk over the fixed order `[TypeAttribute, AssemblyAttribute, Inheritance, Namespace]`, filtered by `_configuration.Precedence`/`IsSourceEnabled`.
- [x] 4.2 Ensure conflicts/metadata failures from every enabled tier are unioned into the result regardless of which tier's role wins.

## 5. Extractor: inheritance resolution

- [x] 5.1 Add `ResolveInheritanceCandidate(Type type)`: iterate `_configuration.Inheritance` in declaration order, resolve `base_type` via the existing `ResolveTypeByFullName` lookup, match with `baseType.IsAssignableFrom(type) && baseType != type`.
- [x] 5.2 Record same-tier conflicts (multiple matching entries, differing role/metadata) using the existing conflict-recording pattern from `ResolveCandidate`.
- [x] 5.3 Guard reflection calls defensively (`TypeLoadException`/`FileNotFoundException`), mirroring `SafeGetCustomAttributesData`.
- [x] 5.4 Set `Evidence` to the matched entry's `base_type` full name; `Subject` to the type's own full name.
- [x] 5.5 Route inheritance metadata extraction through `ExtractWithoutAttributeInstance`.

## 6. Extractor: namespace resolution

- [x] 6.1 Add a constructor parameter to `ArchitectureAttributeRoleExtractor` for the injected namespace-matching delegate (plain-tuple return type, no `Resolution`-typed signature).
- [x] 6.2 Add `ResolveNamespaceCandidate(Type type)`: iterate `_configuration.Namespace` in declaration order, call the injected delegate against the type's namespace.
- [x] 6.3 Record same-tier conflicts using the existing pattern.
- [x] 6.4 Set `Evidence` to the matched entry's namespace/namespace_suffix pattern string; `Subject` to the type's own full name.
- [x] 6.5 Route namespace metadata extraction through `ExtractWithoutAttributeInstance`.

## 7. Execution wiring

- [x] 7.1 In `ArchitectureRoleIndex`, construct the namespace-matching delegate by wrapping each `ArchitectureNamespaceClassificationMapping` in a throwaway `ArchitectureLayer` and calling `ArchitectureLayerResolver.MatchNamespace`, projecting the result to a plain tuple.
- [x] 7.2 Pass the delegate into the extractor's constructor.
- [x] 7.3 Extend `BuildData()`'s empty-config early-return guard to also check `Inheritance.Count`/`Namespace.Count`.

## 8. classification.path deferred diagnostic

- [x] 8.1 Add raw-YAML `classification.path` non-empty-presence detection in `ArchitecturePolicyDocumentLoader.cs` (parse the raw node tree, not the unbound C# model).
- [x] 8.2 Produce a non-fatal `ArchitectureDiagnosticKind.Configuration` diagnostic when detected, fired once per policy load, independent of scanned types.
- [x] 8.3 Surface the diagnostic through the existing classification-findings channel (human output, JSON, CI artifact) alongside conflicts/metadata failures.
- [x] 8.4 Confirm the diagnostic does not affect `validate`'s exit code.

## 9. Tests

- [x] 9.1 Add inheritance fixture types to the `AttributeRoleExtractionTestFixtures` project: a base class with a direct derived type, a transitively-derived type, and an interface-implementing type.
- [x] 9.2 Add namespace fixture types exercising both `namespace:` glob-prefix and `namespace_suffix:` matching.
- [x] 9.3 Add a precedence test: one type matched by both an inheritance entry and a namespace entry with different roles, asserting inheritance wins.
- [x] 9.4 Add a same-tier conflict test for two inheritance entries and for two namespace entries matching one type with different roles.
- [x] 9.5 Add a metadata test using `const:` on an inheritance entry, and a hand-constructed-config test proving `constructor[`/`property:` values are rejected defensively for inheritance/namespace sources.
- [x] 9.6 Add an unresolved-`base_type` test asserting silent no-match with no diagnostic.
- [x] 9.7 Add a `classification.path`-declared fixture asserting the new deferred diagnostic appears and the run still succeeds with no exception and no role assignment from `path`.
- [x] 9.8 Extend `ArchitectureRoleIndexTests.cs` and `ArchitectureAnalysisSessionClassificationTests.cs` with the above scenarios at the appropriate level (extractor-level vs. session-level).

## 10. Spec sync

- [x] 10.1 Confirm the delta spec at `openspec/changes/add-namespace-inheritance-classification/specs/semantic-classification-model/spec.md` matches the implemented behavior exactly (amend if implementation revealed any deviation).

## 11. Docs

- [x] 11.1 Update `docs/policy-format/semantic-classification.md`: move `inheritance`/`namespace` from "reserved" to "implemented," document the new `classification.path` deferred diagnostic.
- [x] 11.2 Add worked convention examples to the same doc: modular monolith, clean architecture, ASP.NET-like, EF-like, Unity/client-like, legacy gradual adoption.
- [x] 11.3 Update `docs/policy-format/supported-capabilities.md` to reflect the new implemented sources.

## 12. Validation and archive

- [x] 12.1 Run `make fmt`.
- [x] 12.2 Run `make acceptance`; fix any failures.
- [x] 12.3 Run `openspec validate --all`.
- [x] 12.4 Run `openspec archive add-namespace-inheritance-classification`.
- [x] 12.5 Open PR closing issue #113's namespace/inheritance scope, with `classification.path` explicitly noted as deferred pending #171 in the Scope/non-goals section.
