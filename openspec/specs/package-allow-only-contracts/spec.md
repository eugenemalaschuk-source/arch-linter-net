# package-allow-only-contracts Specification

## Purpose
Evaluates strict and audit package allow-only contracts that restrict a named source project/assembly to only declaring `PackageReference`s that match an explicitly allowed set of declared NuGet package groups, complementing package dependency contracts with an allow-list rather than a deny-list model.
## Requirements
### Requirement: Evaluate allow-only package reference contracts
The system SHALL allow `contracts.strict_package_allow_only`/`audit_package_allow_only` entries to restrict a named source project/assembly to only declaring `PackageReference`s that match package groups listed in the contract's `allowed` list.

#### Scenario: All package references allowed
- **WHEN** every `PackageReference` declared by the source project matches at least one package group listed in `allowed`
- **THEN** the contract returns an empty violation list

#### Scenario: Package reference outside allowed groups
- **WHEN** the source project declares a `PackageReference` that does not match any package group listed in `allowed`
- **THEN** a violation is returned identifying the source, the disallowed package ID, and the contract name/id

### Requirement: Package allow-only violations are deterministic and sorted
The system SHALL report disallowed package references for a source project sorted by package ID (ordinal), with duplicates removed, each including its resolved version when known.

#### Scenario: Deterministic ordering across multiple disallowed packages
- **WHEN** a source project declares two disallowed package references `Zebra.Sdk` and `Acme.Sdk`
- **THEN** the violation's forbidden references list `Acme.Sdk` before `Zebra.Sdk`

### Requirement: Package allow-only contract accepts optional id and ignored_violations
A package allow-only contract SHALL accept an optional `id` field and an `ignored_violations` list using the `source_type`/`forbidden_reference`/`reason` shape, matched against the source project identifier and the disallowed package ID.

#### Scenario: Ignored disallowed package suppressed
- **WHEN** a `strict_package_allow_only` contract has an `ignored_violations` entry matching the source and a disallowed package ID that would otherwise violate the contract
- **THEN** no violation is reported for that package reference

### Requirement: Strict mode fails validation, audit mode reports only
`strict_package_allow_only` violations SHALL cause strict-mode validation to fail. `audit_package_allow_only` violations SHALL be reported without failing strict-mode validation.

#### Scenario: Strict violation fails validation
- **WHEN** a `strict_package_allow_only` contract has a disallowed-package violation
- **THEN** strict-mode validation reports failure

#### Scenario: Audit violation does not fail strict validation
- **WHEN** an `audit_package_allow_only` contract has a disallowed-package violation
- **THEN** the violation is reported but strict-mode validation does not fail because of it

### Requirement: Package allow-only source must resolve to a declared target assembly
The system SHALL reject, at policy load time, any `strict_package_allow_only`/`audit_package_allow_only` contract whose `source` is not present in `analysis.target_assemblies`, with a diagnostic identifying the contract and the unresolvable source name.

#### Scenario: Unresolvable source rejected at load time
- **WHEN** a `package_allow_only` contract's `source` value does not appear in `analysis.target_assemblies`
- **THEN** policy loading fails with an error identifying the contract and the unresolvable source name

### Requirement: Package allow-only contracts do not support transitive depth
The system SHALL reject `dependency_depth: transitive` on a package allow-only contract, both at policy load time and defensively when the contract is evaluated directly, with an actionable error stating that only `direct` package-reference checking is supported.

#### Scenario: Transitive depth rejected at load time
- **WHEN** a package allow-only contract declares `dependency_depth: transitive`
- **THEN** policy loading fails with an actionable error identifying the contract and stating that only `direct` is currently supported

#### Scenario: Transitive depth rejected defensively at check time
- **WHEN** a package allow-only contract with `DependencyDepth` set to `Transitive` is passed directly to the session check method, bypassing the policy loader
- **THEN** the check throws an actionable error stating that only `direct` is currently supported

### Requirement: Package allow-only contracts and package dependency contracts are independent
Adding `package_allow_only` contracts SHALL NOT change the behavior of existing `package_dependency`, `assembly_dependency`, `assembly_allow_only`, or `external_dependencies` contracts.

#### Scenario: Existing package dependency behavior unchanged
- **WHEN** a policy defines both `package_dependency` and `package_allow_only` contracts for different source projects
- **THEN** each contract family evaluates independently and neither changes the other's violations

### Requirement: Package allow-only contracts share configuration validation with package dependency contracts
Unknown/unusable package groups referenced by a `package_allow_only` contract's `allowed` list, and a `package_allow_only` contract's `source` not resolving to any discovered project's package metadata, SHALL be reported by the same `CheckConfiguration` checks described in the package-dependency-contracts specification's "Unknown or unusable package groups are reported as configuration violations" and "Package dependency/allow-only contracts require discoverable package metadata for their source" requirements.

#### Scenario: Unknown allowed group reported as configuration violation
- **WHEN** a `package_allow_only` contract's `allowed` list references a package group name not declared in `packages`
- **THEN** `CheckConfiguration` SHALL report a violation identifying that group name as an unknown package group

