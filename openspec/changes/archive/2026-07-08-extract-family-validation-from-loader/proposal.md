## Why

`ArchitecturePolicyDocumentLoader` (in `src/ArchLinterNet.Core/Contracts/`) has grown into a family switchboard: ~15 per-family validation methods (assembly/package dependency and allow-only, project metadata, type placement, public API surface, attribute usage, inheritance, interface implementation, composition, acyclic sibling, and the five coverage scopes) are hardcoded directly into `Load` and its `CoverageValidation` partial. Every new YAML contract family added since `v0.4.0` has meant editing this one class, violating the open/closed principle the rest of `ArchLinterNet.Core` already follows (e.g. `ArchitectureContractFamilyRegistry` for catalog construction). Issue #210 asks to extract this per-family logic so the loader becomes a parse-and-orchestrate boundary instead of owning all family validation.

## What Changes

- Introduce an internal `IArchitecturePolicyDocumentValidator` abstraction in `Contracts/` with a single `Validate(ArchitectureContractDocument document)` method.
- Extract each of the ~15 family-specific validation methods (plus the two cross-family checks `ValidateDuplicateIds` and `ValidateLayerNamespaces`) into its own validator class implementing that interface, each a direct lift of the existing method body — same exception types, same message text, no wording changes.
- Collapse the coverage family's dispatcher (`ValidateCoverageNamespaces`) and its four scope-specific helpers (`ValidateRuleInputCoverageContract`, `ValidateDependencyEdgeCoverageContract`, `ValidateProjectOrAssemblyCoverageContract`, plus the inline `namespace`-scope checks) into a single `CoverageValidator` class, preserving their internal call order.
- Introduce an ordered, internal pipeline collection (local to `Contracts/`) listing every validator in the exact sequence `Load` calls them today, since exceptions are thrown eagerly and order is load-bearing behavior.
- Rewrite `ArchitecturePolicyDocumentLoader.Load` to: deserialize, assign fallback ids, then iterate the ordered pipeline invoking `Validate(document)` on each entry — removing all per-family method bodies from the loader class.
- Do **not** wire this into `ArchitectureContractFamilyDescriptor.AdditionalValidation` (added in #208, lives in `Execution/ArchitectureContractFamilyRegistry.cs`). `docs/internal/core-architecture-blueprint.md` states as a hard rule that `Contracts` must depend on nothing else in `Core`, and `Execution`/`Reporting` both call `ArchitecturePolicyDocumentLoader.NormalizeToContractId` directly, so the loader cannot move out of `Contracts` either. Reaching into the `Execution` registry from `Load` would invert that documented dependency direction. `AdditionalValidation` remains unused, as it already is today — this deviates from the #208 design note that assumed it would later be wired into `Load`; the equivalent goal (per-family validator ownership) is achieved with a `Contracts`-local pipeline instead.

## Capabilities

### New Capabilities
- `policy-document-validation-pipeline`: defines the `IArchitecturePolicyDocumentValidator` abstraction, the ordered validator pipeline, and the requirement that `ArchitecturePolicyDocumentLoader.Load` orchestrates validators rather than implementing family-specific checks inline.

### Modified Capabilities
(none — this is an internal restructuring; no spec-level behavior in `yaml-contract-loading`, `contract-family-registry`, or any per-family contract capability changes. Validation messages, exception types, and load order are preserved exactly.)

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs` — shrinks to deserialization, fallback-id assignment, and pipeline orchestration; loses ~15 private validation methods.
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.CoverageValidation.cs` — removed; logic moves into a new `CoverageValidator` class.
- New files under `src/ArchLinterNet.Core/Contracts/Validators/` (or similar): one class per extracted validator, the `IArchitecturePolicyDocumentValidator` interface, and the ordered pipeline registry.
- No changes to `ArchitectureContractFamilyRegistry`, `ArchitectureContractFamilyDescriptor`, `ArchitectureContractCatalog`, or any public API surface (`IArchitecturePolicyDocumentLoader.Load` signature is unchanged).
- Existing invalid-policy tests (`AttributeUsageContractTests.cs`, `InheritanceContractTests.cs`, `CompositionContractTests.cs`, `TypePlacementContractTests.cs`, `PublicApiSurfaceContractTests.cs`, `PackageDependencyValidationTests.cs`, `AssemblyDependencyValidationTests.cs`, `AssemblyAllowOnlyValidationTests.cs`, `AssemblyIndependenceValidationTests.cs`, `ProjectMetadataConfigurationTests.cs`, `InterfaceImplementationContractTests.cs`, `PackageAllowOnlyContractTests.cs`, `RuleInputCoverageValidationTests.cs`, `DependencyEdgeCoverageValidationTests.cs`, `CoverageContractReservedTests.cs`, `ContractLoaderTests.cs`) must remain green unchanged, proving the extraction is behavior-preserving.
