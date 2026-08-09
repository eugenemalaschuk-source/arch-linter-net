## ADDED Requirements

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
