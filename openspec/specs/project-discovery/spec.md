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
- **WHEN** `analysis.solution` points to a file that exists but cannot be parsed as `.sln` or `.slnx` (for classic `.sln`, this includes a file missing the `Microsoft Visual Studio Solution File` header)
- **THEN** discovery does not throw; it produces a Configuration diagnostic naming the file and the parse failure

#### Scenario: Solution parses but discovers no projects
- **WHEN** `analysis.solution` points to a file that parses successfully (has a valid `.sln`/`.slnx` structure) but contains no `.csproj` entries, or all entries are filtered out by `project_include`/`project_exclude`
- **THEN** discovery produces a Configuration diagnostic naming the solution file, distinct from the unparsable-file diagnostic

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

### Requirement: Detect stale build outputs
The system SHALL compare a selected build output's last-write time against the project's `.csproj` file and all `*.cs` files under the project directory (excluding `bin`/`obj`), and SHALL treat an output older than its sources as unresolved rather than silently using it.

#### Scenario: Build output older than a changed source file
- **WHEN** a project's selected build output's DLL was last written before a `.cs` file under the project directory (outside `bin`/`obj`)
- **THEN** discovery does not select that output; it produces a Configuration diagnostic naming the project, the output path, and the build/source timestamps, distinct from "missing project build output"

#### Scenario: Build output older than the project file itself
- **WHEN** a project's selected build output's DLL was last written before the `.csproj` file
- **THEN** discovery treats the output as stale using the same diagnostic as a stale source file

#### Scenario: Build output newer than all sources
- **WHEN** a project's selected build output's DLL was last written after the `.csproj` file and every `.cs` file under the project directory
- **THEN** discovery selects that output normally and produces no staleness diagnostic

### Requirement: Discovery results feed assembly resolution and source roots
The system SHALL use discovery-derived assembly names and search paths only when `analysis.target_assemblies` is empty, and discovery-derived source roots only when `analysis.source_roots` is empty. Discovery logic SHALL be owned directly by `IArchitectureProjectDiscoveryService` (an instance service registered in `AddArchLinterNetCore()`), rather than forwarded to a static `ArchitectureProjectDiscovery` class.

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

#### Scenario: Discovery service resolves without a static forwarding call
- **WHEN** `IArchitectureProjectDiscoveryService` is resolved through `AddArchLinterNetCore()` and its resolution method is invoked
- **THEN** solution parsing and project-file parsing execute via instance collaborators owned by the service, with no reference to a static `ArchitectureProjectDiscovery`, `ArchitectureSolutionParser`, or `ArchitectureProjectFileParser` class

### Requirement: Parse package references for each discovered project
The system SHALL parse `PackageReference` items from each discovered/explicitly listed `.csproj` file's `ItemGroup` elements, recording the package ID (`Include`) and version (from the `Version` attribute or a child `<Version>` element, when present).

#### Scenario: Package reference with attribute version
- **WHEN** a `.csproj` has `<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />`
- **THEN** discovery records a package reference with ID `Newtonsoft.Json` and version `13.0.3` for that project

#### Scenario: Package reference with child element version
- **WHEN** a `.csproj` has `<PackageReference Include="Newtonsoft.Json"><Version>13.0.3</Version></PackageReference>`
- **THEN** discovery records a package reference with ID `Newtonsoft.Json` and version `13.0.3` for that project

#### Scenario: Package reference with no version
- **WHEN** a `.csproj` has `<PackageReference Include="Newtonsoft.Json" />` with no `Version` attribute or child element
- **THEN** discovery records a package reference with ID `Newtonsoft.Json` and no version, pending central package management resolution

#### Scenario: Project with no package references
- **WHEN** a `.csproj` declares no `PackageReference` items
- **THEN** discovery records an empty package reference list for that project

### Requirement: Resolve package versions from central package management
The system SHALL resolve a `PackageReference` with no declared version by looking up the same package ID (case-insensitively) in the nearest ancestor `Directory.Packages.props` file's `PackageVersion` items, walking upward from the project's directory and stopping at the first `Directory.Packages.props` found.

#### Scenario: Version resolved from central package management
- **WHEN** a project's `PackageReference` for `Newtonsoft.Json` has no `Version`, and the nearest ancestor `Directory.Packages.props` has `<PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />`
- **THEN** discovery records version `13.0.3` for that project's `Newtonsoft.Json` reference

#### Scenario: No matching central package version
- **WHEN** a project's `PackageReference` has no `Version`, and no ancestor `Directory.Packages.props` declares a matching `PackageVersion`
- **THEN** discovery records that package reference with no resolvable version

#### Scenario: Nearest Directory.Packages.props wins in nested scopes
- **WHEN** a project directory has an ancestor `Directory.Packages.props` closer than another `Directory.Packages.props` further up the directory tree, and both declare a `PackageVersion` for the same package ID with different versions
- **THEN** discovery uses the version from the nearer `Directory.Packages.props`

#### Scenario: Explicit project-level version is not overridden
- **WHEN** a project's `PackageReference` already declares an explicit `Version`
- **THEN** discovery uses that explicit version and does not consult `Directory.Packages.props`

### Requirement: Discovered projects expose their package references
`ArchitectureDiscoveredProject` SHALL expose the parsed package reference list (package ID and resolved version, when known) for use by package dependency and package allow-only contracts.

#### Scenario: Package references available for contract evaluation
- **WHEN** project discovery completes for a project with one or more `PackageReference` items
- **THEN** the corresponding `ArchitectureDiscoveredProject` entry's package reference list contains an entry for each declared `PackageReference`

### Requirement: Package reference parsing does not require a resolved build output
The system SHALL parse and expose package references for a discovered project regardless of whether that project's build output can be resolved (unlike assembly-name/search-path seeding, which requires a resolved output).

#### Scenario: Package references available without a build output
- **WHEN** a project's build output cannot be found on disk (producing a "missing project build output" diagnostic)
- **THEN** discovery still records that project's parsed package reference list

