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

#### Scenario: Highest installed version selected

- **WHEN** more than one version directory exists under the named shared framework
- **THEN** the system SHALL select the highest parsed version directory

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
