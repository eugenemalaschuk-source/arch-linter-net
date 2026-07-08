## Why

`ArchitectureContractGroups` (`src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`) is a single 723-line class holding 50 strict/audit `[YamlMember]` list properties, one pair per contract family (25 families today). Every new family requires hand-editing this file in multiple places (add two properties, add two lines to `EnumerateStrict`/`EnumerateAudit`), and a third hand-enumeration of every family lives in `DuplicateIdValidator`. Issue #216 asks to reduce this central-DTO pressure now, before the upcoming execution/session refactor re-touches these same checkers, so future contract-family work doesn't keep expanding one mega file.

## What Changes

- Split `ArchitectureContractGroups` into a C# `partial class` across per-family files under `src/ArchLinterNet.Core/Contracts/Families/`, one file per contract family, each owning that family's strict/audit `[YamlMember]` properties and its `IArchitectureContract` POCO. No YAML property names, aliases, or `IgnoreUnmatchedProperties` behavior change.
- Add a `Contracts`-local family binding registry (delegate-based accessors, matching the fail-to-compile-on-rename approach already used by the `Execution`-side descriptor registry from #208) that becomes the single source of truth for "which families exist" within `Contracts`.
- Rewrite `ArchitectureContractGroups.AllStrict`/`AllAudit` (today's hand-written `EnumerateStrict`/`EnumerateAudit`, 24 families, excluding `layer_template`) to project over the registry.
- Rewrite `DuplicateIdValidator` (today's hand-written 50-group literal array, 25 families including `layer_template`) to iterate the same registry.
- Update the stale `yaml-contract-loading` spec (currently documents only 14 groups from several families ago) to describe the registry-driven model in family-count-agnostic language, documenting the path for future contract-family additions.
- No changes to `src/ArchLinterNet.Core/Execution/ArchitectureContractFamilyRegistry.cs`/`ArchitectureContractFamilyDescriptor.cs`, `schema/dependencies.arch.schema.json`, `archlinternet.capabilities.json`, or docs — none of these are affected by an internal `Contracts`-local reorganization.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `yaml-contract-loading`: the deserialized document's contract groups are now described as a registry-driven set of families (family-count-agnostic) rather than an enumerated fixed list of 14 groups; documents the extension path for adding a new contract family without growing one central DTO.

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` — `ArchitectureContractGroups` becomes `partial`; per-family properties move out to `Contracts/Families/*.cs`.
- `src/ArchLinterNet.Core/Contracts/Validators/DuplicateIdValidator.cs` — literal group array replaced by registry iteration.
- New: `src/ArchLinterNet.Core/Contracts/Families/*.cs` (per-family partial DTO slices + POCOs) and a new registry file (e.g. `ArchitectureContractFamilyBindings.cs`).
- `openspec/specs/yaml-contract-loading/spec.md` — requirement wording updated.
- Test project(s) covering `ArchitectureContractModels`, `DuplicateIdValidator`, and policy-loading round-trips gain regression coverage for the refactor.
- No public API, CLI, YAML syntax, schema, docs, or Testing-adapter changes.
