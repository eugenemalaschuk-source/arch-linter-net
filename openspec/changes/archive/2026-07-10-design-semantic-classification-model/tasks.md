## 1. Vocabulary and model design

- [x] 1.1 Define the ten classification terms (`role`, `metadata`, `source`, `evidence`, `confidence`/`precedence`, `conflict`, `override`, `exclusion`, `stale selector`, `uncovered semantic fact`) with non-overlapping meanings.
- [x] 1.2 Decide classification is a top-level configuration section, not a contract family, and document why.
- [x] 1.3 Fix the six-source precedence order and the `classification.precedence` narrowing-only rule.
- [x] 1.4 Define same-tier conflict resolution (first-declared-wins) and the `conflict` fact it still records.
- [x] 1.5 Define that metadata does not merge across sources once a role is assigned.

## 2. YAML shape design

- [x] 2.1 Design `classification.attributes`/`assembly_attributes` (full-type-name mapping, no binary annotation dependency).
- [x] 2.2 Design `classification.inheritance`/`namespace`/`path` convention mappings reusing existing glob syntax.
- [x] 2.3 Design `classification.overrides` with the narrow/broad `reason` threshold.
- [x] 2.4 Design `classification.exclusions` with mandatory `reason`.
- [x] 2.5 Design the metadata extraction syntax (`constructor[N]`, `property:Name`, `const:Full.Type.NAME`, literal fallback).
- [x] 2.6 Design `layers.<name>.selector` (role + exact-match metadata) as additive to the existing `layer` shape.
- [x] 2.7 Write worked YAML examples: Sales/Inventory/SharedKernel modular monolith and Unity/client namespace-convention-only policy.

## 3. Interaction with existing model

- [x] 3.1 Confirm `layers.<name>.namespace` stays required and `selector` is purely additive alongside it — a selector-only layer was found during review to crash `ArchitectureLayerResolver.IsProjectType` at real execution time (unconditional `GlobPattern` access on an empty `Namespace`), so `namespace` is NOT relaxed to an alternative.
- [x] 3.2 Distinguish this design from existing point-in-time constraint families (`AttributeUsageContractFamily`, `InheritanceContractFamily`, `InterfaceImplementationContractFamily`, `TypePlacementContractFamily`) and document why classification is a separate, reusable fact layer rather than a duplicate of them.
- [x] 3.3 Explain the coverage-integration point: a future `scope: semantic_role` variant of the architecture-coverage-model contract, reusing its existing vocabulary, owned by #114.

## 4. Runtime-behavior boundary

- [x] 4.1 Decide and document that this change adds no C# binding and no load-time reject guard for `classification`/`selector`, deviating from the #96 coverage-model precedent (Decision 10) because #107's acceptance criteria explicitly forbid introducing runtime behavior.
- [x] 4.2 Record this as a documented, time-boxed risk and recommend #108/#109 close the gap as their first task.

## 5. Schema update

- [x] 5.1 Add a `classification` `$def` and its sub-shape `$defs` (`attributeClassificationEntry`, `assemblyAttributeClassificationEntry`, `inheritanceClassificationEntry`, `namespaceClassificationEntry`, `pathClassificationEntry`, `classificationOverride`, `classificationExclusion`) to `schema/dependencies.arch.schema.json`, additive only.
- [x] 5.2 Add a `selector` `$def` and reference it from the existing `layer` `$def` as an additive optional field; `layer`'s `required` keeps `namespace` mandatory.
- [x] 5.3 Add `classification` to the schema root's `properties` (root `additionalProperties` stays `false`, so this is required for the section to validate).
- [x] 5.4 Confirm the schema change is additive only and does not alter validation of any existing field for policies that declare no `classification` section and no `selector`.

## 6. Schema regression tests

- [x] 6.1 Add tests to `tests/ArchLinterNet.Core.Tests/ArchitectureContractSchemaTests.cs` asserting the new `$defs`/properties exist, following the existing lightweight schema-assertion pattern.
- [x] 6.2 Add a test asserting `layer`'s `required` rejects a `selector`-only layer definition (no `namespace`), accepts `namespace`+`selector` together, and still accepts a `namespace`-only layer definition unchanged.
- [x] 6.3 Add a test asserting `classificationExclusion` requires `reason` and `classificationOverride` requires `reason` only for namespace-scoped entries.
- [x] 6.4 Add an execution-level regression test (not just schema/load-time) that actually exercises namespace classification (`ArchitectureLayerResolver.IsProjectType`/full validation run) with a `namespace`+`selector` layer, since schema validation and `ArchitecturePolicyDocumentLoader.Load` alone did not catch the selector-only crash found during review.

## 7. Sample policy validation

- [x] 7.1 Extend `samples/policies/modular-monolith.yml` with a `classification` section demonstrating the Sales/Inventory/SharedKernel shape; confirmed it validates against the updated schema.
- [x] 7.2 Extend `samples/policies/unity-asmdef-boundaries.yml` with a `classification` section demonstrating the Unity/client namespace-convention-only shape; confirmed it validates against the updated schema.

## 8. Spec and design sync

- [x] 8.1 Write `design.md` covering context, goals/non-goals, decisions, YAML shape, worked examples, risks, and open questions.
- [x] 8.2 Write the `semantic-classification-model` spec capturing the reviewed shape as ADDED requirements.
- [x] 8.3 State explicit non-goals: no engine, no extraction, no selector matching, no contextual contracts, no annotation package, no runtime behavior change.
- [x] 8.4 Run `openspec validate --all` and confirm the change validates cleanly before archiving.
