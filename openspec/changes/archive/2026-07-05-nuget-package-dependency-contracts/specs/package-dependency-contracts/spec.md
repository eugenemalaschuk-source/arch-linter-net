## ADDED Requirements

### Requirement: Declare named package groups
The system SHALL allow policies to declare named package groups in a top-level `packages` section. Each group SHALL support `package_ids` (exact NuGet package ID matches) and `package_prefixes` (dot-segment prefix matches) lists.

#### Scenario: Policy declares a package group by exact ID
- **WHEN** a policy declares `packages.forbidden_infra.package_ids` with `Newtonsoft.Json`
- **THEN** the policy loader SHALL expose a package group named `forbidden_infra` with `Newtonsoft.Json` as an exact package ID

#### Scenario: Policy declares a package group by prefix
- **WHEN** a policy declares `packages.forbidden_infra.package_prefixes` with `Microsoft.EntityFrameworkCore`
- **THEN** the policy loader SHALL expose `Microsoft.EntityFrameworkCore` as a package prefix for the `forbidden_infra` group

### Requirement: Match package references by exact package ID
The system SHALL match a project's `PackageReference` against a package group when the package ID equals a configured `package_ids` entry, case-insensitively (NuGet package IDs are case-insensitive).

#### Scenario: Exact ID match
- **WHEN** a package group has `package_ids: [Newtonsoft.Json]` and a project references `Newtonsoft.Json`
- **THEN** the reference SHALL match that package group

#### Scenario: Exact ID match is case-insensitive
- **WHEN** a package group has `package_ids: [Newtonsoft.Json]` and a project references `newtonsoft.json`
- **THEN** the reference SHALL match that package group

### Requirement: Match package references by dot-segment prefix
The system SHALL match a project's `PackageReference` against a package group when the package ID equals a configured `package_prefixes` entry or is a dot-segment child of that prefix, case-insensitively.

#### Scenario: Prefix exact match
- **WHEN** a package group has `package_prefixes: [Microsoft.EntityFrameworkCore]` and a project references `Microsoft.EntityFrameworkCore`
- **THEN** the reference SHALL match that package group

#### Scenario: Prefix child match
- **WHEN** a package group has `package_prefixes: [Microsoft.EntityFrameworkCore]` and a project references `Microsoft.EntityFrameworkCore.SqlServer`
- **THEN** the reference SHALL match that package group

#### Scenario: Prefix sibling does not match
- **WHEN** a package group has `package_prefixes: [Microsoft.EntityFrameworkCore]` and a project references `Microsoft.EntityFrameworkCoreTools.Widget`
- **THEN** the reference SHALL NOT match that package group

### Requirement: Evaluate strict package dependency contracts
The system SHALL allow `contracts.strict_package_dependency` entries to forbid a named source project/assembly from declaring a `PackageReference` matching one or more declared package groups.

#### Scenario: Strict package violation found
- **WHEN** the source project in a `strict_package_dependency` contract declares a `PackageReference` matching a forbidden package group
- **THEN** strict validation SHALL return an architecture violation identifying the source, the forbidden package group, and the matched package ID

#### Scenario: Strict package contract passes
- **WHEN** the source project in a `strict_package_dependency` contract declares no `PackageReference` matching any forbidden package group
- **THEN** strict validation SHALL return no violations for that contract

### Requirement: Evaluate audit package dependency contracts
The system SHALL allow `contracts.audit_package_dependency` entries to report forbidden package references without affecting strict validation.

#### Scenario: Audit package violation reported without failing strict
- **WHEN** an `audit_package_dependency` contract's source project declares a `PackageReference` matching a forbidden package group
- **THEN** audit validation SHALL report a violation and strict-mode validation SHALL NOT fail because of it

### Requirement: Package dependency violation identifies package ID and version
Package dependency violations SHALL identify the contract name, optional contract ID, source project, matched forbidden package group, and each matched `PackageReference` as a package ID with its resolved version when known.

#### Scenario: Violation includes resolved version
- **WHEN** `MyApp.Domain` references `Microsoft.EntityFrameworkCore` version `8.0.0` through a `domain-no-ef` contract forbidding the `forbidden_infra` group
- **THEN** the violation SHALL identify `MyApp.Domain` as the source, `forbidden_infra` as the forbidden package group, and evidence containing `Microsoft.EntityFrameworkCore@8.0.0`

#### Scenario: Violation without a resolvable version
- **WHEN** a matched `PackageReference` has no `Version` attribute/element and no matching entry is found in `Directory.Packages.props`
- **THEN** the violation's evidence SHALL identify the package ID alone, without a version suffix

### Requirement: Package dependency contract accepts optional id and ignored_violations
A package dependency contract SHALL accept an optional `id` field (with the same name-derived fallback used by other contract families) and an `ignored_violations` list using the `source_type`/`forbidden_reference`/`reason` shape, matched against the source project identifier and the forbidden package ID.

#### Scenario: Ignored package reference suppressed
- **WHEN** a `strict_package_dependency` contract has an `ignored_violations` entry matching the source project and a forbidden package ID that would otherwise violate the contract
- **THEN** no violation is reported for that package reference

### Requirement: Package dependency source must resolve to a declared target assembly
The system SHALL reject, at policy load time, any `strict_package_dependency`/`audit_package_dependency` contract whose `source` is not present in `analysis.target_assemblies`, with a diagnostic identifying the contract and the unresolvable source name.

#### Scenario: Unresolvable source rejected at load time
- **WHEN** a `package_dependency` contract's `source` value does not appear in `analysis.target_assemblies`
- **THEN** policy loading fails with an error identifying the contract and the unresolvable source name

### Requirement: Package dependency contracts do not support transitive depth
The system SHALL reject `dependency_depth: transitive` on a package dependency contract, both at policy load time and defensively when the contract is evaluated directly, with an actionable error stating that only `direct` package-reference checking is supported.

#### Scenario: Transitive depth rejected at load time
- **WHEN** a package dependency contract declares `dependency_depth: transitive`
- **THEN** policy loading fails with an actionable error identifying the contract and stating that only `direct` is currently supported

#### Scenario: Transitive depth rejected defensively at check time
- **WHEN** a package dependency contract with `DependencyDepth` set to `Transitive` is passed directly to the session check method, bypassing the policy loader
- **THEN** the check throws an actionable error stating that only `direct` is currently supported

### Requirement: Package dependency diagnostics are distinct from external dependency diagnostics
Package dependency violations SHALL be reported using a diagnostic kind distinct from `ExternalDependency` diagnostics, so declared-package-reference violations are never confused with observed-type-reference violations in reporting output.

#### Scenario: Package and external dependency diagnostics are separately identifiable
- **WHEN** a policy produces both a `package_dependency` violation and an `external_dependency` violation in the same validation run
- **THEN** each diagnostic's `Kind` SHALL distinguish package-reference violations from external type-reference violations

### Requirement: Package dependency contracts are independent of external dependency contracts
Adding `packages`/`package_dependency` contracts SHALL NOT change the behavior of existing `external_dependencies`/`strict_external`/`audit_external` contracts, and vice versa.

#### Scenario: Existing external dependency behavior unchanged
- **WHEN** a policy defines both `external_dependencies` groups with `strict_external` contracts and `packages` groups with `strict_package_dependency` contracts
- **THEN** the `strict_external` contracts evaluate exactly as they did before this change was introduced
