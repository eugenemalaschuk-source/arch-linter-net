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

### Requirement: Artifact planning is metadata-only and complete
The system SHALL separate output/reference selection from CLR loading and SHALL compute a complete metadata-only reference closure for cache authorization. If the closure cannot be proven complete, it SHALL mark cache reuse ineligible.

#### Scenario: Unsupported closure input fails closed
- **WHEN** planning cannot resolve a selected artifact or its reference closure metadata
- **THEN** cache reuse is rejected before a cache outcome is accepted

### Requirement: Opt-in shared-framework probing for post-build resolution

The system SHALL accept an optional `analysis.shared_frameworks` list of shared
framework names (for example `Microsoft.AspNetCore.App`) in the policy YAML. When
non-empty, `IArchitectureAssemblyResolutionService.ResolvePostBuild` SHALL resolve
each named framework to its installed shared-framework directory on the host machine
and add that directory to the probing paths used by the isolated post-build load
scope. Consumers that do not set this field SHALL see no change in resolution
behavior.

#### Scenario: Named shared framework assembly resolves during post-build reflection

- **WHEN** `analysis.shared_frameworks` lists `Microsoft.AspNetCore.App`, the named
  framework is installed on the host machine, and a target assembly resolved via
  `--ensure-built` references a type defined in that framework
- **THEN** reflection over that type succeeds without a hand-authored runtimeconfig
  or `dotnet exec` wrapper

#### Scenario: Shared framework directory discovery order

- **WHEN** resolving a named shared framework's directory
- **THEN** the system SHALL prefer `DOTNET_ROOT`/`DOTNET_ROOT(X86)` when set, and
  otherwise derive the shared-framework store from the currently running runtime's
  own installation directory

#### Scenario: Highest compatible version selected

- **WHEN** more than one version directory exists under the named shared framework
- **THEN** the system SHALL select the highest version directory whose major version
  matches an anchor major version, preferring a release build over a prerelease
  build with the same or lower version
- **AND** when no anchor major version can be derived, the system SHALL select the
  highest parsed version directory across all installed major versions, still
  preferring a release build over a prerelease build

#### Scenario: Anchor major version priority

- **WHEN** determining the anchor major version for shared-framework selection
- **THEN** the system SHALL prefer, in order: (1) `analysis.target_framework` when
  set, (2) the major version of the target framework(s) actually resolved for this
  run's selected target assemblies' build output (from project discovery), (3) the
  currently running .NET runtime's own major version as a last resort
- **AND** the ArchLinterNet CLI's own runtime major SHALL NOT be preferred over a
  known discovered target framework, since the CLI always runs on its own fixed
  target framework regardless of what it analyzes

#### Scenario: A higher-major prerelease build does not shadow the anchored major

- **WHEN** the anchor major version is `10` and both a `10.x` release build and an
  `11.0.0-preview.*` build are installed for the named framework
- **THEN** the system SHALL select the `10.x` release build, not the numerically
  higher `11.0.0-preview.*` build

#### Scenario: No candidate exists for the anchor major version

- **WHEN** an anchor major version is known and no installed version directory for
  the named framework matches it
- **THEN** the system SHALL treat the framework as missing rather than selecting a
  version from a different major version

#### Scenario: Ambiguous discovered major versions fail closed

- **WHEN** `analysis.target_framework` is not set and the selected target assemblies'
  discovered build output resolves to more than one distinct major version
- **THEN** the system SHALL throw `InvalidOperationException` naming the conflicting
  major versions and directing the author to set `analysis.target_framework`, rather
  than selecting one of them

#### Scenario: Missing shared framework fails with an actionable diagnostic

- **WHEN** `analysis.shared_frameworks` names a framework that cannot be located on
  the host machine
- **THEN** the system SHALL throw `InvalidOperationException` naming the missing
  framework and the roots that were searched, before any post-build resolution
  proceeds

#### Scenario: Non-isolated resolution is unaffected

- **WHEN** analysis runs without `--ensure-built` (the non-isolated resolution path)
- **THEN** `analysis.shared_frameworks` has no effect; shared-framework probing
  applies only to the post-build isolated load scope

