## Why

ArchLinterNet's existing dependency and allow-only contracts express boundaries only between declared `layers.<name>` (namespace or selector-backed). Business-context boundaries such as "no Domain type in one bounded context may depend on a Domain type in another" require enumerating every concrete layer pair, which does not scale as contexts grow and duplicates information the semantic role/metadata model (#107, #110, #111) already discovers. Issue #112 (parent story #106) asks for a new contract family that compares discovered role/metadata directly between a source type and its dependency targets, without an intermediate `layers.<name>` declaration.

## What Changes

- Add `strict_context_dependencies`/`audit_context_dependencies` contract families: source/forbidden/exclude are inline selector objects (`role` + `metadata`) matched directly against discovered role/metadata, not layer names.
- Add `strict_context_allow_only`/`audit_context_allow_only` contract families with the same selector shape for source/allowed/exclude, restricting a source context's dependencies to same-context (or explicitly allowed) targets.
- Add a contextual-selector metadata value syntax with four deterministic operators: exact literal match, `any` (`"*"`), `in` (list of literals), and `not-equal-to-source` (`"!{source.metadata.<key>}"`, a cross-reference to the matched source type's own resolved metadata value). This is a new, small value grammar — no regex.
- Add diagnostics for contextual violations carrying source role/metadata, target role/metadata, and which selector (source/forbidden/allowed/exclude) produced the match, distinguishable from existing namespace/layer dependency diagnostics in both JSON and human output.
- Reuse the existing inline `ignored_violations` mechanism for the new families. External baseline support is added if it fits the existing per-group registration cleanly; otherwise it is explicitly deferred with a documented follow-up issue, per #112's own acceptance-criteria allowance.
- Update `schema/dependencies.arch.schema.json`, docs (`docs/contracts/`, `docs/policy-format/`, `docs/ai/capabilities.md`, `archlinternet.capabilities.json`, `mkdocs.yml`), and example policies to cover the new families.
- A contextual contract's direct role/metadata reference is registered as an equally valid consumption path as a `layers.<name>.selector` match, per the existing `semantic-classification-model` capability's requirement that coverage-participating consumption not be hard-coded to selectors alone — this only requires exposing/marking the consumed role/metadata pairs; it does not implement the future coverage `scope: semantic_role` variant (#114) itself.

## Capabilities

### New Capabilities
- `contextual-dependency-contracts`: strict/audit contract family comparing source and forbidden role/metadata selectors directly, including the metadata operator vocabulary, diagnostics, and ignored-violation support.
- `contextual-allow-only-contracts`: strict/audit contract family restricting a source selector's dependencies to allowed role/metadata selectors, mirroring the dependency family's selector/operator/diagnostic conventions.

### Modified Capabilities
- `semantic-classification-model`: register the new contextual contracts' direct role/metadata references as coverage-participating consumption, resolving the capability's existing forward-reference to #112 (no other requirement changes).

## Impact

- New model/POCO files under `src/ArchLinterNet.Core/Contracts/Families/` and additions to `ArchitectureContractGroups`.
- New descriptor entries in `src/ArchLinterNet.Core/Execution/ArchitectureContractFamilyRegistry.cs`.
- New checker logic in/near `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.Checking.cs`, and a new contextual-selector matcher distinct from `ArchitectureLayerTypeMatcher`.
- New diagnostic payload/diagnostic types alongside `Model/DependencyPayload.cs`/`Model/DependencyDiagnostic.cs`, wired through `ArchitectureDiagnosticMapper` and `ArchitectureDiagnosticFormatter`.
- Possible addition to `src/ArchLinterNet.Core/Contracts/ArchitectureBaselineModels.cs` (or a documented deferral).
- `schema/dependencies.arch.schema.json`, docs, `archlinternet.capabilities.json`, `mkdocs.yml`, `samples/policies/`.
- New tests in `tests/ArchLinterNet.Core.Tests/` (Sales/Inventory/SharedKernel-style fixtures) and `tests/ArchLinterNet.Cli.Tests/`.
