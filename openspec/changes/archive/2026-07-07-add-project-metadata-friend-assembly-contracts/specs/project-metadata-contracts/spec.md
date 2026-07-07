## ADDED Requirements

### Requirement: Declare project metadata governance contracts
The system SHALL allow `contracts.strict_project_metadata` and `contracts.audit_project_metadata` entries that target one or more discovered projects and declare one or more metadata expectations over selected MSBuild properties, allowed friend assemblies, or disallowed project-reference targets.

#### Scenario: Policy declares a strict project metadata contract
- **WHEN** a policy declares `contracts.strict_project_metadata` with a non-empty `projects` list and at least one metadata expectation
- **THEN** the policy loader SHALL expose a strict project metadata contract for evaluation

#### Scenario: Contract with no project selector is rejected
- **WHEN** a `project_metadata` contract declares an empty or missing `projects` list
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

#### Scenario: Contract with no expectations is rejected
- **WHEN** a `project_metadata` contract declares no required properties, no forbidden properties, no allowed friend assemblies, and no forbidden project references
- **THEN** policy loading SHALL fail with a configuration error identifying the contract

### Requirement: Validate required and forbidden project properties
The system SHALL allow a project metadata contract to require exact values for selected MSBuild properties and to forbid selected MSBuild properties from having configured values for matching discovered projects.

#### Scenario: Required property matches
- **WHEN** a contract requires `Nullable: enable` for a discovered project whose effective `Nullable` property is `enable`
- **THEN** no violation SHALL be reported for that property

#### Scenario: Required property mismatch is a violation
- **WHEN** a contract requires `IsPackable: true` for a discovered project whose effective `IsPackable` property is `false`
- **THEN** strict validation SHALL report a project metadata violation identifying the project, property key, expected value, and actual value

#### Scenario: Forbidden property value is a violation
- **WHEN** a contract forbids `IsTestProject: true` for a discovered production project and that project's effective `IsTestProject` property is `true`
- **THEN** strict validation SHALL report a project metadata violation identifying the project, property key, forbidden value, and actual value

### Requirement: Restrict friend assemblies to an explicit allowlist
The system SHALL allow a project metadata contract to declare `allowed_friend_assemblies`, and SHALL report a violation for each `InternalsVisibleTo` assembly declared by a matching project that is not present in that allowlist.

#### Scenario: Allowed friend assembly passes
- **WHEN** a project declares `InternalsVisibleTo` for `ArchLinterNet.Core.Tests` and the contract's `allowed_friend_assemblies` includes `ArchLinterNet.Core.Tests`
- **THEN** no friend-assembly violation SHALL be reported for that declaration

#### Scenario: Undeclared friend assembly is a violation
- **WHEN** a project declares `InternalsVisibleTo` for `ArchLinterNet.Cli` and the contract's `allowed_friend_assemblies` omits `ArchLinterNet.Cli`
- **THEN** strict validation SHALL report a violation identifying the project and forbidden friend assembly name

### Requirement: Detect production projects referencing forbidden project targets
The system SHALL allow a project metadata contract to declare `forbidden_project_references`, and SHALL report a violation for each declared `ProjectReference` whose resolved target project matches one of those forbidden entries.

#### Scenario: Forbidden project reference is reported
- **WHEN** a production project metadata contract forbids references to `tests/**/*.csproj` and a matching project declares a `ProjectReference` to `tests/MyApp.Tests/MyApp.Tests.csproj`
- **THEN** strict validation SHALL report a violation identifying the source project and forbidden referenced project

#### Scenario: Allowed project reference passes
- **WHEN** a matching project declares a `ProjectReference` that does not match any forbidden entry
- **THEN** no project-reference violation SHALL be reported for that reference

### Requirement: Support audit project metadata contracts
The system SHALL allow `contracts.audit_project_metadata` entries to report metadata, friend-assembly, and forbidden-project-reference violations without causing strict validation to fail.

#### Scenario: Audit-only metadata violation is reported without failing strict validation
- **WHEN** a policy contains only an `audit_project_metadata` contract that finds a metadata mismatch
- **THEN** audit validation SHALL report the mismatch and strict validation SHALL still pass

### Requirement: Emit deterministic project metadata diagnostics
Project metadata violations SHALL identify the contract name, optional contract ID, discovered project, violation kind, evidence subject, expected rule, actual value when known, and source file path when available, ordered deterministically by project path and then evidence subject.

#### Scenario: Metadata property diagnostic includes expected and actual values
- **WHEN** a required property mismatch is reported
- **THEN** the diagnostic SHALL include the project path, property key, expected value, actual value, and the `.csproj` or imported props file that supplied the actual value when known

#### Scenario: Friend assembly diagnostic includes assembly name
- **WHEN** an undeclared `InternalsVisibleTo` value is reported
- **THEN** the diagnostic SHALL include the project path and the friend assembly name as deterministic evidence
