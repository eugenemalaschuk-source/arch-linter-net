## Context

`ArchitectureAssemblyResolver.ResolveFromDocument` requires `analysis.target_assemblies` to be non-empty and resolves each name via a fixed probe-path order (see `assembly-resolution` spec). `ArchitectureSourceScanner` falls back to a hardcoded `["src", "tests"]` when `analysis.source_roots` is empty. Neither component understands `.sln`/`.slnx`/`.csproj`. This change adds a discovery step that runs before assembly resolution and produces the same shapes those components already consume (assembly names, search paths, source roots), so the resolver and scanner themselves stay unchanged.

## Goals / Non-Goals

**Goals:**
- Let a policy declare `analysis.solution` or `analysis.projects` instead of listing every assembly.
- Deterministically pick one build output per project even when multi-targeted.
- Produce actionable Configuration diagnostics when discovery can't resolve an output.
- Zero new NuGet dependencies; zero behavior change for policies that don't set the new fields.

**Non-Goals:**
- Building anything (`dotnet build`, `dotnet restore`, MSBuild evaluation). Discovery only reads files already on disk.
- Replacing explicit `target_assemblies`/`assembly_search_paths`/`source_roots` — those win whenever present.
- Performance/caching of discovery results (tracked under issue #19).
- Full MSBuild project evaluation (conditions, imports, `Directory.Build.props` property overrides). Only direct `<PropertyGroup>` reads of `TargetFramework(s)` and `AssemblyName` from the `.csproj` itself.

## Decisions

**Discovery as a pre-resolution step, not a resolver replacement.**
`ArchitectureRunnerFactory.BuildRunner()` calls a new `ArchitectureProjectDiscovery.ResolveFromDocument(document, repositoryRoot)` before `ArchitectureAssemblyResolver.ResolveFromDocument()`. Discovery returns a `ProjectDiscoveryResult` containing the assembly names and search paths it found; these are merged into the document's effective `TargetAssemblies`/`AssemblySearchPaths` only where the explicit lists are empty. `ArchitectureAssemblyResolver` is unmodified — it still just resolves names via probe paths, now possibly seeded by discovery.
- *Alternative considered*: teach the resolver itself to parse projects. Rejected — conflates two concerns (loading vs. discovering) and risks regressing the explicit-config path covered by existing `assembly-resolution` tests.

**Manual XML/line parsing, no MSBuild SDK / Buildalyzer.**
`.csproj` is read with `System.Xml.Linq` for `<TargetFramework>`, `<TargetFrameworks>`, `<AssemblyName>` (default: file name without extension). `.slnx` is XML (`<Project Path="...">`, including nested `<Folder>` elements). Classic `.sln` is parsed line-by-line for `Project("{guid}") = "Name", "Path", "{guid}"` entries, ignoring solution-folder pseudo-projects (paths not ending in `.csproj`).
- *Alternative considered*: add `Microsoft.Build`/Buildalyzer for accurate MSBuild evaluation (conditional properties, SDK defaults, imports). Rejected for this change — it's a much heavier dependency and full evaluation isn't needed to read `TargetFramework`/`AssemblyName`, which are simple literal properties in the overwhelming majority of real projects. Documented as a known limitation.

**Build output candidate path: `bin/{Configuration}/{TargetFramework}/{AssemblyName}.dll` relative to the project directory.**
This matches default SDK-style project output layout. `analysis.configuration` defaults to `"Debug"`.

**Multi-target selection order:** (1) `analysis.target_framework` if set — used verbatim, error if no output exists there; (2) if exactly one of the project's TFMs has an existing `.dll` at the expected path, use it; (3) otherwise (zero or more than one TFM with output) emit a Configuration diagnostic listing every TFM checked and whether an output existed for each, and add the project's assembly name to `MissingAssemblyNames`-equivalent reporting rather than guessing.
- *Alternative considered*: pick the lexicographically-last or highest-version TFM automatically. Rejected — issue acceptance criteria require selection to be "deterministic and documented," and an implicit highest-version heuristic is opaque next to an explicit error a user can immediately fix with `analysis.target_framework`.

**Source roots from discovered project directories.**
When `analysis.source_roots` is empty AND discovery is configured (`solution` or `projects` set), the distinct set of discovered project directories (relative to repository root) becomes the effective source roots passed to `ArchitectureSourceScanner`. When discovery is not configured, the existing `["src","tests"]` default is untouched.

**New diagnostics reuse `ConfigurationDiagnostic`/`ArchitectureDiagnosticKind.Configuration`.**
No new diagnostic kind. New diagnostic instances for: `solution/project file not found`, `solution/project file unparsable`, `project has no build output for any candidate target framework`, `project build output ambiguous across N target frameworks`. Each message names the project, the candidate paths checked, and which one (if any) was selected — satisfying the "documented in diagnostics" acceptance criterion.

**`project_include`/`project_exclude` semantics.**
Both are lists of glob patterns matched against the discovered project path relative to repository root (same `NamespaceGlobPattern`-style glob already used elsewhere in the codebase, reused rather than introducing a second glob implementation). `project_include` (if non-empty) narrows discovered projects to matches; `project_exclude` removes matches afterward. Applies only to projects discovered via `analysis.solution` — `analysis.projects` is already an explicit list and is not filtered.

## Risks / Trade-offs

- [Hand-rolled `.csproj`/`.sln` parsing misses edge cases (conditional `TargetFramework`, MSBuild property functions, custom `Directory.Build.props` overrides of `AssemblyName`)] → Mitigation: documented non-goal; diagnostics clearly state when a project's output can't be found rather than silently producing a wrong assembly name.
- [Classic `.sln` format has many legacy quirks (nested solution folders, non-C# project types)] → Mitigation: only `.csproj`-pointing entries are considered; non-C# project references are skipped without error.
- [New `Discovery` module could blur Core's existing module boundaries] → Mitigation: keep discovery as a self-contained, side-effect-free parser + selector returning plain data (`ProjectDiscoveryResult`), consumed only by `ArchitectureRunnerFactory`; no new public surface on `ArchitectureAssemblyResolver` or `ArchitectureSourceScanner`.

## Open Questions

None blocking — `analysis.target_framework` is global (not per-project) for this change; per-project overrides can be added later if multi-project multi-target policies need it, without a breaking schema change (additive field).
