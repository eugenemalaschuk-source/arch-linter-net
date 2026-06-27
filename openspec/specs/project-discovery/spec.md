# project-discovery Specification

## Purpose
TBD - created by archiving change project-discovery. Update Purpose after archive.
## Requirements
### Requirement: Parse solution files into project paths
The system SHALL parse a `.sln` (classic) or `.slnx` (XML) file referenced by `analysis.solution` into a list of `.csproj` paths, skipping non-C# project entries and solution folders.

#### Scenario: Slnx solution with two projects
- **WHEN** `analysis.solution` points to a `.slnx` file containing two `<Project Path="...">` entries pointing to `.csproj` files
- **THEN** discovery returns both project paths, resolved relative to the solution file's directory

#### Scenario: Classic sln solution with a solution folder
- **WHEN** `analysis.solution` points to a `.sln` file containing one `Project(...)` entry for a `.csproj` and one for a solution folder
- **THEN** discovery returns only the `.csproj` entry; the solution folder entry is skipped without error

#### Scenario: Solution file not found
- **WHEN** `analysis.solution` points to a path that does not exist
- **THEN** discovery does not throw; it produces a Configuration diagnostic naming the missing path

#### Scenario: Solution file unparsable
- **WHEN** `analysis.solution` points to a file that exists but cannot be parsed as `.sln` or `.slnx`
- **THEN** discovery does not throw; it produces a Configuration diagnostic naming the file and the parse failure

### Requirement: Parse project files for assembly metadata
The system SHALL parse each discovered or explicitly listed `.csproj` file (via XML, without MSBuild SDK evaluation) to determine its `AssemblyName` and target framework(s).

#### Scenario: Single-targeted project
- **WHEN** a `.csproj` has `<TargetFramework>net8.0</TargetFramework>` and no explicit `<AssemblyName>`
- **THEN** discovery uses the project file name (without extension) as the assembly name and `net8.0` as the single target framework

#### Scenario: Explicit assembly name
- **WHEN** a `.csproj` has `<AssemblyName>MyLib</AssemblyName>`
- **THEN** discovery uses `MyLib` as the assembly name regardless of the project file name

#### Scenario: Multi-targeted project
- **WHEN** a `.csproj` has `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>`
- **THEN** discovery records both `net8.0` and `net9.0` as candidate target frameworks for that project

### Requirement: Filter discovered projects with include/exclude globs
The system SHALL apply `analysis.project_include` (if non-empty) to narrow solution-discovered projects to matching paths, then apply `analysis.project_exclude` to remove matches, using paths relative to the repository root. These filters SHALL NOT apply to projects listed explicitly via `analysis.projects`.

#### Scenario: Include narrows discovered set
- **WHEN** a solution discovers 5 projects and `analysis.project_include` is `["src/**"]`
- **THEN** only projects under `src/` remain after filtering

#### Scenario: Exclude removes matches
- **WHEN** discovered projects include one under `tests/Fixtures/` and `analysis.project_exclude` is `["tests/Fixtures/**"]`
- **THEN** that project is removed from the discovered set

#### Scenario: Explicit projects list is unaffected by filters
- **WHEN** `analysis.projects` explicitly lists a project path that would not match `analysis.project_include`
- **THEN** the explicitly listed project is still included

### Requirement: Resolve build output deterministically for multi-targeted projects
The system SHALL select exactly one build output per discovered project at `bin/{Configuration}/{TargetFramework}/{AssemblyName}.dll`, using `analysis.configuration` (default `Debug`) and, when set, `analysis.target_framework`.

#### Scenario: Single target framework with existing output
- **WHEN** a project targets only `net8.0` and `bin/Debug/net8.0/<AssemblyName>.dll` exists
- **THEN** discovery selects that output without requiring `analysis.target_framework`

#### Scenario: Multi-target with override
- **WHEN** a project targets `net8.0` and `net9.0`, both have existing outputs, and `analysis.target_framework` is `net9.0`
- **THEN** discovery selects the `net9.0` output

#### Scenario: Multi-target with exactly one existing output
- **WHEN** a project targets `net8.0` and `net9.0`, only `net8.0` has a build output on disk, and `analysis.target_framework` is not set
- **THEN** discovery selects the `net8.0` output

#### Scenario: Multi-target ambiguous
- **WHEN** a project targets `net8.0` and `net9.0`, both have existing outputs, and `analysis.target_framework` is not set
- **THEN** discovery does not guess; it produces a Configuration diagnostic naming the project and both candidate target frameworks

#### Scenario: No build output found
- **WHEN** a project's only target framework has no build output at the expected path
- **THEN** discovery produces a Configuration diagnostic naming the project and the path checked

### Requirement: Discovery results feed assembly resolution and source roots
The system SHALL use discovery-derived assembly names and search paths only when `analysis.target_assemblies` is empty, and discovery-derived source roots only when `analysis.source_roots` is empty.

#### Scenario: Explicit target_assemblies takes precedence
- **WHEN** `analysis.target_assemblies` is non-empty and `analysis.solution` is also set
- **THEN** the explicit `target_assemblies` list is used; discovery's assembly names are not added

#### Scenario: Discovery seeds empty target_assemblies
- **WHEN** `analysis.target_assemblies` is empty and `analysis.solution` resolves 2 projects with existing build outputs
- **THEN** the effective target assembly names passed to assembly resolution are the 2 discovered assembly names, and the effective search paths include each project's selected output directory

#### Scenario: Discovery seeds empty source_roots
- **WHEN** `analysis.source_roots` is empty and `analysis.projects` lists project files in `src/Core` and `src/Cli`
- **THEN** the effective source roots used by source/method-body scanning are `src/Core` and `src/Cli`

#### Scenario: No discovery configured falls back to defaults
- **WHEN** none of `analysis.solution`, `analysis.projects`, or `analysis.target_assemblies` are set
- **THEN** the system behaves exactly as before this change (assembly resolution still throws `InvalidOperationException` for empty `target_assemblies`, and source root scanning still defaults to `["src", "tests"]`)

