## Context

`ArchitectureContractGroups` (in `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`) is a single sealed class with 50 `[YamlMember]`-decorated `List<T>` properties — one strict/audit pair per contract family, 25 families today. The same 25 family names are independently re-enumerated by hand in five places: (1) `ArchitectureContractGroups.EnumerateStrict`/`EnumerateAudit` (24 families — deliberately **excludes** `layer_template`, whose raw contracts are expanded into real layer contracts elsewhere and are never themselves "real" contracts), (2) `DuplicateIdValidator`'s literal 50-entry group array (25 families, **includes** `layer_template`), (3) `Execution.ArchitectureContractFamilyRegistry.All` (added in #208), (4) `schema/dependencies.arch.schema.json`, and (5) `archlinternet.capabilities.json`. This change addresses (1) and (2), which live in `Contracts` and are the ones issue #216 is scoped to.

A hard constraint carries forward from #219: `Contracts` must depend on nothing else in `Core`, including `Execution`. That's why `Execution.ArchitectureContractFamilyDescriptor.AdditionalValidation` was added but deliberately never wired up — the validator pipeline that would consume it lives in `Contracts`. Any new family metadata this change introduces must stay `Contracts`-local; it will necessarily duplicate (not share) the family list already in `Execution.ArchitectureContractFamilyRegistry`, same as #219 accepted for the validator pipeline.

`ArchitectureContractDocument` already binds `Layers`/`ExternalDependencies`/`Packages` as `Dictionary<string, T>` — proving dictionary-keyed YAML binding is precedented in this model — but applying that to `ArchitectureContractGroups` would mean every family's YAML keys collapse into one dictionary shape, losing the flat `strict_x`/`audit_x` top-level keys the schema and every existing policy file rely on, and would require a custom `INodeDeserializer` to preserve them. That's a materially higher-risk change than needed to solve the actual pain point (hand-enumeration pressure), so this design does not pursue it.

## Goals / Non-Goals

**Goals:**
- Stop `ArchitectureContractModels.cs` from being the single file every new contract family must edit.
- Collapse the two `Contracts`-local hand-enumerations (`EnumerateStrict`/`EnumerateAudit` and `DuplicateIdValidator`'s group array) onto one `Contracts`-local registry, each preserving its current (different) family subset exactly.
- Keep 100% of existing YAML syntax, `[YamlMember]` aliases, `IgnoreUnmatchedProperties` behavior, and the `strict_coverage`/`audit_coverage` reserved-but-unimplemented binding unchanged.
- Document the resulting pattern as the path future contract-family tasks follow.

**Non-Goals:**
- Changing YAML syntax, adding/removing contract families, or changing strict/audit semantics.
- Touching `Execution.ArchitectureContractFamilyRegistry`/`ArchitectureContractFamilyDescriptor`, `schema/dependencies.arch.schema.json`, `archlinternet.capabilities.json`, or docs — none of these need to change for this refactor, and #219 already established that `Execution`'s own family list is allowed to duplicate `Contracts`'.
- Introducing dictionary-backed or dynamic YAML binding, source generators, or reflection-based enumeration.
- Any public API or Testing-adapter surface change.

## Decisions

**1. Partial-class split over dictionary binding or source generation.** `ArchitectureContractGroups` becomes `partial`; each family's two `[YamlMember]` properties and its `IArchitectureContract` POCO move into their own file under `src/ArchLinterNet.Core/Contracts/Families/` (e.g. `LayerContractFamily.cs`, `CycleContractFamily.cs`). YamlDotNet binds partial classes exactly like non-partial ones (it reflects over the merged member list), so this is a zero-behavior-change, compile-time-only reorganization. A new family now adds one new file instead of editing the shared 723-line file — "extending" the type, not the file.

**2. A `Contracts`-local delegate-based family binding registry, not reflection.** New internal record (name TBD at implementation, e.g. `ArchitectureContractFamilyBinding`) with `FamilyId`, `Func<ArchitectureContractGroups, IEnumerable<IArchitectureContract>> Strict`, `Func<ArchitectureContractGroups, IEnumerable<IArchitectureContract>> Audit`, and `bool IncludeInContractEnumeration`. A static list of 25 entries is the new single per-family registration point. Delegates (`g => g.StrictLayers`) fail to compile if a property is renamed, matching the precedent set by `Execution.ArchitectureContractFamilyDescriptor` in #208 — reflection-by-convention (e.g. matching property names to family ids by string) was rejected there for the same reason and is rejected here.

**3. Two call sites, two projections of one registry — not two registries.** `AllStrict`/`AllAudit` filter the registry to `IncludeInContractEnumeration == true` (24 families, `layer_template` excluded, preserving current behavior exactly). `DuplicateIdValidator` iterates the full 25-entry registry (both `Strict` and `Audit` accessors → 50 groups, `layer_template` included, preserving current behavior exactly). This is the one behavioral subtlety in the current code that a naive "just add up all families" registry would silently break — confirmed by reading both call sites directly, not inferred.

**4. `Execution`'s registry stays untouched and stays duplicate.** Making `Execution.ArchitectureContractFamilyRegistry` consume the new `Contracts`-local registry would require `Execution` to depend on it, which is already allowed (Execution already depends on `Contracts`) — but doing so is out of scope here: it wasn't broken, #219 already chose duplication over unification for exactly this boundary, and folding it in would widen this change beyond what #216 asks for.

## Risks / Trade-offs

- [Risk: partial-class split across ~25 new files is a large mechanical diff, easy to introduce a copy-paste alias/type mistake] → Mitigation: existing `ArchitectureContractSchemaTests` and policy-loading tests already pin every YAML key; add a full-document round-trip regression test loading a policy file that exercises every family before/after the split; run `make acceptance` before opening the PR.
- [Risk: registry filter (`IncludeInContractEnumeration`) silently gets the `layer_template` exclusion wrong in one direction] → Mitigation: add an explicit test asserting `AllStrict`/`AllAudit` family count is 24 and excludes `layer_template`, and a separate test asserting `DuplicateIdValidator` still checks `layer_template` (e.g. duplicate template IDs still throw).
- [Risk: future readers assume the new `Contracts`-local registry and `Execution.ArchitectureContractFamilyRegistry` are the same thing and try to merge them casually] → Mitigation: doc comment on the new registry cross-referencing the `Execution` one and citing the `Contracts`-must-not-depend-on-`Execution` constraint, plus the updated `yaml-contract-loading` spec.

## Migration Plan

Pure internal refactor, no data migration, no config/CLI changes, no versioning implications. Land as one PR; if regression tests reveal a family was missed, fix forward before merge — nothing about this change requires a rollback plan (revert-the-PR is sufficient).

## Open Questions

None — design decisions above are final for this change.
