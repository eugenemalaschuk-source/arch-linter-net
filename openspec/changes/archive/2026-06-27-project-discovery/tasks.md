## 1. Schema

- [x] 1.1 Add `Solution`, `Projects`, `ProjectInclude`, `ProjectExclude`, `Configuration`, `TargetFramework` to `ArchitectureAnalysisConfiguration` in `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` with matching YAML aliases (`solution`, `projects`, `project_include`, `project_exclude`, `configuration` default `"Debug"`, `target_framework`).

## 2. Discovery module

- [x] 2.1 Create `src/ArchLinterNet.Core/Discovery/` module with `ArchitectureProjectDiscovery` (entry point) and supporting types (`DiscoveredProject`, `ProjectDiscoveryResult`, `ProjectDiscoveryDiagnostic` or reuse `ConfigurationDiagnostic`).
- [x] 2.2 Implement `.slnx` parsing (XML `<Project Path="...">`, nested `<Folder>` elements).
- [x] 2.3 Implement classic `.sln` parsing (line-based `Project(...)` entries, skip non-`.csproj` references).
- [x] 2.4 Implement `.csproj` parsing via `System.Xml.Linq`: `AssemblyName` (default to file name), `TargetFramework`/`TargetFrameworks`.
- [x] 2.5 Implement `project_include`/`project_exclude` glob filtering for solution-discovered projects only (reuse existing glob pattern logic from `Resolution/NamespaceGlobPattern` if applicable, or adapt it).
- [x] 2.6 Implement build output candidate resolution: `bin/{Configuration}/{TargetFramework}/{AssemblyName}.dll`, deterministic multi-target selection per design.md, and diagnostics for missing/ambiguous output.
- [x] 2.7 Implement diagnostics for missing/unparsable solution or project files.

## 3. Integration

- [x] 3.1 Wire `ArchitectureProjectDiscovery` into `ArchitectureRunnerFactory.BuildRunner()` before `ArchitectureAssemblyResolver.ResolveFromDocument()`: merge discovered assembly names/search paths into the effective config only when `target_assemblies`/`assembly_search_paths` are empty.
- [x] 3.2 Wire discovered source roots into the effective `source_roots` passed to `ArchitectureSourceScanner` only when `analysis.source_roots` is empty and discovery is configured.
- [x] 3.3 Surface discovery diagnostics through the existing `CheckConfiguration()` / `ConfigurationDiagnostic` pipeline in `ArchitectureContractRunner`.

## 4. Tests

- [x] 4.1 Add NUnit fixtures (temp-directory authored) for: explicit-only (no discovery, regression), solution-based (`.slnx`) discovery, solution-based (`.sln`) discovery, project-based (`analysis.projects`) discovery, missing build output, ambiguous multi-target output, single-resolved multi-target output, `project_include`/`project_exclude` filtering, discovered source roots feeding source scanning.
- [x] 4.2 Verify existing `ConfigurationCheckTests.cs` and `assembly-resolution`-covered behavior is unchanged (no regressions) when discovery fields are unset.

## 5. Docs and spec sync

- [x] 5.1 Document `analysis.solution`, `analysis.projects`, `analysis.project_include`, `analysis.project_exclude`, `analysis.configuration`, `analysis.target_framework` in `docs/reference/yaml-schema.md`.
- [x] 5.2 Run `openspec archive project-discovery` after implementation and tests are complete; run `openspec validate --all`.
