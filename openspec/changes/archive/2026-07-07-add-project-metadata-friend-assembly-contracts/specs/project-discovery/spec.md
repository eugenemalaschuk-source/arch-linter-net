## ADDED Requirements

### Requirement: Parse selected MSBuild properties for each discovered project
The system SHALL parse selected scalar MSBuild properties from each discovered or explicitly listed `.csproj` file, including project-local values and inherited values from the nearest applicable `Directory.Build.props` chain when available without full MSBuild evaluation.

#### Scenario: Project-local property is captured
- **WHEN** a discovered `.csproj` contains `<Nullable>enable</Nullable>`
- **THEN** discovery SHALL expose `Nullable=enable` for that project

#### Scenario: Inherited property from Directory.Build.props is captured
- **WHEN** a discovered project omits `<TreatWarningsAsErrors>` locally and the nearest applicable `Directory.Build.props` chain sets it to `true`
- **THEN** discovery SHALL expose `TreatWarningsAsErrors=true` for that project

### Requirement: Parse friend assembly declarations for each discovered project
The system SHALL parse `InternalsVisibleTo` item declarations from each discovered or explicitly listed `.csproj` file and expose the declared friend assembly names for that project.

#### Scenario: Multiple friend assemblies are recorded
- **WHEN** a `.csproj` declares three `InternalsVisibleTo` items
- **THEN** discovery SHALL expose all three friend assembly names for that project in deterministic order

#### Scenario: Project with no friend assemblies records an empty list
- **WHEN** a `.csproj` declares no `InternalsVisibleTo` items
- **THEN** discovery SHALL expose an empty friend-assembly list for that project

### Requirement: Parse project references for each discovered project
The system SHALL parse `ProjectReference` items from each discovered or explicitly listed `.csproj` file and expose the resolved referenced project paths for that project.

#### Scenario: Relative project reference is resolved
- **WHEN** a `.csproj` declares `<ProjectReference Include="../Tests/MyApp.Tests.csproj" />`
- **THEN** discovery SHALL expose the referenced project path resolved relative to the source project file

#### Scenario: Project with no project references records an empty list
- **WHEN** a `.csproj` declares no `ProjectReference` items
- **THEN** discovery SHALL expose an empty project-reference list for that project

### Requirement: Discovered projects expose metadata for project governance contracts
`ArchitectureDiscoveredProject` SHALL expose parsed project properties, friend assemblies, and project references for use by project metadata governance contracts without requiring successful build-output resolution.

#### Scenario: Metadata available even when build output is missing
- **WHEN** a discovered project has no resolvable build output and discovery reports a build-output diagnostic
- **THEN** the discovered project SHALL still expose its parsed properties, friend assemblies, and project references
