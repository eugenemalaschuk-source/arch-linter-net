## 1. Models and YAML wiring

- [x] 1.1 Add `ArchitectureAssemblyDependencyContract` to `ArchitectureContractModels.cs` (`name`, `id`, `source: string`, `forbidden: List<string>`, `ignored_violations`, `reason`)
- [x] 1.2 Add `ArchitectureAssemblyAllowOnlyContract` to `ArchitectureContractModels.cs` (`name`, `id`, `source: string`, `allowed: List<string>`, `ignored_violations`, `reason`)
- [x] 1.3 Add `StrictAssemblyDependency`/`AuditAssemblyDependency` list properties to `ArchitectureContractGroups` with `[YamlMember(Alias = "strict_assembly_dependency")]`/`"audit_assembly_dependency"`, wired into `EnumerateStrict()`/`EnumerateAudit()`
- [x] 1.4 Add `StrictAssemblyAllowOnly`/`AuditAssemblyAllowOnly` list properties to `ArchitectureContractGroups` with `[YamlMember(Alias = "strict_assembly_allow_only")]`/`"audit_assembly_allow_only"`, wired into `EnumerateStrict()`/`EnumerateAudit()`

## 2. Loader validation

- [x] 2.1 Add the four new groups to the duplicate-contract-id validation group array in `ArchitecturePolicyDocumentLoader.cs`
- [x] 2.2 Add `ValidateAssemblyDependencyContracts`: every `source`/`forbidden` assembly name in `strict_assembly_dependency`/`audit_assembly_dependency` contracts must be present in `analysis.target_assemblies`; raise a clear, deterministic error identifying the contract and the unresolvable assembly name otherwise
- [x] 2.3 Add `ValidateAssemblyAllowOnlyContracts`: every `source`/`allowed` assembly name in `strict_assembly_allow_only`/`audit_assembly_allow_only` contracts must be present in `analysis.target_assemblies`; raise the same style of error
- [x] 2.4 Wire both new validation methods into `Load()`
- [x] 2.5 Confirm the four new groups are NOT added to `CollectLayerBearingContractIds` (assembly names are not `document.Layers` keys)

## 3. Check logic

- [x] 3.1 Add `ArchitectureAnalysisSession.AssemblyDependency.cs` (new partial file) with `CheckAssemblyDependencyContract(ArchitectureAssemblyDependencyContract contract)` and `CheckAssemblyAllowOnlyContract(ArchitectureAssemblyAllowOnlyContract contract)`
- [x] 3.2 Implement `CheckAssemblyDependencyContract`: resolve `source` from `Context.TargetAssemblies` by simple name; for each entry in `contract.Forbidden` (declaration order, skipping the source's own name), resolve the forbidden assembly and check `source.GetReferencedAssemblies()` for a direct match
- [x] 3.3 Implement `CheckAssemblyAllowOnlyContract`: resolve `source`; compute direct references restricted to names present in `Context.TargetAssemblies` and not in `allowed`/self; report remaining names deduplicated and sorted ordinally
- [x] 3.4 Construct `ArchitectureViolation` per violation reusing existing fields (contract name/id, source assembly name, forbidden/disallowed assembly name(s), evidence); use `"outside allowed assemblies"` as the forbidden-namespace text for allow-only violations
- [x] 3.5 Respect `IsContractSelected(contract.Id)` and apply ignore matching via `CreateExecutionContext`/`ArchitectureContractExecutionContext.IsIgnored`, consistent with other contract families
- [x] 3.6 Guarantee `CheckAssemblyDependencyContract` output ordering follows `contract.Forbidden` declaration order, not `GetReferencedAssemblies()` enumeration order

## 4. Catalog, handler, and DI wiring

- [x] 4.1 Add `AddGroup("strict_assembly_dependency", "strict", "assembly_dependency", groups.StrictAssemblyDependency)` and the audit counterpart to `ArchitectureContractCatalog.Build`
- [x] 4.2 Add `AddGroup("strict_assembly_allow_only", "strict", "assembly_allow_only", groups.StrictAssemblyAllowOnly)` and the audit counterpart to `ArchitectureContractCatalog.Build`
- [x] 4.3 Add `AssemblyDependencyContractHandler` to `ArchitectureContractHandlers.cs` (family `"assembly_dependency"`, delegates to `session.CheckAssemblyDependencyContract`)
- [x] 4.4 Add `AssemblyAllowOnlyContractHandler` to `ArchitectureContractHandlers.cs` (family `"assembly_allow_only"`, delegates to `session.CheckAssemblyAllowOnlyContract`)
- [x] 4.5 Register both new handlers in `ServiceCollectionExtensions.AddArchLinterNetCore()`

## 5. JSON schema

- [x] 5.1 Add `strict_assembly_dependency`/`audit_assembly_dependency` array properties to `schema/dependencies.arch.schema.json`, with `$defs.assemblyDependencyContract` (`name`, `source`, `forbidden` required)
- [x] 5.2 Add `strict_assembly_allow_only`/`audit_assembly_allow_only` array properties, with `$defs.assemblyAllowOnlyContract` (`name`, `source`, `allowed` required)

## 6. Tests

- [x] 6.1 Assembly dependency: clean source with no forbidden direct reference produces no violations
- [x] 6.2 Assembly dependency: direct forbidden reference produces a violation identifying source/forbidden assembly and contract id
- [x] 6.3 Assembly dependency: multiple forbidden assemblies with deterministic violation ordering (declaration order)
- [x] 6.4 Assembly dependency: source listed in its own `forbidden` list produces no self-violation
- [x] 6.5 Assembly dependency: transitive-only reference (no direct edge) produces no violation
- [x] 6.6 Assembly dependency: strict mode failure vs. audit mode reporting-only behavior
- [x] 6.7 Assembly dependency: unresolvable `source` or `forbidden` assembly name raises a policy-load error
- [x] 6.8 Assembly dependency: ignored-violation entry suppresses a matching pair
- [x] 6.9 Assembly allow-only: all direct references allowed (or self) produces no violations
- [x] 6.10 Assembly allow-only: direct reference to a declared, non-allowed assembly produces a violation
- [x] 6.11 Assembly allow-only: direct reference to an assembly outside `analysis.target_assemblies` produces no violation
- [x] 6.12 Assembly allow-only: multiple disallowed references reported sorted and deduplicated
- [x] 6.13 Assembly allow-only: strict mode failure vs. audit mode reporting-only behavior
- [x] 6.14 Assembly allow-only: unresolvable `source` or `allowed` assembly name raises a policy-load error
- [x] 6.15 Assembly allow-only: ignored-violation entry suppresses a matching pair
- [x] 6.16 Regression: existing namespace/layer `strict`/`audit`, `strict_allow_only`/`audit_allow_only`, `strict_assembly_independence`/`audit_assembly_independence`, and Unity `asmdef` contract tests remain green and unmodified in behavior
- [x] 6.17 Catalog/handler-registry tests proving both new families dispatch identically to a direct session call (mirroring `ArchitectureContractHandlerRegistryTests` patterns)
- [x] 6.18 Multi-target case: contract referencing an assembly declared once in `analysis.target_assemblies` resolves correctly when multiple target assemblies are configured

## 7. Docs and samples

- [x] 7.1 Add `docs/contracts/assembly-dependency.md` covering both `assembly_dependency` and `assembly_allow_only`, distinguishing them from namespace/layer dependency/allow-only, `assembly_independence`, and Unity `.asmdef` checks; document ordered-layer/cycle-detection as explicit follow-up scope
- [x] 7.2 Add table rows to `docs/contracts/index.md`
- [x] 7.3 Update `docs/policy-format/index.md` and `docs/policy-format/supported-capabilities.md`
- [x] 7.4 Add an "Assembly dependency contract" / "Assembly allow-only contract" section to `docs/reference/yaml-schema.md`
- [x] 7.5 Update `docs/ai/capabilities.md` and `docs/ai/policy-authoring-guide.md`
- [x] 7.6 Update `README.md` feature bullet
- [x] 7.7 Add the new doc page to `mkdocs.yml` nav
- [x] 7.8 Extend `samples/policies/modular-monolith.yml` with working `assembly_dependency` and `assembly_allow_only` examples

## 8. Spec sync and validation

- [x] 8.1 Run `make fmt`
- [x] 8.2 Run `make acceptance`: 600/605 tests pass; the same 5 failures reproduce identically on a clean `main` checkout (Windows-only fake-filesystem/repository-root-resolver path handling and a Roslyn compilation-factory seam test), confirmed pre-existing and unrelated to this change. Self-policy strict and audit runs both pass via direct CLI invocation.
- [x] 8.3 Run `openspec validate --all`
- [x] 8.4 Run `openspec archive add-assembly-dependency-contracts`
