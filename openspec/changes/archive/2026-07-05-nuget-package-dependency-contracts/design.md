## Context

ArchLinterNet already governs first-party *type references* to external namespaces/types (`external_dependencies`, `strict_external`/`audit_external`) and first-party *assembly references* (`assembly_dependency`, `assembly_allow_only`, PR #58/#181/#184). Neither sees a `PackageReference` that no compiled type touches. Project discovery (`ArchitectureProjectDiscoveryService` / `ArchitectureProjectFileParser`) already parses each `.csproj` via `XDocument` for `AssemblyName`/`TargetFramework(s)` and exposes `ArchitectureDiscoveredProject` records through `ArchitectureAnalysisContext.ProjectDiscovery`; it does not read `PackageReference` items or `Directory.Packages.props` (central package management, "CPM") at all.

This change adds package-reference data to that same discovery pass and layers two new contract families on top, reusing the existing handler/catalog/diagnostic pipeline rather than inventing a parallel one.

## Goals / Non-Goals

**Goals:**
- Parse `PackageReference` items (`Include`, and `Version` from either the attribute or a child `<Version>` element) from each discovered/explicit `.csproj`.
- Resolve a package's version from the nearest ancestor `Directory.Packages.props` (`PackageVersion` entries) when the project's own `PackageReference` omits `Version`, matching standard NuGet CPM resolution (walk from the project directory upward, stop at the first `Directory.Packages.props` found).
- Let policies declare named forbidden package groups (`packages:` section) matched by exact package ID or dot-segment prefix, mirroring `external_dependencies` namespace/type-prefix semantics.
- Add `strict_package_dependency`/`audit_package_dependency` (forbidden groups) and `strict_package_allow_only`/`audit_package_allow_only` (allow-list) contract families, keyed by `source` = project's `AssemblyName` (same identifier assembly contracts already use), consistent with the existing assembly-dependency/assembly-allow-only shape.
- Emit diagnostics that name the source project, package ID, version (when known), contract id/name, and matched package group — structurally distinct (`ArchitectureDiagnosticKind.PackageDependency`) from `ExternalDependency` diagnostics.

**Non-Goals:**
- No NuGet restore, lock-file, or transitive package-graph resolution — only what is statically declared in `.csproj`/`Directory.Packages.props`.
- No vulnerability or license scanning.
- No semantic/runtime DI validation.
- No glob beyond simple dot-segment prefix matching (issue allows "prefix/glob where appropriate"; prefix matching alone covers the stated use cases — e.g. forbidding all of `Microsoft.EntityFrameworkCore.*` — without introducing a second matching DSL).

## Decisions

- **Source keyed by `AssemblyName`, not project path.** Assembly-dependency contracts already key `source`/`forbidden` by assembly name resolved through project discovery. Reusing that identifier keeps `packages` contracts consistent with `assembly_dependency` contracts (same `source:` value can be used across both families in one policy) and avoids introducing a second "what identifies a project" concept. Package data is attached to the same `ArchitectureDiscoveredProject` record discovery already produces, keyed the same way.
- **`source` validated against `analysis.target_assemblies` at policy load time, exactly like assembly contracts.** `ArchitecturePolicyDocumentLoader` already has `ValidateAssemblyDependencyContracts`/`ValidateAssemblyAllowOnlyContracts` helpers that reject an unresolvable `source`/`forbidden`/`allowed` assembly name at load time; package contracts get the same validation shape (`ValidatePackageDependencyContracts`/`ValidatePackageAllowOnlyContracts`) rather than inventing a separate "resolvable via project discovery" validation path. Package *data* itself (the `.csproj`/CPM-derived reference list) is still looked up via project discovery, keyed by the same assembly name.
- **New `packages` top-level section, separate from `external_dependencies`.** Per the issue's explicit requirement, package-reference groups stay independent from type-reference groups so users can opt into either or both without coupling their lifecycles. Same YAML shape family (`package_ids` / `package_prefixes`) as `namespace_prefixes`/`type_prefixes` for consistency.
- **Reuse `ArchitectureViolation`/handler/catalog pipeline.** New handlers (`PackageDependencyContractHandler`, `PackageAllowOnlyContractHandler`) implement the existing `IArchitectureContractHandler`; new families are registered in `ArchitectureContractCatalog.Build` and `ServiceCollectionExtensions.AddArchLinterNetCore()` exactly like `assembly_dependency`/`assembly_allow_only`. No changes to `ArchitectureContractExecutor` are needed (it already iterates whatever families the catalog declares).
- **New `ForbiddenPackageGroup` violation field + `PackageDependencyDiagnostic`.** Mirrors `ForbiddenExternalGroup`/`ExternalDependencyDiagnostic` exactly, so `ArchitectureDiagnosticMapper.FromViolation` gets one new `if` branch. Evidence strings use `"{PackageId}@{Version}"` (or bare `PackageId` when no version is resolvable) so the existing evidence-list shape (`IReadOnlyCollection<string>`) carries package+version without a new violation field.
- **Central package management resolution is best-effort and version-only.** We do not evaluate `ManagePackageVersionsCentrally`/`CentralPackageVersionOverrideEnabled` MSBuild properties (that requires SDK property evaluation, out of scope per "static project metadata"); instead, when a `PackageReference` has no `Version`, we look up the same package ID in the nearest ancestor `Directory.Packages.props` and use that version if found, otherwise leave version unresolved. This is deterministic and matches the common case without a full MSBuild property evaluator.
- **`dependency_depth: transitive` is rejected**, exactly like assembly contracts (`RequireDirectDependencyDepth`) — package references have no notion of transitivity here since no package graph is resolved.
- **Test project exclusion is policy-authored, not automatic.** Like assembly/external contracts, whether a test project is exempt is controlled entirely by which `source` values a policy declares contracts for (or `analysis.project_exclude`/`project_include` at discovery time) — no new "is this a test project" heuristic is introduced.

## Risks / Trade-offs

- [Package version left unresolved for props-based CPM edge cases (e.g. `Directory.Packages.props` version overridden per-TFM or via `VersionOverride`)] → Diagnostics report the package ID without a version in that case rather than guessing; this is acceptable since the contract only needs to identify the forbidden package, not its exact resolved version.
- [Introducing a second "source" identifier convention (assembly name) for something that is fundamentally project-level] → Accepted because it matches existing assembly-contract conventions the user already knows; documented explicitly in `docs/contracts/package-dependencies.md`.
- [`Directory.Packages.props` walk could pick up an unrelated file in monorepos with multiple CPM roots] → Walk stops at the first `Directory.Packages.props` found above the project (standard NuGet behavior), not the repository root, so nested CPM scopes resolve correctly.

## Migration Plan

Additive only — new YAML section, new contract families, new diagnostic kind. Existing policies and diagnostics are unaffected. No archived-change or breaking-change handling required.

## Open Questions

None — scope confirmed against issue #59 acceptance criteria and prior-art contract families (`assembly_dependency`, `assembly_allow_only`, `external_dependencies`).
