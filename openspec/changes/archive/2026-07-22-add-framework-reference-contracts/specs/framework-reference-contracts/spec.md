## ADDED Requirements

### Requirement: Declare named framework-reference groups
The system SHALL allow policies to declare named framework-reference groups in a top-level `framework_references` section. Each group SHALL support `framework_names` (exact `FrameworkReference` `Include` matches) and `framework_name_prefixes` (dot-segment prefix matches) lists.

#### Scenario: Policy declares a framework group by exact name
- **WHEN** a policy declares `framework_references.forbidden_web.framework_names` with `Microsoft.AspNetCore.App`
- **THEN** the policy loader SHALL expose a framework group named `forbidden_web` with `Microsoft.AspNetCore.App` as an exact framework name

#### Scenario: Policy declares a framework group by prefix
- **WHEN** a policy declares `framework_references.forbidden_web.framework_name_prefixes` with `Microsoft.AspNetCore`
- **THEN** the policy loader SHALL expose `Microsoft.AspNetCore` as a framework name prefix for the `forbidden_web` group

### Requirement: Match framework references by exact name
The system SHALL match a project's `FrameworkReference` against a framework group when its `Include` name equals a configured `framework_names` entry, case-insensitively.

#### Scenario: Exact name match
- **WHEN** a framework group has `framework_names: [Microsoft.AspNetCore.App]` and a project declares `FrameworkReference Include="Microsoft.AspNetCore.App"`
- **THEN** the reference SHALL match that framework group

#### Scenario: Exact name match is case-insensitive
- **WHEN** a framework group has `framework_names: [Microsoft.AspNetCore.App]` and a project declares `FrameworkReference Include="microsoft.aspnetcore.app"`
- **THEN** the reference SHALL match that framework group

### Requirement: Match framework references by dot-segment prefix
The system SHALL match a project's `FrameworkReference` against a framework group when its `Include` name equals a configured `framework_name_prefixes` entry or is a dot-segment child of that prefix, case-insensitively.

#### Scenario: Prefix exact match
- **WHEN** a framework group has `framework_name_prefixes: [Microsoft.AspNetCore]` and a project declares `FrameworkReference Include="Microsoft.AspNetCore"`
- **THEN** the reference SHALL match that framework group

#### Scenario: Prefix child match
- **WHEN** a framework group has `framework_name_prefixes: [Microsoft.AspNetCore]` and a project declares `FrameworkReference Include="Microsoft.AspNetCore.App"`
- **THEN** the reference SHALL match that framework group

#### Scenario: Prefix sibling does not match
- **WHEN** a framework group has `framework_name_prefixes: [Microsoft.AspNetCore]` and a project declares `FrameworkReference Include="Microsoft.AspNetCoreTools.Widget"`
- **THEN** the reference SHALL NOT match that framework group

### Requirement: Evaluate strict framework-reference dependency contracts
The system SHALL allow `contracts.strict_framework_dependency` entries to forbid a named source project/assembly from declaring a `FrameworkReference` matching one or more declared framework groups.

#### Scenario: Strict framework violation found
- **WHEN** the source project in a `strict_framework_dependency` contract declares a `FrameworkReference` matching a forbidden framework group
- **THEN** strict validation SHALL return an architecture violation identifying the source, the forbidden framework group, and the matched framework name

#### Scenario: Strict framework contract passes
- **WHEN** the source project in a `strict_framework_dependency` contract declares no `FrameworkReference` matching any forbidden framework group
- **THEN** strict validation SHALL return no violations for that contract

### Requirement: Evaluate audit framework-reference dependency contracts
The system SHALL allow `contracts.audit_framework_dependency` entries to report forbidden framework references without affecting strict validation.

#### Scenario: Audit framework violation reported without failing strict
- **WHEN** an `audit_framework_dependency` contract's source project declares a `FrameworkReference` matching a forbidden framework group
- **THEN** audit validation SHALL report a violation and strict-mode validation SHALL NOT fail because of it

### Requirement: Framework-reference violations preserve condition and project occurrence identity
Framework-reference violations SHALL identify the contract name, optional contract ID, source project, matched forbidden framework group, the matched `FrameworkReference` name, and the MSBuild `Condition` value (when present) under which the reference was declared, so that the same framework reference declared under two different conditions in the same project, or in two different projects, produces distinct violation identities.

#### Scenario: Violation includes condition context
- **WHEN** `MyApp.Api` declares `FrameworkReference Include="Microsoft.AspNetCore.App"` under `Condition="'$(TargetFramework)'=='net10.0'"` and a `strict_framework_dependency` contract forbids that framework
- **THEN** the violation's evidence SHALL identify `MyApp.Api` as the source and include the declared condition alongside the matched framework name

#### Scenario: Same framework reference in two projects yields distinct identities
- **WHEN** both `MyApp.Api` and `MyApp.Worker` declare `FrameworkReference Include="Microsoft.AspNetCore.App"` and both are covered by forbidding contracts
- **THEN** the two resulting violations SHALL have distinct violation/baseline identities differing by source project

#### Scenario: Same project, two conditions, yields distinct identities
- **WHEN** `MyApp.Api` declares `FrameworkReference Include="Microsoft.AspNetCore.App"` twice under two different `Condition` values, both matching a forbidding contract
- **THEN** the two resulting violations SHALL have distinct violation/baseline identities differing by condition

### Requirement: Framework-reference contract accepts optional id and ignored_violations
A framework-reference dependency contract SHALL accept an optional `id` field (with the same name-derived fallback used by other contract families) and an `ignored_violations` list using the `source_type`/`forbidden_reference`/`reason` shape, matched against the source project identifier and the forbidden framework name.

#### Scenario: Ignored framework reference suppressed
- **WHEN** a `strict_framework_dependency` contract has an `ignored_violations` entry matching the source project and a forbidden framework name that would otherwise violate the contract
- **THEN** no violation is reported for that framework reference

### Requirement: Framework-reference contract does not use dependency_depth
A framework-reference dependency contract SHALL NOT accept a `dependency_depth` field; framework-reference governance has no transitive-dependency concept.

#### Scenario: dependency_depth is rejected by schema
- **WHEN** a `strict_framework_dependency`/`audit_framework_dependency` contract in a policy document includes a `dependency_depth` field
- **THEN** schema validation SHALL reject the document

### Requirement: Framework-reference dependency source must resolve to a declared target assembly
The system SHALL reject, at policy load time, any `strict_framework_dependency`/`audit_framework_dependency` contract whose `source` is not present in `analysis.target_assemblies`, with a diagnostic identifying the contract and the unresolvable source name.

#### Scenario: Unresolvable source rejected at load time
- **WHEN** a `framework_dependency` contract's `source` value does not appear in `analysis.target_assemblies`
- **THEN** policy loading fails with an error identifying the contract and the unresolvable source name

### Requirement: Framework-reference diagnostics are distinct from package diagnostics
Framework-reference dependency violations SHALL be reported using a diagnostic kind distinct from `PackageDependency` and `ExternalDependency` diagnostics.

#### Scenario: Framework and package diagnostics are separately identifiable
- **WHEN** a policy produces both a `framework_dependency` violation and a `package_dependency` violation in the same validation run
- **THEN** each diagnostic's `Kind` SHALL distinguish framework-reference violations from package-reference violations

### Requirement: Unknown or unusable framework groups are reported as configuration violations
The system SHALL detect, during `CheckConfiguration`, any framework group name referenced by a `strict_framework_dependency`/`audit_framework_dependency` `forbidden` list (or a `strict_framework_allow_only`/`audit_framework_allow_only` `allowed` list) that either is not declared in `framework_references` or is declared but has no non-empty `framework_names`/`framework_name_prefixes` matcher, and SHALL report a `<configuration>` violation for each such group instead of allowing the contract to silently match nothing.

#### Scenario: Undeclared framework group referenced by a contract
- **WHEN** a `framework_dependency`/`framework_allow_only` contract references a framework group name that is not a key in `framework_references`
- **THEN** `CheckConfiguration` SHALL report a violation identifying that group name as an unknown framework group

#### Scenario: Declared framework group with no usable matchers
- **WHEN** a `framework_dependency`/`framework_allow_only` contract references a framework group declared in `framework_references` with empty (or all-blank) `framework_names` and `framework_name_prefixes`
- **THEN** `CheckConfiguration` SHALL report a violation identifying that group name as an invalid framework group

### Requirement: Framework-reference dependency/allow-only contracts require discoverable project metadata for their source
The system SHALL detect, during `CheckConfiguration`, any `framework_dependency`/`framework_allow_only` contract whose `source` does not correspond to any project in `Context.ProjectDiscovery`'s discovered projects (including when project discovery did not run at all), and SHALL report a `<configuration>` violation identifying the contract and its source, rather than allowing the contract to silently evaluate as passing with no visible signal that no project metadata was available.

#### Scenario: No project discovery configured
- **WHEN** a policy declares a `framework_dependency`/`framework_allow_only` contract but `analysis.solution` and `analysis.projects` are both unset, so project discovery never runs
- **THEN** `CheckConfiguration` SHALL report a violation naming the contract and its source, stating that no project metadata was discovered

#### Scenario: Source assembly resolves to a discovered project
- **WHEN** project discovery discovers a project whose assembly name matches a `framework_dependency`/`framework_allow_only` contract's `source`
- **THEN** `CheckConfiguration` SHALL NOT report a missing-metadata violation for that contract

### Requirement: Framework-reference contracts are independent of package and external dependency contracts
Adding `framework_references`/`framework_dependency` contracts SHALL NOT change the behavior of existing `packages`/`package_dependency` or `external_dependencies`/`strict_external`/`audit_external` contracts, and vice versa.

#### Scenario: Existing package and external dependency behavior unchanged
- **WHEN** a policy defines `packages` groups with `strict_package_dependency` contracts, `external_dependencies` groups with `strict_external` contracts, and `framework_references` groups with `strict_framework_dependency` contracts
- **THEN** the existing package and external dependency contracts evaluate exactly as they did before this change was introduced

### Requirement: Framework-reference diagnostics render equivalent evidence in human, JSON, SARIF, and Testing API output

Every `strict_framework_dependency`/`audit_framework_dependency` violation SHALL render the same source project, forbidden-framework-group display, matched `FrameworkReference` name, and condition evidence in human text output, unified JSON output, SARIF output, and the `ArchLinterNet.Testing` API. No adapter SHALL fall back to an empty or generic value for a field the underlying violation carries.

#### Scenario: Human output shows framework evidence
- **WHEN** a `strict_framework_dependency` contract produces a violation for source `MyApp.Api` against forbidden framework group `forbidden_web` matching `Microsoft.AspNetCore.App`
- **THEN** the human-formatted line identifies `MyApp.Api` as the source and lists `Microsoft.AspNetCore.App` among the forbidden references

#### Scenario: Unified JSON shows framework evidence
- **WHEN** the same violation is serialized to unified JSON
- **THEN** the JSON object's source and forbidden-reference fields are non-empty and match the human-formatted evidence, and the object includes a field naming the matched framework group

#### Scenario: SARIF, human, JSON, and Testing API evidence are equivalent
- **WHEN** the same violation is rendered as human text, unified JSON, SARIF, and through the Testing API's validation result
- **THEN** all four identify the same source project and the same matched framework reference
