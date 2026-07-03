## MODIFIED Requirements

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
