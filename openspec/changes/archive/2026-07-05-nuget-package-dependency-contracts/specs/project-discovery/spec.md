## ADDED Requirements

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
