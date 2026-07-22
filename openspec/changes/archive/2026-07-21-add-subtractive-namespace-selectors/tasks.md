## 1. Model and schema

- [x] 1.1 Add `ArchitectureLayerExclusion` (`Namespace`, `NamespaceSuffix`) and `ArchitectureLayer.Exclude` (`List<ArchitectureLayerExclusion>`, default empty) in `ArchitectureContractModels.cs`.
- [x] 1.2 Add `layerExclusion` `$defs` entry and `exclude` property to the `layer` definition in `schema/dependencies.arch.schema.json` (the fragment schema references the same `$def` by pointer, so no separate edit was needed there).

## 2. Loader validation

- [x] 2.1 Extend `ArchitecturePolicyDocumentLoader.ValidateLayerNodeKeys` to allow `exclude` on layer nodes (monolithic raw-YAML path; composed/imported policies are validated against the same schema `$defs/layer`).
- [x] 2.2 Add a nested key validator (`ValidateLayerExcludeEntries`) for `exclude` entries so an unrecognized key (e.g. `role`) or a missing `namespace` fails loading with an actionable error naming the layer and key.

## 3. Resolution

- [x] 3.1 Update `ArchitectureLayerResolver` namespace matching (`MatchNamespace`, `MatchesNamespace`, `ResolveContainingLayer`) to reject a namespace that matches an include glob but also matches an `exclude` entry, reusing `NamespaceGlobPattern` via shared `MatchLiteral`/`MatchGlob` helpers.
- [x] 3.2 Layers with an empty `Exclude` list skip the exclusion check entirely (`layer.Exclude.Count == 0` short-circuit in `MatchNamespace`).

## 4. Unmatched-exclude diagnostics

- [x] 4.1 `ArchitectureAnalysisSession.FindUnmatchedLayerExclusions` computes, once per policy-consistency pass, whether each layer's `exclude` entry matched any first-party namespace within that layer's included scope (reusing the same assembly/type scanning idiom as `FindLayerOverlaps`).
- [x] 4.2 Unmatched entries are reported as an `unmatched-layer-exclusion` `PolicyConsistencyDiagnostic`, governed by `analysis.policy_consistency` and rendered through the existing human/JSON/SARIF policy-consistency projection — not a new diagnostic channel.

## 5. Layer-description provenance

- [x] 5.1 `ArchitectureLayerResolver.DescribeLayer` appends `(excluding <pattern>, <pattern>...)` when a layer has `Exclude` entries, so every existing consumer of `DescribeLayer` (violation reasons, policy-consistency findings, coverage evidence) surfaces exclusion participation as plain searchable text without a dedicated explain-command feature.

## 6. Tests

- [x] 6.1 Unit tests for `ArchitectureLayerResolver` (`LayerResolverExclusionTests.cs`) covering: included namespace with no exclude; excluded namespace; namespace outside any exclude entry; multiple exclude entries; exclude with `namespace_suffix`; excluded namespace falling back to a separate declared layer; `DescribeLayer` with/without exclude; `FindMatchingExclusion`.
- [x] 6.2 Loader tests (`ContractLoaderSelectorTests.cs`): layer with `exclude` loads successfully; unrecognized key inside `exclude` fails to load with an actionable error; missing `namespace` inside an `exclude` entry fails to load.
- [x] 6.3 Fixture test (`LayerExclusionAcceptanceTests.cs`) — minimal policy: layer `namespace: LayerExclusionAcceptanceFixtures.Product.*` excluding `...Product.Generated`.
- [x] 6.4 Fixture test (`LayerExclusionAcceptanceTests.cs`) — multi-module policy: `ModulesCore` layer over `LayerExclusionAcceptanceFixtures.Modules.*` excluding `*.Infrastructure`/`*.Persistence`; an external-dependency rule scoped to `ModulesCore` proves a vendor persistence SDK reference is blocked in a Domain type and unblocked (out of scope) in Infrastructure/Persistence types.
- [x] 6.5 Policy-consistency tests (`PolicyConsistencyCheckTests.cs`): typo'd exclude entry produces `unmatched-layer-exclusion`; matching entry does not; layer with no exclude entries produces nothing.
- [x] 6.6 `DescribeLayer_WithExclude_IncludesExcludingClause` / `DescribeLayer_WithoutExclude_IsUnchanged` cover the layer-description provenance requirement (superseding the originally planned explain-command-specific test — see Spec synchronization below).
- [x] 6.7 Self-architecture regression: `rtk make lint-architecture` passes against this repo's own policy; full `rtk make acceptance` (lint + CEL/Core/Cli test suites, 2514 tests) passes with no regressions.

## 7. Documentation

- [x] 7.1 Document `exclude:` on layers in `docs/policy-format/layers-and-namespaces.md` and `docs/reference/yaml-schema.md`, including the warning that exclusions narrow legitimate scope and known debt belongs in an exact violation baseline, not silent exclusion.
- [x] 7.2 Representative example mirrors the issue's `Product.Modules.*` / `Infrastructure` / `Persistence` scenario.

## 8. Spec synchronization

- [x] 8.1 Compared implementation against `specs/layer-contracts/spec.md` delta and corrected two mismatches found during sync: unmatched-exclude reporting is a `policy-consistency` finding (not a rule-input-coverage projection), and provenance is carried by the shared `DescribeLayer` text used across violation/policy-consistency/coverage output (not a new `explain`-command-specific mechanism, since the `explain` verb resolves dependency-graph paths between two nodes and has no concept of "why doesn't this type belong to layer X"). `proposal.md` and `design.md` were corrected to match.
- [x] 8.2 `openspec validate --all` run after archiving.
