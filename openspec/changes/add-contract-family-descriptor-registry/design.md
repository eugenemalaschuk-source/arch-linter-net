## Context

`ArchitectureContractCatalog.Build` (`src/ArchLinterNet.Core/Execution/ArchitectureContractCatalog.cs`) constructs the flat `ArchitectureContractDescriptor` list every contract executes from. Today it does this via ~25 hand-written pairs of `AddGroup("strict_x", "strict", "x", groups.StrictX)` / `AddGroup("audit_x", "audit", "x", groups.AuditX)` calls, one pair per family, in a fixed order that is observable (it determines violation/cycle insertion order and `--timings` entry order, per the comment at line 50-54 and the pinned test `FamiliesInOrder_MatchesHistoricalExecutorDispatchOrder`). Baseline capability is a second, independently hardcoded concern: `IsGroupResolvable` excludes `"asmdef"` and `"layer_template"` by name.

This is one of several god-file extension points named in issue #208. This change addresses only the catalog's family-discovery/order/baseline-capability concern — the smallest-blast-radius slice that has a concrete, literal duplication problem and whose behavior is fully pinned by existing tests, so preservation can be verified mechanically.

## Goals / Non-Goals

**Goals:**
- Replace the ~25 hand-written `AddGroup` call pairs with a single loop driven by an ordered list of descriptors.
- Replace the hardcoded `IsGroupResolvable` name check with a per-descriptor `IsBaselineCapable` flag.
- Give each family's catalog metadata (id, YAML group names, order, baseline capability, contract accessors, owned CLR types) a single home, so adding a family means adding one descriptor instead of editing `Build` by hand.
- Preserve `ArchitectureContractCatalog`'s full public behavior exactly: `FamiliesInOrder`, `ContractsFor`, `AvailableContractIds`, `BaselineCapableGroups`, `ContractIdsInGroup`, `ResolveGroup` all produce identical output for identical input documents.
- Reserve a placeholder slot on the descriptor for a future family-specific validation hook, without wiring it to anything in this change.

**Non-Goals:**
- Changing `ArchitectureContractModels.cs` / `ArchitectureContractGroups` — the YAML DTO stays typed lists; dictionary-based YAML binding is explicitly deferred.
- Touching `ArchitectureContractHandlers.cs`, `ArchitectureContractHandlerRegistry.cs`, or `ServiceCollectionExtensions.cs` — handler/DI wiring is a separately-scoped, already-reasonable extension point (see the `contract-handler-execution` capability) and is left untouched.
- Touching `ArchitecturePolicyDocumentLoader.cs` validation logic, `ArchitectureAnalysisSession.*` check methods, or diagnostics mapping.
- Any CLI-visible behavior change, including exit codes, JSON output shape, or diagnostic text.
- Runtime plugin loading or external/dynamic family registration.

## Decisions

**Descriptor shape: a sealed record with delegate accessors, not reflection or generics.**
`ArchitectureContractFamilyDescriptor` holds `Func<ArchitectureContractGroups, IEnumerable<IArchitectureContract>> StrictContracts` and `AuditContracts` delegates rather than, say, a generic `ArchitectureContractFamilyDescriptor<T>` or reflection-based property lookup by group name. Delegates let each descriptor capture arbitrary per-family logic (notably `layer_template`, whose accessor must call `LayerTemplateExpander.Expand(groups.StrictLayerTemplates)` rather than return the raw list) while keeping the descriptor list homogeneous (`IReadOnlyList<ArchitectureContractFamilyDescriptor>`, no generic variance issues). Reflection was rejected: it would silently break if a `Strict*`/`Audit*` property were renamed, with no compile-time signal, and `ArchitectureContractGroups` deliberately keeps its explicit typed properties (non-goal: no YAML model changes).

**Registry as a static class with an ordered list literal, not a builder or DI-registered service.**
`ArchitectureContractFamilyRegistry.All` is a plain `IReadOnlyList<ArchitectureContractFamilyDescriptor>` built once from an ordered object-initializer list — mirroring the current `AddGroup` call sequence line-for-line so the diff is easy to review and the order is trivially auditable. It is not registered in `ServiceCollectionExtensions` because `ArchitectureContractCatalog.Build` is a static factory method with no DI dependencies today (`ArchitectureContractCatalog.Build(document)`), and this change does not alter that.

**Baseline capability becomes descriptor data, not a name-based predicate.**
`IsBaselineCapable` replaces `IsGroupResolvable(string family) => family is not ("asmdef" or "layer_template")`. Every descriptor sets it explicitly (`true` for 23 families, `false` for `asmdef` and `layer_template`), so a new family's baseline capability is a decision made once, at the descriptor site, instead of an implicit default that a second hardcoded list must remember to exclude from.

**Owned CLR types and the validation-hook placeholder are inert metadata in this change.**
`OwnedContractTypes` (`IReadOnlyList<Type>`, e.g. `[typeof(ArchitectureDependencyContract)]`) and `AdditionalValidation` (`Action<ArchitectureContractDocument>?`, always `null` today) are populated/declared but not read by any production code path yet. They exist because the issue asks for these fields explicitly as future extension surface ("owned contract CLR types where useful", "family-specific validation hook placeholders"); wiring `AdditionalValidation` into `ArchitecturePolicyDocumentLoader.Load` is deliberately deferred to a later task since that loader's validation order and exception messages are behavior that this change must not risk.

**`Build` becomes a single loop over `ArchitectureContractFamilyRegistry.All`.**
For each descriptor, `Build` calls `AddGroup(descriptor.StrictGroupName, "strict", descriptor.FamilyId, descriptor.StrictContracts(groups))` then the audit equivalent — the same `AddGroup` helper and family-ordering (`familiesInOrder`/`seenFamilies`) logic stays, just parameterized from the descriptor instead of literal arguments.

## Risks / Trade-offs

- [Risk] A transcription error while converting 25 `AddGroup` pairs into descriptor entries silently reorders or drops a family → Mitigation: the existing `FamiliesInOrder_MatchesHistoricalExecutorDispatchOrder` test (which lists the exact 25-family order) and the other `ArchitectureContractCatalogTests` are run unmodified against the refactored `Build`; a new registry-level test additionally asserts no duplicate family ids and that the registry's family-id sequence equals the same historical list, so the check exists independent of catalog wiring.
- [Risk] `layer_template`'s expansion-via-accessor is easy to get subtly wrong (e.g. forgetting `LayerTemplateExpander.Expand`) → Mitigation: `ContractsFor_IncludesExpandedLayerTemplateContracts` and `AvailableContractIds_IncludesExpandedLayerTemplateIds` already cover this and are preserved unmodified.
- [Trade-off] Delegate-based accessors are slightly less discoverable than reading literal `AddGroup` calls top-to-bottom in one method → accepted, because the registry's ordered list is still read top-to-bottom and is now the single place to look, rather than one of several places (model, catalog, loader, handlers) a reader previously had to cross-reference to understand one family.

## Migration Plan

Single-PR, non-breaking, internal-only refactor (no public API surface changes — `ArchitectureContractDescriptor`, `ArchitectureContractCatalog`'s public members, and all consumers are unchanged). No feature flag or rollback plan is needed beyond a normal revert, since behavior is byte-for-byte preserved and verified by existing + new unit tests.
