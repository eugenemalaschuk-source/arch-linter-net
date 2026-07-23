# framework-reference-allow-only-contracts Specification

## Purpose
Evaluates strict and audit framework-reference allow-only contracts that restrict a named source project/assembly to only declaring `FrameworkReference`s matching an explicitly allowed set of declared framework groups, sharing the same real per-target-framework MSBuild evaluation, explicit/implicit classification, and fail-closed evaluation behavior as `framework-reference-contracts`.
## Requirements
### Requirement: Evaluate allow-only framework-reference contracts
The system SHALL allow `contracts.strict_framework_allow_only`/`audit_framework_allow_only` entries to restrict a named source project/assembly to only declaring `FrameworkReference`s that match framework groups listed in the contract's `allowed` list.

#### Scenario: All framework references allowed
- **WHEN** every `FrameworkReference` declared by the source project matches at least one framework group listed in `allowed`
- **THEN** the contract returns an empty violation list

#### Scenario: Framework reference outside allowed groups
- **WHEN** the source project declares a `FrameworkReference` that does not match any framework group listed in `allowed`
- **THEN** a violation is returned identifying the source, the disallowed framework name, and the contract name/id

### Requirement: Framework allow-only violations are deterministic and sorted
The system SHALL report disallowed framework references for a source project sorted by framework name (ordinal), with duplicates removed.

#### Scenario: Deterministic ordering across multiple disallowed frameworks
- **WHEN** a source project declares two disallowed framework references `Microsoft.WindowsDesktop.App` and `Microsoft.AspNetCore.App`
- **THEN** the violation's forbidden references list `Microsoft.AspNetCore.App` before `Microsoft.WindowsDesktop.App`

### Requirement: Framework allow-only contract accepts optional id and ignored_violations
A framework allow-only contract SHALL accept an optional `id` field and an `ignored_violations` list using the `source_type`/`forbidden_reference`/`reason` shape, matched against the source project identifier and the disallowed framework name.

#### Scenario: Ignored disallowed framework suppressed
- **WHEN** a `strict_framework_allow_only` contract has an `ignored_violations` entry matching the source and a disallowed framework name that would otherwise violate the contract
- **THEN** no violation is reported for that framework reference

### Requirement: Strict mode fails validation, audit mode reports only
`strict_framework_allow_only` violations SHALL cause strict-mode validation to fail. `audit_framework_allow_only` violations SHALL be reported without failing strict-mode validation.

#### Scenario: Strict violation fails validation
- **WHEN** a `strict_framework_allow_only` contract has a disallowed-framework violation
- **THEN** strict-mode validation reports failure

#### Scenario: Audit violation does not fail strict validation
- **WHEN** an `audit_framework_allow_only` contract has a disallowed-framework violation
- **THEN** the violation is reported but strict-mode validation does not fail because of it

### Requirement: Framework allow-only source must resolve to a declared target assembly
The system SHALL reject, at policy load time, any `strict_framework_allow_only`/`audit_framework_allow_only` contract whose `source` is not present in `analysis.target_assemblies`, with a diagnostic identifying the contract and the unresolvable source name.

#### Scenario: Unresolvable source rejected at load time
- **WHEN** a `framework_allow_only` contract's `source` value does not appear in `analysis.target_assemblies`
- **THEN** policy loading fails with an error identifying the contract and the unresolvable source name

### Requirement: Framework allow-only contract does not use dependency_depth
A framework allow-only contract SHALL NOT accept a `dependency_depth` field; framework-reference governance has no transitive-dependency concept.

#### Scenario: dependency_depth is rejected by schema
- **WHEN** a `strict_framework_allow_only`/`audit_framework_allow_only` contract in a policy document includes a `dependency_depth` field
- **THEN** schema validation SHALL reject the document

### Requirement: Framework allow-only contracts and framework dependency contracts are independent
Adding `framework_allow_only` contracts SHALL NOT change the behavior of existing `framework_dependency`, `package_dependency`, `package_allow_only`, or `external_dependencies` contracts.

#### Scenario: Existing framework dependency behavior unchanged
- **WHEN** a policy defines both `framework_dependency` and `framework_allow_only` contracts for different source projects
- **THEN** each contract family evaluates independently and neither changes the other's violations

### Requirement: Framework allow-only contracts share configuration validation with framework dependency contracts
Unknown/unusable framework groups referenced by a `framework_allow_only` contract's `allowed` list, a `framework_allow_only` contract's `source` not resolving to any discovered project, and its source project's MSBuild evaluation failing for the whole project or any configured target framework, SHALL be reported by the same `CheckConfiguration` checks described in the framework-reference-contracts specification's "Unknown or unusable framework groups are reported as configuration violations", "Framework-reference dependency/allow-only contracts require discoverable project metadata for their source", and "Framework-reference evaluation fails closed when MSBuild evaluation cannot succeed" requirements.

#### Scenario: Unknown allowed group reported as configuration violation
- **WHEN** a `framework_allow_only` contract's `allowed` list references a framework group name not declared in `framework_references`
- **THEN** `CheckConfiguration` SHALL report a violation identifying that group name as an unknown framework group

#### Scenario: Uninstalled or invalid target framework fails closed
- **WHEN** a `framework_allow_only` contract's source project declares a target framework that cannot be built by the installed SDK
- **THEN** `CheckConfiguration` SHALL report a violation naming the contract, the source project, and the target framework that failed to evaluate, and the contract's own check SHALL NOT report a false-clean result on the basis of unevaluated data

### Requirement: Framework allow-only declarations are discovered through real per-target-framework MSBuild evaluation
The system SHALL resolve a `framework_allow_only` contract's source project's `FrameworkReference` declarations using the same real, per-target-framework MSBuild design-time build (via Buildalyzer, with the `Configuration` global property set to `analysis.configuration`) as `framework_dependency` contracts, including `Condition` on both the `FrameworkReference` item and its containing `ItemGroup` (including conditions depending on `$(Configuration)`), declarations from imported `.props`/`.targets`, and explicit-vs-implicit classification via `IsImplicitlyDefined` metadata.

#### Scenario: Allow-only evaluation honors per-target-framework conditions
- **WHEN** a multi-targeted project's `FrameworkReference` for a disallowed framework is conditioned to only one of its target frameworks
- **THEN** the `framework_allow_only` contract SHALL report a violation only for that target framework, not for target frameworks where the condition does not apply

#### Scenario: Allow-only violation identity distinguishes build configuration
- **WHEN** a disallowed `FrameworkReference` is conditioned to only one of `Debug`/`Release` `Configuration`, for the same target framework
- **THEN** the `framework_allow_only` violation's identity SHALL include that `Configuration`, distinct from what an occurrence under the other `Configuration` would produce

### Requirement: Framework allow-only diagnostics use a distinct typed diagnostic kind

`framework_allow_only` violations SHALL be represented by a `FrameworkReferenceAllowOnlyDiagnostic` with a dedicated `ArchitectureDiagnosticKind`, distinct from `FrameworkReferenceDiagnostic` (deny-list framework contracts) and from `PackageAllowOnlyDiagnostic`.

#### Scenario: Allow-only violation has its own diagnostic kind
- **WHEN** a `strict_framework_allow_only`/`audit_framework_allow_only` contract produces a violation
- **THEN** the resulting diagnostic's `Kind` is the dedicated framework allow-only diagnostic kind, distinct from package and framework-dependency diagnostic kinds

#### Scenario: Allow-only diagnostic reports a distinct logical-location kind in SARIF
- **WHEN** a `framework_allow_only` violation is rendered as a SARIF result
- **THEN** the result's logical location `kind` identifies it as a framework-reference finding, distinct from `"package"`, `"namespace"`, or `"type"`

### Requirement: Framework allow-only diagnostics render equivalent evidence in human, JSON, SARIF, and Testing API output

Every `framework_allow_only` violation SHALL render the same source project, disallowed-framework, target framework, explicit/implicit classification, and declaring project path evidence in human text, unified JSON, SARIF, and the `ArchLinterNet.Testing` API, with no adapter falling back to an empty or generic value for a field the underlying violation carries.

#### Scenario: Unified JSON shows allowed framework groups and structured evidence
- **WHEN** a `framework_allow_only` violation is serialized to unified JSON
- **THEN** the JSON object includes a field listing the contract's configured `allowed` framework group names, alongside non-empty source and forbidden-reference fields, and an `evidence` array with per-reference `framework_name`, `target_framework`, `explicit`, and `source_path` fields

