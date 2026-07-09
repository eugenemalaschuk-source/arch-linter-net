## Context

`ArchitectureAnalysisSession.CheckConfiguration(bool strict)` (`ArchitectureAnalysisSession.cs:237-701`) is the runtime pass that validates configuration-owned references — layer names, external dependency groups, package groups, project paths — declared by contracts. It duplicates a nearly-identical loop for `strict` and `audit` modes (lines 304-511), each hand-enumerating 16 of the 25 contract families' `Document.Contracts.*` collections directly. Three families (`type_placement`, `attribute_usage`, `interface_implementation`) route through private static extraction helpers on `ArchitectureAnalysisSession.PolicyConsistency.cs` (`GetTypePlacementReferencedLayerNames`, `GetAttributeUsageReferencedLayerNames`, `GetInterfaceImplementationReferencedLayerNames`) that also back a second, independent `GetReferencedLayerNames` switch used by rule_input-coverage deferral and policy-consistency checks.

Issue #211/#227 already solved the equivalent problem for contract *execution*: `ArchitectureContractFamilyRegistry.All` is an ordered list of `ArchitectureContractFamilyDescriptor` records, each carrying a `Checker` delegate (`ArchitectureContractChecker`) that `ArchitectureContractHandlerRegistry` invokes per-family instead of a hand-written per-family `switch`/dispatch. This design applies the same descriptor-driven pattern to configuration inspection.

## Goals / Non-Goals

**Goals:**
- Let each contract family declare, on its own descriptor, what configuration references it contributes — so adding a new family's config-reference reporting means adding one descriptor field, not editing `CheckConfiguration`.
- Preserve `CheckConfiguration`'s observable behavior exactly: same violations, same messages, same `ArchitectureViolation` fields, same rule_input-coverage deferral interaction, for every currently-covered family.
- Collapse the strict/audit duplication in `CheckConfiguration` into one loop over the registry, reusing the `StrictContracts`/`AuditContracts` accessors the descriptor already exposes.
- Consolidate the two independent per-family layer-extraction paths (`CheckConfiguration`'s ad hoc calls vs. `PolicyConsistency.cs`'s `GetReferencedLayerNames` switch) onto one set of shared extraction helpers for the three families that need bespoke extraction.

**Non-Goals:**
- Replacing `ArchitectureAnalysisSession` itself, or introducing a seam interface for it.
- Changing which families currently receive configuration-reference validation. In particular, `composition` contracts' `AllowedOnlyInLayers` remains unfed into `CheckConfiguration` even though `GetReferencedLayerNames` already has a case for it — closing that gap would produce new violations for previously-passing policies, which is a policy-semantics change out of scope here.
- Reworking `ArchitectureContractChecker`/`ArchitectureContractHandlerRegistry`/contract execution (#227) — this is a parallel, independent extension point for a different concern (configuration inspection vs. contract checking).
- Touching `Contracts`, `Discovery`, `Scanning`, or `Reporting` modules.

## Decisions

### 1. Delegate, not interface, and shaped like `ArchitectureContractChecker`
`ArchitectureConfigurationContributor` is a delegate: `void ArchitectureConfigurationContributor(ArchitectureAnalysisSession session, ArchitectureConfigurationReferenceCollector collector, IArchitectureContract contract)`. This mirrors `ArchitectureContractChecker`'s `(session, contract) => result` shape and the design.md rationale already recorded for #227 (`openspec/changes/archive/2026-07-09-checker-registry/design.md`): family-specific logic differs in what it needs to read off the contract, but here — unlike checkers, which return heterogeneous results (violations vs. cycles vs. summaries) — every contributor's *effect* is uniform (push references into a shared collector), so a `void`-returning delegate over a shared mutable collector is simpler than forcing each contributor to return and merge its own result value. Passing `session` in (rather than making this a static function) preserves access to `session.IsContractSelected(...)`, needed by the package/project-metadata families.

**Alternative considered**: a generic `IArchitectureConfigurationContributor<TContract>` interface per family. Rejected for the same reason #227 rejected a generic checker interface — 16 tiny classes add more ceremony than 16 lambda entries in the registry, and the registry is already the established single source of truth for per-family wiring.

**Accessibility differs from `ArchitectureContractChecker` on purpose**: `ArchitectureContractChecker` is `public` because it is exposed through the public `IArchitectureContractHandlerRegistry.TryGetHandler` API (consumed by cross-assembly test fakes in `ArchitectureBaselineApplicationServiceFakeCompositionTests.cs`/`ArchitectureValidationApplicationServiceFakeCompositionTests.cs`). `ArchitectureConfigurationContributor` has no such public consumer — it only flows between the internal `ArchitectureContractFamilyDescriptor` and `ArchitectureAnalysisSession.CheckConfiguration`'s private registry loop — so it stays `internal`, matching `ArchitectureConfigurationReferenceCollector`'s own `internal` visibility. The two delegates look parallel in shape but differ correctly in accessibility.

### 2. `ArchitectureConfigurationReferenceCollector` replaces the inline closures
A small class owns exactly what `CheckConfiguration`'s local closures (`AddLayerNames`, `AddExternalGroupNames`, `AddPackageGroupNames`) and locals (`layerReferencingContractIds`, `referencedExternalGroups`, `referencedPackageGroups`, `packageContractSources`, `projectMetadataContractProjects`) did, as named methods:
- `AddLayerNames(string? contractId, IEnumerable<string> names)`
- `AddExternalGroupNames(IEnumerable<string> names)`
- `AddPackageGroupNames(IEnumerable<string> names)`
- `AddPackageContractSource(string contractName, string? contractId, string source)`
- `AddProjectMetadataProject(string contractName, string? contractId, string projectPath)`

...plus read-only accessors (`LayerReferencingContractIds`, `ReferencedExternalGroups`, `ReferencedPackageGroups`, `PackageContractSources`, `ProjectMetadataContractProjects`) that the unchanged validation logic in `CheckConfiguration` reads from after the contribution loop. Internal representation (dictionary/set/list shapes) is identical to today's locals, so the validation logic below the collection point needs no behavioral changes — only variable-source changes.

### 3. Wiring lives on `ArchitectureContractFamilyDescriptor`/`ArchitectureContractFamilyRegistry`, not a parallel registry
Add one nullable property, `ArchitectureConfigurationContributor? ConfigurationContributor { get; init; }`, to `ArchitectureContractFamilyDescriptor`, defaulting to `null` — following the exact pattern already used for `OwnedContractTypes`/`AdditionalValidation`. A parallel/separate registry was considered and rejected: the family identity, strict/audit accessors, and contract CLR type are already defined once per family in `ArchitectureContractFamilyRegistry.All`; a second registry would require keeping two lists in sync by family id, reintroducing the exact fragmentation this change removes.

`CheckConfiguration(bool strict)` becomes:
```
var collector = new ArchitectureConfigurationReferenceCollector();
foreach (var descriptor in ArchitectureContractFamilyRegistry.All)
{
    var contracts = strict ? descriptor.StrictContracts(Document.Contracts) : descriptor.AuditContracts(Document.Contracts);
    foreach (var contract in contracts)
    {
        descriptor.ConfigurationContributor?.Invoke(this, collector, contract);
    }
}
// ...existing validation logic, reading from `collector` instead of locals...
```
This also fixes the strict/audit duplication: both modes now share the same loop body, differing only in which accessor (`StrictContracts` vs `AuditContracts`) is invoked — already true of contract execution's dispatch, so no new concept is introduced.

### 4. Promote three extraction helpers to `internal static`
`GetTypePlacementReferencedLayerNames`, `GetAttributeUsageReferencedLayerNames`, `GetInterfaceImplementationReferencedLayerNames` (currently `private static` on `ArchitectureAnalysisSession.PolicyConsistency.cs`) become `internal static`, so the registry's contributor lambdas (which live in a different file, same assembly) can call them directly. `GetReferencedLayerNames`'s switch (also `PolicyConsistency.cs`, used by rule_input-coverage/policy-consistency) keeps calling the same helpers — no duplication is introduced, and both call sites now provably extract identical layer names for these three families.

### 5. The composition gap stays open, on purpose
`GetReferencedLayerNames` (`PolicyConsistency.cs:594`) already has `ArchitectureCompositionContract c => c.AllowedOnlyInLayers`, but no `AddLayerNames` call for `composition` exists in `CheckConfiguration` today, so composition contracts referencing an unknown layer produce no configuration violation. Since the acceptance bar for this change is "current configuration error behavior remains compatible," `composition`'s `ConfigurationContributor` stays `null` — matching current behavior exactly. This is called out explicitly (proposal.md, this section) rather than silently fixed, so a future change can decide, with the right sign-off, whether to close it as a deliberate behavior change.

## Risks / Trade-offs

- **[Risk]** Moving the `IsContractSelected(c.Id)` guard for `package_dependency`/`package_allow_only`/`project_metadata` into each contributor lambda could be missed or misplaced, silently changing which contracts are validated → **Mitigation**: existing tests in `PackageDependencyConfigurationTests.cs` and `ProjectMetadataConfigurationTests.cs` already assert on contract-selection interaction; keep them green, add no new selection logic.
- **[Risk]** The rule_input-coverage dangling-deferral logic (`CollectRuleInputCoveredContractIds`, `IsFullyOwnedByRuleInputCoverage`) depends on the exact `Dictionary<string, HashSet<string>>` shape of `layerReferencingContractIds` → **Mitigation**: `ArchitectureConfigurationReferenceCollector`'s internal representation is a direct lift of the existing locals; `RuleInputCoverageValidationTests.cs` must pass unchanged as the regression gate for this interaction.
- **[Trade-off]** 16 small lambdas in `ArchitectureContractFamilyRegistry.cs` add lines to an already-large file, rather than reducing total line count — the win is logical (one place per family, no central switchboard to edit for new families), not a line-count reduction. This mirrors the same trade-off already accepted for `Checker` in #227.
- **[Risk]** The *relative order* of `CheckConfiguration`'s violations changed: the old hand-written strict/audit blocks visited families in the order `dependency, allow_only, cycle, method_body, independence, layer, protected, external, external_allow_only, package_dependency, package_allow_only, project_metadata, type_placement, attribute_usage, inheritance, interface_implementation`, whereas the new single loop visits `ArchitectureContractFamilyRegistry.All`'s order, so `layer` now runs second (not sixth) and `package_dependency`/`package_allow_only`/`project_metadata` now run before `protected`/`external`/`external_allow_only` (not after). This can reorder entries in `--format json` CI artifacts when a policy produces more than one configuration violation across families. No test pins an exact multi-violation order for `CheckConfiguration` today (existing tests all assert via `.Any(...)` on individual violations, not positional/sequence equality), so this is not a regression against any documented guarantee — but it is a real, observable ordering change worth calling out for anyone diffing JSON CI artifacts across this change. → **Mitigation**: none needed beyond this note, since ordering was never a documented contract of `CheckConfiguration`; if a future change wants ordering stability, `ArchitectureContractFamilyRegistry.All`'s order is now the single deterministic source to pin a test against.

## Migration Plan

Pure internal refactor within `ArchLinterNet.Core.Execution`; no data migration, no public API break, no config/schema change. Land as a single change: introduce the delegate/collector/descriptor field, rewire `CheckConfiguration`, promote the three helpers, run full test suite. No rollback concerns beyond reverting the commit.

## Open Questions

None outstanding — the composition gap is a resolved decision (left open, documented), not an open question.
