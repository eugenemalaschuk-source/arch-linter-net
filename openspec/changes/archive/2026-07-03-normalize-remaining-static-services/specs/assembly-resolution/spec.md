## MODIFIED Requirements

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
