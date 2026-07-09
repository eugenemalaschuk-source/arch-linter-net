## 1. Registry scaffolding

- [x] 1.1 Create `src/ArchLinterNet.Core/Contracts/Families/` directory.
- [x] 1.2 Add `ArchitectureContractFamilyBinding` record (internal, `Contracts`-local) with `FamilyId`, `Strict`/`Audit` delegate accessors (`Func<ArchitectureContractGroups, IEnumerable<IArchitectureContract>>`), and `IncludeInContractEnumeration` bool.
- [x] 1.3 Add `ArchitectureContractFamilyBindings` static registry class with an empty `All` list, to be filled in as each family is migrated (task group 2).
- [x] 1.4 Mark `ArchitectureContractGroups` as `partial` in `ArchitectureContractModels.cs`.

## 2. Migrate contract families to per-family partial files

For each family below: move its two `[YamlMember]` properties and its `IArchitectureContract` POCO out of `ArchitectureContractModels.cs` into a new `src/ArchLinterNet.Core/Contracts/Families/<Family>ContractFamily.cs` file declaring `partial class ArchitectureContractGroups`, and add one entry to `ArchitectureContractFamilyBindings.All` (`IncludeInContractEnumeration = true` for all except `layer_template`, which is `false`).

- [x] 2.1 `dependency` (`Strict`/`Audit`, `ArchitectureDependencyContract`)
- [x] 2.2 `layer` (`StrictLayers`/`AuditLayers`, `ArchitectureLayerContract`)
- [x] 2.3 `layer_template` (`StrictLayerTemplates`/`AuditLayerTemplates`, `ArchitectureLayerTemplateContract`) — `IncludeInContractEnumeration = false`
- [x] 2.4 `allow_only` (`StrictAllowOnly`/`AuditAllowOnly`, `ArchitectureAllowOnlyContract`)
- [x] 2.5 `cycle` (`StrictCycles`/`AuditCycles`, `ArchitectureCycleContract`)
- [x] 2.6 `method_body` (`StrictMethodBody`/`AuditMethodBody`, `ArchitectureMethodBodyContract`)
- [x] 2.7 `asmdef` (`StrictAsmdef`/`AuditAsmdef`, `ArchitectureAsmdefContract`)
- [x] 2.8 `independence` (`StrictIndependence`/`AuditIndependence`, `ArchitectureIndependenceContract`)
- [x] 2.9 `assembly_independence` (`StrictAssemblyIndependence`/`AuditAssemblyIndependence`, `ArchitectureAssemblyIndependenceContract`)
- [x] 2.10 `assembly_dependency` (`StrictAssemblyDependency`/`AuditAssemblyDependency`, `ArchitectureAssemblyDependencyContract`)
- [x] 2.11 `assembly_allow_only` (`StrictAssemblyAllowOnly`/`AuditAssemblyAllowOnly`, `ArchitectureAssemblyAllowOnlyContract`)
- [x] 2.12 `package_dependency` (`StrictPackageDependency`/`AuditPackageDependency`, `ArchitecturePackageDependencyContract`)
- [x] 2.13 `package_allow_only` (`StrictPackageAllowOnly`/`AuditPackageAllowOnly`, `ArchitecturePackageAllowOnlyContract`)
- [x] 2.14 `project_metadata` (`StrictProjectMetadata`/`AuditProjectMetadata`, `ArchitectureProjectMetadataContract`)
- [x] 2.15 `protected` (`StrictProtected`/`AuditProtected`, `ArchitectureProtectedContract`)
- [x] 2.16 `external` (`StrictExternal`/`AuditExternal`, `ArchitectureExternalDependencyContract`)
- [x] 2.17 `external_allow_only` (`StrictExternalAllowOnly`/`AuditExternalAllowOnly`, `ArchitectureExternalAllowOnlyContract`)
- [x] 2.18 `acyclic_sibling` (`StrictAcyclicSiblings`/`AuditAcyclicSiblings`, `ArchitectureAcyclicSiblingContract`)
- [x] 2.19 `type_placement` (`StrictTypePlacement`/`AuditTypePlacement`, `ArchitectureTypePlacementContract`)
- [x] 2.20 `public_api_surface` (`StrictPublicApiSurface`/`AuditPublicApiSurface`, `ArchitecturePublicApiSurfaceContract`)
- [x] 2.21 `attribute_usage` (`StrictAttributeUsage`/`AuditAttributeUsage`, `ArchitectureAttributeUsageContract`)
- [x] 2.22 `inheritance` (`StrictInheritance`/`AuditInheritance`, `ArchitectureInheritanceContract`)
- [x] 2.23 `interface_implementation` (`StrictInterfaceImplementation`/`AuditInterfaceImplementation`, `ArchitectureInterfaceImplementationContract`)
- [x] 2.24 `composition` (`StrictComposition`/`AuditComposition`, `ArchitectureCompositionContract`)
- [x] 2.25 `coverage` (`StrictCoverage`/`AuditCoverage`, `ArchitectureCoverageContract`) — keep the existing "reserved, not implemented" doc comment intact

## 3. Rewire enumeration and validation

- [x] 3.1 Replace `ArchitectureContractGroups.EnumerateStrict`/`EnumerateAudit` (and the `AllStrict`/`AllAudit` properties backed by them) with projections over `ArchitectureContractFamilyBindings.All.Where(b => b.IncludeInContractEnumeration)`.
- [x] 3.2 Replace `DuplicateIdValidator`'s literal 50-entry group array with an iteration over `ArchitectureContractFamilyBindings.All` (both `Strict` and `Audit` accessors, all 25 families).
- [x] 3.3 Confirm no remaining references to the removed private `EnumerateStrict`/`EnumerateAudit` methods.
- [x] 3.4 Confirm `ArchitectureContractModels.cs` now contains only `ArchitectureContractDocument`, shared/support types (`ArchitectureAnalysisConfiguration`, `ArchitectureLayer`, `ArchitectureExternalDependencyGroup`, `ArchitecturePackageGroup`, `ArchitectureIgnoredViolation`, etc.), and the now-`partial` `ArchitectureContractGroups` shell — no per-family properties remain in this file.

## 4. Tests

- [x] 4.1 Add/extend a policy-loading round-trip test that loads a YAML fixture covering every registered family's strict and audit groups and asserts each `ArchitectureContractGroups` property is populated as before.
- [x] 4.2 Add a test asserting `ArchitectureContractGroups.AllStrict`/`AllAudit` enumerate exactly the families with `IncludeInContractEnumeration = true` and exclude `layer_template`.
- [x] 4.3 Add/extend a `DuplicateIdValidator` test asserting duplicate IDs are still detected in every family's strict and audit groups, including `layer_template`.
- [x] 4.4 Pick one representative family (e.g. `layer`) and add a focused test exercising both its strict and audit groups end-to-end through loading, enumeration, and duplicate-ID validation.
- [x] 4.5 Run the existing `ArchitectureContractSchemaTests` and full test suite to confirm no behavior change leaked into schema or catalog expectations.

## 5. Spec sync and validation

- [x] 5.1 Confirm `openspec/changes/decouple-yaml-contract-groups/specs/yaml-contract-loading/spec.md` delta matches the implemented registry shape (update wording if the final field/type names differ from the design draft).
- [x] 5.2 Run `openspec validate decouple-yaml-contract-groups --strict` (or equivalent) before archiving.
- [x] 5.3 Run `make fmt` and `make acceptance`; fix failures.

## 6. Archive and PR

- [ ] 6.1 Run `opsx-archive` to fold the spec delta into `openspec/specs/yaml-contract-loading/spec.md` and run `openspec validate --all`.
- [ ] 6.2 Open the PR referencing issue #216 with Summary, Architecture notes, Scope/non-goals, Tests run, Risks/follow-ups.
