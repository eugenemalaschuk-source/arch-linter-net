## 1. Model and YAML wiring

- [x] 1.1 Add `ArchitectureAssemblyIndependenceContract` to `ArchitectureContractModels.cs` (`name`, `id`, `assemblies: List<string>`, `ignored_violations`, `reason`)
- [x] 1.2 Add `StrictAssemblyIndependence`/`AuditAssemblyIndependence` list properties to `ArchitectureContractGroups` with `[YamlMember(Alias = "strict_assembly_independence")]`/`"audit_assembly_independence"`, wired into `EnumerateStrict()`/`EnumerateAudit()`

## 2. Loader validation

- [x] 2.1 Add the two new groups to the duplicate-contract-id validation group array in `ArchitecturePolicyDocumentLoader.cs`
- [x] 2.2 Add a new validation step: every assembly name in `strict_assembly_independence`/`audit_assembly_independence` contracts must be present in `analysis.target_assemblies`; raise a clear, deterministic error identifying the contract and the unresolvable assembly name otherwise
- [x] 2.3 Confirm the new groups are NOT added to `CollectLayerBearingContractIds` (assembly names are not `document.Layers` keys)

## 3. Check logic

- [x] 3.1 Add `ArchitectureAnalysisSession.AssemblyIndependence.cs` (new partial file) with `CheckAssemblyIndependenceContract(ArchitectureAssemblyIndependenceContract contract)`
- [x] 3.2 Implement direct-reference detection: for each ordered pair of distinct assembly names in `contract.Assemblies` (declaration order), resolve both from `Context.TargetAssemblies` by simple name, call `Assembly.GetReferencedAssemblies()` on the source, and check for the forbidden assembly's simple name among direct references
- [x] 3.3 Construct `ArchitectureViolation` per violation reusing existing fields (contract name/id, source assembly name, forbidden assembly name, evidence)
- [x] 3.4 Respect `IsContractSelected(contract.Id)` and apply ignore matching via `CreateExecutionContext`/`ArchitectureContractExecutionContext.IsIgnored`, consistent with other contract families
- [x] 3.5 Guarantee output ordering follows `contract.Assemblies` declaration order, not `GetReferencedAssemblies()` enumeration order

## 4. Catalog, handler, and DI wiring

- [x] 4.1 Add `AddGroup("strict_assembly_independence", "strict", "assembly_independence", groups.StrictAssemblyIndependence)` and the audit counterpart to `ArchitectureContractCatalog.Build`
- [x] 4.2 Add `AssemblyIndependenceContractHandler` to `ArchitectureContractHandlers.cs` (family `"assembly_independence"`, delegates to `session.CheckAssemblyIndependenceContract`)
- [x] 4.3 Register `IArchitectureContractHandler, AssemblyIndependenceContractHandler` in `ServiceCollectionExtensions.AddArchLinterNetCore()`

## 5. JSON schema

- [x] 5.1 Add `strict_assembly_independence`/`audit_assembly_independence` array properties to `schema/dependencies.arch.schema.json`
- [x] 5.2 Add `$defs.assemblyIndependenceContract` (mirroring `independenceContract`, with `assemblies` instead of `layers`)

## 6. Tests

- [x] 6.1 Clean independent assemblies produce no violations
- [x] 6.2 Direct assembly reference produces a violation identifying source/forbidden assembly and contract id
- [x] 6.3 Multiple assemblies with deterministic violation ordering (declaration order)
- [x] 6.4 Strict mode failure vs. audit mode reporting-only behavior
- [x] 6.5 Unresolvable assembly name in the list raises a policy-load error
- [x] 6.6 Ignored-violation entry suppresses a matching pair
- [x] 6.7 Regression: existing namespace/layer `strict_independence`/`audit_independence` and Unity `asmdef` contract tests remain green and unmodified in behavior
- [x] 6.8 Catalog/handler-registry test proving the new family dispatches identically to a direct session call (mirroring `ArchitectureContractHandlerRegistryTests` patterns)

## 7. Docs and samples

- [x] 7.1 Add `docs/contracts/assembly-independence.md`, distinguishing namespace/layer independence, generic .NET assembly independence, and Unity `.asmdef` checks
- [x] 7.2 Add a table row to `docs/contracts/index.md`
- [x] 7.3 Update `docs/policy-format/index.md` and `docs/policy-format/supported-capabilities.md`
- [x] 7.4 Add an "Assembly independence contract" section to `docs/reference/yaml-schema.md`
- [x] 7.5 Update `docs/ai/capabilities.md` and `docs/ai/policy-authoring-guide.md`
- [x] 7.6 Update `README.md` feature bullet
- [x] 7.7 Add the new doc page to `mkdocs.yml` nav
- [x] 7.8 Extend `samples/policies/modular-monolith.yml` with a working assembly-independence example

## 8. Spec sync and validation

- [ ] 8.1 Run `rtk make fmt`
- [ ] 8.2 Run `rtk make acceptance` and confirm it passes, including self-policy strict/audit runs
- [ ] 8.3 Run `openspec validate --all`
- [ ] 8.4 Run `openspec archive add-assembly-independence-contracts`
