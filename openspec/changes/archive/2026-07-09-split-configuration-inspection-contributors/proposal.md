## Why

`ArchitectureAnalysisSession.CheckConfiguration(bool strict)` (`src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.cs:237-701`) hand-enumerates every contract family's YAML-derived collections, twice (once for strict, once for audit), to decide which layer names, external dependency groups, package groups, and project paths each family references. Every new contract family that references any config-owned concept requires editing this central method in four places (strict block, audit block, and often a matching case in the `GetReferencedLayerNames` switch in `ArchitectureAnalysisSession.PolicyConsistency.cs`). This makes the session a switchboard for unrelated families and is exactly the kind of god-class growth `ArchitectureContractFamilyRegistry`/`ArchitectureContractChecker` (#227) already fixed for contract *execution* — the same pattern was never applied to configuration *inspection*.

## What Changes

- Introduce an `ArchitectureConfigurationContributor` delegate (`Execution.Abstractions`), shaped like the existing `ArchitectureContractChecker` delegate, that lets a family report its own configuration references given a session, a shared collector, and one contract instance.
- Introduce an `ArchitectureConfigurationReferenceCollector` class that replaces `CheckConfiguration`'s inline mutable locals and closures (`layerReferencingContractIds`, `referencedExternalGroups`, `referencedPackageGroups`, `packageContractSources`, `projectMetadataContractProjects`) with named sink methods.
- Add a nullable `ConfigurationContributor` property to `ArchitectureContractFamilyDescriptor`, populated for the 16 families that already report configuration references today (`dependency`, `layer`, `allow_only`, `cycle`, `method_body`, `independence`, `protected`, `external`, `external_allow_only`, `package_dependency`, `package_allow_only`, `project_metadata`, `type_placement`, `attribute_usage`, `inheritance`, `interface_implementation`). Left `null` for every other family (`layer_template`, `assembly_dependency`, `assembly_allow_only`, `assembly_independence`, `public_api_surface`, `asmdef`, `acyclic_sibling`, `coverage`, `composition`) — unchanged from today.
- Rewrite `CheckConfiguration(bool strict)` to loop `ArchitectureContractFamilyRegistry.All` once, invoking each descriptor's contributor (if any) against a single collector, instead of two hand-written per-family blocks.
- Promote the three private `GetXxxReferencedLayerNames` extraction helpers on the session to `internal static` so the new per-family contributors reuse the exact same extraction logic the `PolicyConsistency.cs` switch already uses, instead of duplicating it.
- **Known, explicitly preserved gap**: `composition` contracts' `AllowedOnlyInLayers` is already handled by the `GetReferencedLayerNames` switch (used elsewhere for dangling-layer deferral) but was never fed into `CheckConfiguration`, so a composition contract with a typo'd layer name produces no configuration diagnostic today. This change does **not** fix that gap — doing so would introduce a new violation class for previously-silent policies, which conflicts with this change's "current configuration error behavior remains compatible" goal. Left as a documented follow-up.
- No change to contract execution/checker internals from #227, and no change to `ArchitectureAnalysisSession`'s public API surface beyond internal restructuring.

## Capabilities

### New Capabilities
- `configuration-contributor-registry`: describes the `ArchitectureConfigurationContributor` delegate, the `ArchitectureConfigurationReferenceCollector`, and the rule that each contract family's configuration-reference reporting lives on its own descriptor rather than in a central switchboard method.

### Modified Capabilities
- `contract-family-registry`: `ArchitectureContractFamilyDescriptor` gains a `ConfigurationContributor` property (nullable, defaults to `null`), following the same "inert unless populated" pattern already documented for `OwnedContractTypes`/`AdditionalValidation`, except this one is live and invoked by `CheckConfiguration`.

## Impact

- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.cs` — `CheckConfiguration(bool strict)` rewritten to iterate the registry; missing-assembly/discovery-diagnostic handling and the post-collection validation logic (layer resolution, rule_input-coverage deferral, external/package group checks, package/project metadata cross-checks) are unchanged.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.PolicyConsistency.cs` — three helper methods change from `private static` to `internal static`; no behavior change.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractFamilyDescriptor.cs` — one new nullable property.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractFamilyRegistry.cs` — 16 descriptors gain a `ConfigurationContributor`.
- New files: `ArchitectureConfigurationContributor.cs` (delegate) and `ArchitectureConfigurationReferenceCollector.cs` (collector class) under `Execution`/`Execution.Abstractions`.
- Test projects: extend `PackageDependencyConfigurationTests.cs`, `ExternalAllowOnlyContractTests.cs`, and add direct unknown-layer coverage for at least one of `type_placement`/`attribute_usage`/`interface_implementation` (today only indirectly exercised through the shared helper). No production behavior is expected to change, so all existing tests in `ConfigurationCheckTests.cs`, `ConfigurationCheckByModeTests.cs`, `ProjectMetadataConfigurationTests.cs`, and `RuleInputCoverageValidationTests.cs` must continue to pass unmodified.
- No impact on `Contracts`, `Discovery`, `Scanning`, or `Reporting` modules.
