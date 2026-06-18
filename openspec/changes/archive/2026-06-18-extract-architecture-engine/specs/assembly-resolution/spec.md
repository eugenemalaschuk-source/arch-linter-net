## ADDED Requirements

### Requirement: Resolve target assemblies from YAML document
The system SHALL resolve all assemblies listed in `analysis.target_assemblies` from the YAML document into `System.Reflection.Assembly` instances.

#### Scenario: All assemblies found
- **WHEN** `target_assemblies` lists 3 assembly names and all are loadable
- **THEN** the resolver returns 3 `Assembly` instances

#### Scenario: Assembly not found
- **WHEN** `target_assemblies` lists an assembly name that cannot be loaded from any probe path
- **THEN** the resolver does NOT throw; instead it collects the name into `ResolutionResult.MissingAssemblyNames`
- **AND** the probing paths searched are recorded in `ResolutionResult.AssemblyProbingPaths`
- **AND** `CheckConfiguration` later produces an `ArchitectureViolation` with `ForbiddenNamespace = "missing target assembly"` and a message listing the missing name and probe paths

### Requirement: Multi-probe-path resolution strategy
The system SHALL probe for assemblies in this order: (1) already-loaded assemblies in `AppDomain.CurrentDomain`, (2) `Assembly.Load`, (3) env var `ARCHITECTURE_ASSEMBLY_SEARCH_PATHS`, (4) YAML `analysis.assembly_search_paths`, (5) `AppContext.BaseDirectory`, (6) repository root, (7) `<repo>/artifacts/bin`, (8) `<repo>/bin`.

#### Scenario: Assembly found in already-loaded set
- **WHEN** an assembly with the target name is already loaded in the current AppDomain
- **THEN** the resolver returns that assembly without further probing

#### Scenario: Assembly found via env var probe path
- **WHEN** `ARCHITECTURE_ASSEMBLY_SEARCH_PATHS` is set to a directory containing the target DLL
- **THEN** the resolver loads and returns that assembly

#### Scenario: Duplicate assembly names deduplicated
- **WHEN** `target_assemblies` contains the same name twice
- **THEN** the resolver returns only one instance (deduplicated by name)

### Requirement: Empty target assemblies error
The system SHALL throw `InvalidOperationException` when `analysis.target_assemblies` is empty.

#### Scenario: No target assemblies defined
- **WHEN** `target_assemblies` list is empty
- **THEN** the system throws `InvalidOperationException` indicating assemblies must be defined
