## MODIFIED Requirements

### Requirement: CLI emits human and JSON architecture change reports
The CLI SHALL expose `change snapshot` to write a complete snapshot and `change report` to compare `--base` and `--current` snapshots. `change snapshot` SHALL accept `--ensure-built`, `--no-restore`, `--configuration`, `--framework`, `--platform`, and `--runtime` as explicit build-state options. When a build-state option is selected, the CLI SHALL apply the same preparation mode and effective output context to validation, namespace and assembly graph projection, and optional baseline-debt comparison before persisting the snapshot. A post-build snapshot SHALL use the supported isolated post-build analysis path and SHALL preserve canonical finding identity, requested mode, condition-set scope, deterministic ordering, and complete-result semantics. Ordinary snapshot invocation without those options SHALL remain non-building. `change report` SHALL support deterministic `human` and `json` output, leave existing validation behavior unchanged when it is not invoked, and return success for a completed report regardless of whether the report contains drift.

#### Scenario: Snapshot accepts a complete post-build analysis request
- **WHEN** a policy opts into `Microsoft.AspNetCore.App` and a user runs `change snapshot --ensure-built --configuration Debug --framework net10.0`
- **THEN** validation, both graph projections, and optional baseline-debt collection use the same post-build output context
- **AND THEN** the CLI writes a complete snapshot without requiring a consumer runtimeconfig, NuGet-cache DLL lookup, or `dotnet exec` workaround

#### Scenario: Ordinary snapshot remains non-building
- **WHEN** a user runs `change snapshot` without build-state options
- **THEN** the command does not restore or build implicitly

#### Scenario: JSON output is usable by CI
- **WHEN** `change report --format json` completes
- **THEN** stdout contains exactly one valid JSON document with ordered delta and debt sections

#### Scenario: Report does not perform partial analysis
- **WHEN** a user invokes `change report` with two snapshot paths
- **THEN** the command compares only the supplied complete snapshot artifacts
- **AND THEN** it does not select or analyze a changed-file or changed-project subset
