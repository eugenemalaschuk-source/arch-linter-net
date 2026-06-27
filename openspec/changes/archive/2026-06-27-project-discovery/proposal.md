## Why

Today a policy must hand-list every `analysis.target_assemblies` entry and often `analysis.assembly_search_paths` too. This is brittle: renaming a project, adding a target framework, or moving build output breaks the policy with no actionable error. First-class `.sln`/`.slnx`/`.csproj` discovery lets a policy point at the actual repository structure instead of a manually maintained assembly list, while leaving explicit configuration fully supported for projects that want exact control.

## What Changes

- Add `analysis.solution`, `analysis.projects`, `analysis.project_include`, `analysis.project_exclude`, `analysis.configuration`, and `analysis.target_framework` to the YAML schema.
- Add a discovery step that parses `.sln` (classic) and `.slnx` (XML) solution files into a list of project paths, and parses `.csproj` files (via `System.Xml.Linq`, no MSBuild SDK dependency) for `AssemblyName` and `TargetFramework(s)`.
- Resolve target assembly names and search paths from discovered projects' build outputs (`bin/{Configuration}/{TargetFramework}/{AssemblyName}.dll`) when `analysis.target_assemblies` is not set.
- Resolve `analysis.source_roots` from discovered project directories when not explicitly set and discovery is configured.
- Deterministically select a single target framework per project when multi-targeted: explicit `analysis.target_framework` override, else the single TFM with an existing build output, else a Configuration diagnostic naming the ambiguous candidates.
- Emit new Configuration diagnostics for: project/solution file not found or unparsable, project found but no build output present, and ambiguous multi-target output.
- Existing explicit `target_assemblies`/`assembly_search_paths`/`source_roots` behavior is unchanged and takes precedence over discovery results.

## Capabilities

### New Capabilities
- `project-discovery`: parses `.sln`/`.slnx`/`.csproj` files, resolves target assembly names, search paths, and source roots from discovered projects, and produces actionable diagnostics for missing/ambiguous outputs.

### Modified Capabilities
- `assembly-resolution`: `ArchitectureAssemblyResolver` must accept assembly names and search paths contributed by project discovery (in addition to explicit YAML config) without changing its existing resolution/probing behavior for explicit config.

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` — new `ArchitectureAnalysisConfiguration` fields.
- `src/ArchLinterNet.Core/Execution/ArchitectureRunnerFactory.cs` — invoke discovery before assembly resolution.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractRunner.Checking.cs` / `ArchitectureContractRunner.cs` — new Configuration diagnostics for discovery failures.
- `src/ArchLinterNet.Core/Scanning/ArchitectureSourceScanner.cs` — consumes discovered source roots (no change to its own logic, just receives different input).
- New module (e.g. `src/ArchLinterNet.Core/Discovery/`) for `.sln`/`.slnx`/`.csproj` parsing.
- `docs/reference/yaml-schema.md` — document new config fields.
- No new NuGet dependencies; no automatic `dotnet build` invocation.
