## Why

Layer selection today is purely additive: a layer's `namespace`/`namespace_suffix` glob matches everything under a root with no way to carve out an owned sub-namespace. Modular solutions that want "all `Product.Modules.*` code except each module's own `Infrastructure`/`Persistence`" are forced into manual per-module enumeration, overbroad rules, or coverage-only gaps. Contextual selectors (`context_dependencies`, `context_allow_only`, `port_boundaries`) and coverage contracts already support an `exclude` list of their own selector shape — layers are the one structural selector family without any subtraction, and layers are also the highest-leverage fix: dependency, allow-only, external-dependency, protected, cycle, and acyclic-sibling contracts all reference layers by name, so giving layers an `exclude` propagates subtraction to every one of them without touching each family's own evaluation code.

## What Changes

- Add an `exclude:` sibling list to `layers.<name>` accepting the same `namespace`/`namespace_suffix` glob shape layers already use for inclusion. A namespace is in the layer's scope only if it matches an include glob and matches none of the exclude globs (`result = include - union(excludes)`).
- Layers with no `exclude:` key are byte-for-byte unchanged (empty exclude list, no-op subtraction) — no migration for existing 0.5.0 policies.
- `ArchitectureLayerResolver` namespace matching (`MatchNamespace`, `MatchesNamespace`, `ResolveContainingLayer`) applies exclusion after inclusion match, reusing the existing `NamespaceGlobPattern` glob engine (no new glob grammar).
- JSON schema (`schema/dependencies.arch.schema.json`, `schema/dependencies.arch.fragment.schema.json`) accepts `exclude` under the `layer` definition.
- `ArchitecturePolicyDocumentLoader` raw-YAML key validators accept `exclude` on layer nodes for both monolithic and composed/imported policies, so a typo'd key still fails loudly.
- The shared layer-description text used across violation messages, policy-consistency findings, and coverage evidence (`ArchitectureLayerResolver.DescribeLayer`) names a layer's `exclude` entries alongside its include pattern, so exclusion participation is visible as searchable text in human, JSON, and SARIF output without a separate explain-specific mechanism.
- An exclude glob that matches nothing under its layer's include scope is reportable as a stale/unmatched exclusion, mirroring the existing unmatched-ignore diagnostic precedent, so a typo like `.Persistnce` is visible instead of silently doing nothing.
- Documentation for the policy YAML format describes `exclude:` on layers and warns that exclusions narrow legitimate scope — known debt belongs in exact violation baselines, not silent exclusion.
- **Explicitly out of scope for this change** (documented, not silently dropped): contextual selectors and coverage contracts already have their own working `exclude` mechanisms and are unchanged here; type-placement matchers, layout-convention matchers, package/assembly contracts, and layer templates/containers do not gain a new `exclude` field in this change and remain candidates for follow-up issues.

## Capabilities

### New Capabilities
(none — this extends an existing capability's requirements rather than introducing a new one)

### Modified Capabilities
- `layer-contracts`: layer namespace/namespace_suffix selection gains an `exclude` list that subtracts matching namespaces from an otherwise-matched layer, with unmatched-exclude policy-consistency reporting and shared layer-description provenance.

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` — `ArchitectureLayer` gains `Exclude`.
- `src/ArchLinterNet.Core/Resolution/ArchitectureLayerResolver.cs` — exclusion-aware namespace matching.
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs` — raw-YAML key allow-list for layer nodes.
- `src/ArchLinterNet.Core/Contracts/PolicyImports/ArchitecturePolicyEffectiveSchemaValidator.cs` — composed-policy schema path (uses the JSON schema, no code change expected beyond the schema file).
- `src/ArchLinterNet.Core/Resolution/ArchitectureLayerResolver.cs` (`DescribeLayer`) — shared layer-description provenance for excluded namespaces, flowing into violation reasons, policy-consistency findings, and coverage evidence.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.PolicyConsistency.cs` — `unmatched-layer-exclusion` policy-consistency check.
- `schema/dependencies.arch.schema.json`, `schema/dependencies.arch.fragment.schema.json` — schema additions.
- Existing dependency/allow-only/external-dependency/protected/cycle/acyclic-sibling contracts are unaffected in code (they consume layer resolution results) but gain exclusion semantics transitively through the layer they reference.
- Documentation under `docs/` describing the policy YAML format.
- No changes required in `ArchLinterNet.Cli` or `ArchLinterNet.Testing` beyond passthrough of existing diagnostic projections.
