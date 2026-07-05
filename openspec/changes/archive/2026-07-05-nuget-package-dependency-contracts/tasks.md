## 1. Project discovery: package reference parsing

- [x] 1.1 Extend `DiscoveredProjectFile` (`src/ArchLinterNet.Core/Discovery/ProjectDiscoveryModels.cs`) with a `PackageReferences` list (package ID + optional version).
- [x] 1.2 Extend `ArchitectureDiscoveredProject` with the same `PackageReferences` list.
- [x] 1.3 Extend `ArchitectureProjectFileParser.Parse` to read `ItemGroup/PackageReference` elements (`Include`, `Version` attribute or child `<Version>` element).
- [x] 1.4 Add central package management resolution: locate the nearest ancestor `Directory.Packages.props` from the project directory upward and resolve missing `PackageReference` versions from its `PackageVersion` entries (case-insensitive package ID match).
- [x] 1.5 Wire the parsed package reference list through `ArchitectureProjectDiscoveryService.ResolveFromDocument` into `ArchitectureDiscoveredProject`, independent of whether build output resolution succeeds.

## 2. Policy model: packages section and contract classes

- [x] 2.1 Add `ArchitecturePackageGroup` (`package_ids`, `package_prefixes`) and a `Packages` dictionary property on `ArchitectureContractDocument`, following the `ExternalDependencies`/`ArchitectureExternalDependencyGroup` shape.
- [x] 2.2 Add `ArchitecturePackageDependencyContract` (`name`, `id`, `source`, `forbidden`, `dependency_depth`, `ignored_violations`, `reason`) and `ArchitecturePackageAllowOnlyContract` (`name`, `id`, `source`, `allowed`, `dependency_depth`, `ignored_violations`, `reason`), mirroring `ArchitectureAssemblyDependencyContract`/`ArchitectureAssemblyAllowOnlyContract`.
- [x] 2.3 Add `StrictPackageDependency`/`AuditPackageDependency`/`StrictPackageAllowOnly`/`AuditPackageAllowOnly` list properties to `ArchitectureContractGroups`.

## 3. Policy loading validation

- [x] 3.1 Add `ValidateDuplicateIds` entries for the four new contract lists.
- [x] 3.2 Add `ValidatePackageDependencyContracts`/`ValidatePackageAllowOnlyContracts` in `ArchitecturePolicyDocumentLoader`, rejecting a `source` not present in `analysis.target_assemblies`, mirroring `ValidateAssemblyDependencyContracts`/`ValidateAssemblyAllowOnlyContracts`.
- [x] 3.3 Reject `dependency_depth: transitive` for both new families at load time, mirroring `RequireDirectDependencyDepth` load-time checks.

## 4. Package group matching

- [x] 4.1 Add an `ArchitecturePackageDependencyResolver` (or extend the existing external-dependency resolver pattern) implementing exact `package_ids` match and dot-segment `package_prefixes` match, case-insensitive.

## 5. Contract evaluation

- [x] 5.1 Add `ArchitectureAnalysisSession.PackageDependency.cs` with `CheckPackageDependencyContract` and `CheckPackageAllowOnlyContract`, mirroring `ArchitectureAnalysisSession.AssemblyDependency.cs`: resolve `source` against discovered projects' `AssemblyName`, evaluate forbidden/allowed package groups against that project's `PackageReferences`, respect `ignored_violations`, and defensively reject `dependency_depth: transitive` at check time.
- [x] 5.2 Add `ArchitectureViolation.ForbiddenPackageGroup` init property.
- [x] 5.3 Build violation evidence as `"{PackageId}@{Version}"` when a version is known, else `"{PackageId}"`; allow-only violations sorted by package ID ordinal.

## 6. Diagnostics and reporting

- [x] 6.1 Add `ArchitectureDiagnosticKind.PackageDependency` to the enum.
- [x] 6.2 Add `PackageDependencyDiagnostic` record (`ContractName`, `ContractId`, `SourceType`, `ForbiddenNamespace`, `ForbiddenReferences`, `ForbiddenPackageGroup`), mirroring `ExternalDependencyDiagnostic`.
- [x] 6.3 Add a mapping branch in `ArchitectureDiagnosticMapper.FromViolation` for `violation.ForbiddenPackageGroup != null`.

## 7. Handler registration and catalog wiring

- [x] 7.1 Add `PackageDependencyContractHandler`/`PackageAllowOnlyContractHandler` in `ArchitectureContractHandlers.cs`, delegating to the new session check methods.
- [x] 7.2 Register both handlers in `ServiceCollectionExtensions.AddArchLinterNetCore()`.
- [x] 7.3 Add `package_dependency`/`package_allow_only` family entries (strict + audit) to `ArchitectureContractCatalog.Build`.

## 8. Tests

- [x] 8.1 Add project-file-parser tests for `PackageReference` parsing (attribute version, child-element version, missing version).
- [x] 8.2 Add central package management resolution tests (nearest `Directory.Packages.props` wins, no match leaves version unresolved, explicit project version not overridden).
- [x] 8.3 Add `PackageDependencyContractTests` covering: direct `PackageReference` violation, central-package-management-resolved violation, package group prefix match, allowed/passing case, `ignored_violations`, strict-vs-audit behavior, unresolvable `source` load-time rejection, `dependency_depth: transitive` rejection.
- [x] 8.4 Add `PackageAllowOnlyContractTests` mirroring `AssemblyAllowOnlyContractTests` structure (disallowed reference, sorted/deduplicated evidence, ignored entries, strict-vs-audit).
- [x] 8.5 Add a test project/`Directory.Packages.props` fixture set under the test project (or an in-memory `IArchitectureFileSystem` fake, matching the existing `ArchitectureProjectDiscoveryService` test pattern) covering test-project exclusion via `analysis.project_exclude`.

## 9. Documentation

- [x] 9.1 Add `docs/policy-format/package-dependencies.md` (YAML shape, matching rules, CPM resolution notes) mirroring `docs/policy-format/external-dependencies.md`.
- [x] 9.2 Add `docs/contracts/package-dependencies.md` (usage guide, examples, comparison to `external_dependencies` and `assembly_dependency`) mirroring `docs/contracts/assembly-dependency.md`.

## 10. Spec synchronization and archive

- [x] 10.1 Run `openspec validate --all` and fix any issues.
- [x] 10.2 Archive the change (`opsx-archive` / `openspec archive nuget-package-dependency-contracts`) after implementation, tests, and docs are complete.
