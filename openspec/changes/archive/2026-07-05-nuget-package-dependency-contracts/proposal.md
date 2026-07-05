## Why

Existing `external_dependencies` contracts (`strict_external`/`audit_external`) catch forbidden vendor/framework types only when compiled code actually references them. A project can declare a forbidden `PackageReference` (e.g. EF Core in a domain project) that is never touched by first-party code yet still ships as architecture-relevant surface area — a supply-chain and layering signal that today's type-level scanning cannot see. Real .NET repositories also use central package management (`Directory.Packages.props`), which today's project discovery does not parse at all. Issue #59 asks for a first-class, declared-package contract family that governs `PackageReference`/central package version entries directly, independent of whether any type from that package is referenced in code.

## What Changes

- Extend project discovery (`ArchitectureProjectFileParser`, `ArchitectureDiscoveredProject`) to parse `PackageReference` items from each `.csproj` and resolve missing versions from the nearest ancestor `Directory.Packages.props` (central package management), exposing a per-project package reference list.
- Add a new top-level `packages` policy section for declaring named forbidden package groups, matched by exact `package_ids` and/or `package_prefixes` (dot-segment prefix matching, mirroring existing `namespace_prefixes` semantics).
- Add two new contract families: `package_dependency` (forbidden groups) and `package_allow_only` (allow-list), each with `strict_*`/`audit_*` variants, following the existing assembly-dependency/assembly-allow-only contract shape (`source` project/assembly, `forbidden`/`allowed` package group names, `ignored_violations`, `reason`).
- Add a new `PackageDependencyDiagnostic` model (source project, package group, matched package IDs/versions, contract id/name) and a new `ArchitectureDiagnosticKind.PackageDependency` value so package-reference violations are reported distinctly from `ExternalDependency` (type-reference) violations.
- Document the new policy section and contract family under `docs/policy-format/` and `docs/contracts/`.

## Capabilities

### New Capabilities
- `package-dependency-contracts`: declared `packages` groups, `strict_package_dependency`/`audit_package_dependency` contracts, `.csproj`/central-package-management parsing, and package-reference diagnostics.
- `package-allow-only-contracts`: `strict_package_allow_only`/`audit_package_allow_only` contracts restricting a source project to an allow-listed set of package groups.

### Modified Capabilities
- `project-discovery`: project file parsing gains `PackageReference` extraction and central package management (`Directory.Packages.props`) resolution; `ArchitectureDiscoveredProject` gains a package reference list.

## Impact

- `src/ArchLinterNet.Core/Discovery/` — project file parser, discovery models.
- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` — new `packages` section, new contract classes.
- `src/ArchLinterNet.Core/Execution/` — new contract handlers, catalog wiring, new `ArchitectureAnalysisSession.PackageDependency.cs` check methods, package group resolver.
- `src/ArchLinterNet.Core/Model/` — `PackageDependencyDiagnostic`, `ArchitectureDiagnosticKind.PackageDependency`, `ArchitectureViolation.ForbiddenPackageGroup`.
- `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticMapper.cs` — new mapping branch.
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs` — register new handlers.
- `docs/contracts/`, `docs/policy-format/` — new docs.
- `tests/ArchLinterNet.Core.Tests/` — new test fixtures/tests.
- No changes to existing `external_dependencies`/assembly contract behavior.
