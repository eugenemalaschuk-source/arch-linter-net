## 1. Core matching

- [x] 1.1 Add `ArchitectureLayerResolver.MatchesNamespacePattern(namespaceName, pattern)` using `NamespaceGlobPattern.Parse(pattern).Match(namespaceName).Matched`.
- [x] 1.2 Route `IsAllowedLocation` (`ArchitectureAnalysisSession.TypePlacement.cs`) through `MatchesNamespacePattern` instead of `ArchitectureLayerResolver.MatchesPrefix` for its `allowedNamespacePrefixes` parameter.

## 2. Policy-load validation

- [x] 2.1 Add `PolicyDocumentValidatorSupport.ValidateNamespacePatterns(contractLabel, fieldName, entries)` that parses each entry with `NamespaceGlobPattern.Parse` and rethrows `InvalidNamespacePatternException` as an actionable `InvalidOperationException`.
- [x] 2.2 Call it from `CompositionValidator` for `allowed_only_in_namespaces`.
- [x] 2.3 Call it from `AttributeUsageValidator` for `allowed_only_in_namespaces` and `forbidden_in_namespaces`.
- [x] 2.4 Call it from `InterfaceImplementationValidator` for `allowed_only_in_namespaces` and `forbidden_in_namespaces`.
- [x] 2.5 Call it from `TypePlacementValidator` for `must_reside_in_namespaces`.

## 3. Tests

- [x] 3.1 `LayerResolverGlobTests`: `MatchesNamespacePattern` literal and glob matching, including boundary cases (zero-segment wildcard fails, exact match, descendant match, invalid syntax throws).
- [x] 3.2 Composition contract test: `allowed_only_in_namespaces` with a glob pattern (`CompositionContractTestFixtures.Modules.*.Composition`) allows an in-boundary call and flags an out-of-boundary one.
- [x] 3.3 Attribute-usage contract test: glob pattern on `allowed_only_in_namespaces` and `forbidden_in_namespaces`.
- [x] 3.4 Interface-implementation contract test: glob pattern on `allowed_only_in_namespaces` and `forbidden_in_namespaces`.
- [x] 3.5 Type-placement contract test: glob pattern on `must_reside_in_namespaces`.
- [x] 3.6 Invalid-pattern policy-load tests for each of the four validators (bare `*`, partial-segment `*`, leading `*`, empty segment) asserting an actionable `InvalidOperationException`.
- [x] 3.7 Literal-pattern regression: covered by all pre-existing literal-pattern family tests continuing to pass plus dedicated `MatchesNamespacePattern` literal tests.

## 4. Docs

- [x] 4.1 Update `docs/contracts/composition.md`, `attribute-usage.md`, `interface-implementation.md`, and `type-placement.md` to document glob support for the namespace-allowance fields.
- [x] 4.2 Update `docs/ai/policy-authoring-guide.md` and `docs/policy-format/layers-and-namespaces.md` with the same guidance.

## 5. Spec sync and archive

- [ ] 5.1 Compare implementation against `specs/namespace-glob-patterns/spec.md` delta; adjust wording if implementation diverged.
- [ ] 5.2 Run `openspec validate --all`.
- [ ] 5.3 Run `openspec archive explicit-namespace-allowance-glob-grammar`.

## 6. Validation

- [ ] 6.1 `make fmt`.
- [ ] 6.2 `make acceptance`.
