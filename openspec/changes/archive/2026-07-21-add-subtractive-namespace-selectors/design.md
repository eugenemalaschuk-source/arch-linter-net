## Context

Layers (`ArchitectureLayer` in `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`) are matched by `ArchitectureLayerResolver` (`src/ArchLinterNet.Core/Resolution/ArchitectureLayerResolver.cs`) using `NamespaceGlobPattern` (`src/ArchLinterNet.Core/Resolution/NamespaceGlobPattern.cs`), which restricts globs to whole-segment `*` wildcards and rejects everything else (`**`, `?`, partial-segment wildcards). Every other contract family (dependency, allow-only, external-dependency, protected, cycle, acyclic-sibling) references layers by name and consumes the resolver's output — none of them re-implement namespace matching. Contextual selectors (`ArchitectureContextSelector`) and coverage contracts (`ArchitectureCoverageContract.Exclude`) already have their own working `exclude` shape and are unaffected by this change.

`ArchitecturePolicyDocumentLoader` deserializes YAML with YamlDotNet's `IgnoreUnmatchedProperties()` for backward compatibility, but separately walks the raw `YamlStream` to reject unknown keys on monolithic policies (`ValidateLayerNodeKeys`) and validates composed/imported policies against `schema/dependencies.arch.schema.json` (which sets `additionalProperties: false` on the `layer` definition). Both paths must be updated together or a new `exclude:` key on a layer would either be silently swallowed (monolithic, if the allow-list were forgotten) or rejected (composed, if the schema were forgotten).

## Goals / Non-Goals

**Goals:**
- A layer's effective namespace scope = matched by `namespace`/`namespace_suffix` glob AND not matched by any `exclude` glob.
- Zero behavior change for the ~100% of existing policies that declare no `exclude` on any layer.
- Exclusion propagates to every contract family that references layers by name, without changes to those families' own evaluation code.
- A typo in an `exclude` pattern that matches nothing is reportable, not silently inert.
- The shared layer-description text used across diagnostics names which excludes participate in a layer's scope.

**Non-Goals:**
- A generic `ISelector`/`include`+`exclude` abstraction shared literally (same interface/type) across layers, contextual selectors, type-placement matchers, layout-convention matchers, and coverage. Those families already have (contextual, coverage) or don't need (type-placement, layout, templates) subtraction; forcing one polymorphic type across all of them now would touch far more surface area than this issue's representative scenarios require and risks half-finished wiring. This change keeps the *algebra* (`include - union(excludes)`) consistent and reuses the same glob engine, without a shared runtime type.
- New glob syntax. `exclude` entries use exactly the same `NamespaceGlobPattern` grammar layers already use for inclusion (whole-segment `*` only).
- Changes to contextual-selector or coverage-contract `exclude` behavior — both already work today and are out of scope.
- CEL-based (`when`) exclusion. Layer `exclude` entries are structural (`namespace`/`namespace_suffix`) glob matches only, matching the issue's representative scenario; CEL-driven exclusion for layers is not requested by the acceptance scenarios and is left for a future change if needed.

## Decisions

**Exclude entries reuse `NamespaceGlobPattern`, not a new matcher.** `ArchitectureLayer.Exclude` is `List<ArchitectureLayerExclusion>` where `ArchitectureLayerExclusion` has `Namespace`/`NamespaceSuffix` — the same two fields `ArchitectureLayer` itself uses for inclusion. `ArchitectureLayerResolver` builds the excluded-namespace check by constructing a throwaway match against each exclusion entry with the existing `NamespaceGlobPattern.Match`, mirroring how coverage roots already piggyback on layer matching. Alternative considered: give each exclusion entry the full `ArchitectureLayerSelector` shape (role/metadata/when) — rejected because the issue's representative scenario and acceptance scenarios are purely namespace-structural, and reusing the heavier selector shape would imply CEL/role support that nothing here evaluates, misleading policy authors.

**Exclusion is evaluated per-namespace at match time, not pre-expanded.** `ArchitectureLayerResolver.MatchNamespace`/`MatchesNamespace`/`ResolveContainingLayer` first find the best include match (same logic as today), then check exclusion entries only for namespaces that already matched an include glob — cheap because exclusion checks are skipped entirely for layers with an empty `Exclude` list (the common case), preserving current performance for the ~100% of layers with no exclusion.

**Unmatched-exclude detection reuses the existing policy-consistency pass, not a new diagnostic family.** An `exclude` entry is "unmatched" if, across the analyzed namespace inventory, no first-party namespace both matches the layer's include glob and matches that exclude glob. `ArchitectureAnalysisSession.CheckPolicyConsistency` already scans every target-assembly type once per session for the layer-overlap check (`FindLayerOverlaps`); `FindUnmatchedLayerExclusions` reuses the same scanning idiom to mark which exclude entries matched, then reports the rest as an `unmatched-layer-exclusion` `PolicyConsistencyDiagnostic`, governed by the existing `analysis.policy_consistency` severity setting (human/JSON/SARIF) — avoids inventing a parallel diagnostic channel or severity knob alongside the unmatched-ignored-violations precedent.

**Schema and loader validation change in lockstep.** `schema/dependencies.arch.schema.json`'s `layer` definition gains `"exclude": {"type": "array", "items": {"$ref": "#/$defs/layerExclusion"}}` (new `$defs/layerExclusion` with `namespace`/`namespace_suffix`, `additionalProperties: false`, at least one of the two required — mirrors the existing `layer` `anyOf` shape). `ArchitecturePolicyDocumentLoader.ValidateLayerNodeKeys` adds `exclude` to its allow-list, and a new nested key check validates each exclusion entry's keys the same way existing nested validators do. The fragment schema gets the same addition so imported fragments can declare layer exclusions.

**Provenance extends the existing `DescribeLayer` text, not a new structured field.** `ArchitectureLayerResolver.DescribeLayer` already renders a layer's namespace/suffix/selector shape as plain text, and that text already flows into violation reasons (`ArchitectureNamespaceViolationFinder`), policy-consistency findings (`FindLayerOverlaps`/`FindUnreachableContracts`), and coverage evidence (`ArchitectureAnalysisSession.SemanticCoverage.cs`) — i.e. every existing human/JSON/SARIF surface that names a layer. `DescribeLayer` appends `(excluding <pattern>, <pattern>...)` when `Exclude` is non-empty, so every one of those call sites gains exclusion visibility for free, without a new typed model or a dedicated explain-command feature.

## Risks / Trade-offs

- **[Risk]** A layer `exclude` entry could unintentionally shadow a *different* layer that already owns the excluded namespace (e.g. excluding `Product.Modules.*.Infrastructure` from a broad layer while a separate `Infrastructure` layer also declares `Product.Modules.*.Infrastructure`) → **Mitigation**: excluded namespaces simply fall out of the broad layer's scope and are re-evaluated against all other declared layers exactly as an unmatched namespace is today (existing `ResolveContainingLayer` semantics: first matching layer wins); no new ambiguity is introduced, and namespace-coverage contracts already flag namespaces matched by no layer.
- **[Risk]** Silent no-op exclusions (typo'd glob) would look like the feature works when it doesn't → **Mitigation**: unmatched-exclude reporting (Decision 3) makes this visible via existing coverage/diagnostic channels; documented as a required check in the acceptance scenario for `.Persistnce`-style typos.
- **[Trade-off]** Not building one polymorphic selector type across every family now means a later change extending exclusion to type-placement or layout-convention matchers will duplicate some of this design rather than inherit an existing abstraction. Accepted because the issue's own acceptance criteria are satisfied by the layers-only surface, and premature abstraction across families with different scoping shapes (role/metadata vs. namespace vs. file-fact) was explicitly flagged as a non-goal.

## Migration Plan

No migration required — existing layers deserialize `Exclude` as an empty list by default (`= new()`), so behavior for 0.5.0 policies is unchanged. Documentation update ships in the same change describing the new optional field. No rollback concerns beyond a normal revert since the schema addition is additive (`exclude` optional, no removed/renamed fields).

## Open Questions

None outstanding — acceptance scenarios in issue #356 are fully addressed by layer-level `exclude` given dependency/allow-only/external-dependency contracts resolve their scope through layers.
