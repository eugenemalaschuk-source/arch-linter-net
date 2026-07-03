# Assembly Resolution Specification

## Purpose
Resolves the assemblies named in a policy's target_assemblies list to loaded .NET Assembly instances via a multi-probe-path strategy.
## Requirements
### Requirement: Resolve target assemblies from YAML document
The system SHALL resolve all assemblies listed in `analysis.target_assemblies` from the YAML document into `System.Reflection.Assembly` instances. When `analysis.target_assemblies` is empty, the system SHALL instead resolve assemblies from names contributed by project discovery (see `project-discovery` capability), if any were discovered. This resolution logic SHALL be owned directly by `IArchitectureAssemblyResolutionService` (an instance service registered in `AddArchLinterNetCore()`), rather than forwarded to a static `ArchitectureAssemblyResolver` class.

#### Scenario: All assemblies found
- **WHEN** `target_assemblies` lists 3 assembly names and all are loadable
- **THEN** the resolver returns 3 `Assembly` instances

#### Scenario: Assembly not found
- **WHEN** `target_assemblies` lists an assembly name that cannot be loaded from any probe path
- **THEN** the resolver does NOT throw; instead it collects the name into `ResolutionResult.MissingAssemblyNames`
- **AND** the probing paths searched are recorded in `ResolutionResult.AssemblyProbingPaths`
- **AND** `CheckConfiguration` later produces an `ArchitectureViolation` with `ForbiddenNamespace = "missing target assembly"` and a message listing the missing name and probe paths

#### Scenario: Empty target_assemblies with discovered names
- **WHEN** `target_assemblies` is empty and project discovery resolved 2 assembly names with existing build outputs
- **THEN** the resolver treats those 2 discovered names exactly as if they had been listed in `target_assemblies`, probing for them using the existing probe-path strategy

#### Scenario: Resolution service resolves without a static forwarding call
- **WHEN** `IArchitectureAssemblyResolutionService` is resolved through `AddArchLinterNetCore()` and its resolution method is invoked
- **THEN** the multi-probe-path resolution executes as an instance method with no reference to a static `ArchitectureAssemblyResolver` class

### Requirement: Multi-probe-path resolution strategy
The system SHALL probe for assemblies in this order: (1) already-loaded assemblies in `AppDomain.CurrentDomain`, (2) `Assembly.Load`, (3) env var `ARCHITECTURE_ASSEMBLY_SEARCH_PATHS`, (4) YAML `analysis.assembly_search_paths`, (5) search paths contributed by project discovery, (6) `AppContext.BaseDirectory`, (7) repository root, (8) `<repo>/artifacts/bin`, (9) `<repo>/bin`.

#### Scenario: Assembly found in already-loaded set
- **WHEN** an assembly with the target name is already loaded in the current AppDomain
- **THEN** the resolver returns that assembly without further probing

#### Scenario: Assembly found via env var probe path
- **WHEN** `ARCHITECTURE_ASSEMBLY_SEARCH_PATHS` is set to a directory containing the target DLL
- **THEN** the resolver loads and returns that assembly

#### Scenario: Duplicate assembly names deduplicated
- **WHEN** `target_assemblies` contains the same name twice
- **THEN** the resolver returns only one instance (deduplicated by name)

#### Scenario: Assembly found via project discovery search path
- **WHEN** `analysis.assembly_search_paths` does not contain a discovered project's build output directory, but project discovery selected that directory as a search path
- **THEN** the resolver loads the assembly from that discovered output directory

### Requirement: Empty target assemblies error
The system SHALL throw `InvalidOperationException` when `analysis.target_assemblies` is empty AND project discovery contributed no assembly names.

#### Scenario: No target assemblies defined and no discovery configured
- **WHEN** `target_assemblies` list is empty and `analysis.solution`/`analysis.projects` are not set
- **THEN** the system throws `InvalidOperationException` indicating assemblies must be defined

#### Scenario: No target assemblies defined but discovery configured with no resolvable projects
- **WHEN** `target_assemblies` list is empty, `analysis.solution` is set, but no projects could be discovered or none had a resolvable build output
- **THEN** the system throws `InvalidOperationException` indicating assemblies must be defined, in addition to any Configuration diagnostics describing why discovery found nothing

