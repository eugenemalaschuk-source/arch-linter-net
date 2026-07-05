# assembly-allow-only-contracts Specification

## Purpose
Evaluates strict and audit directional assembly allow-only contracts that restrict a named source .NET assembly to only directly referencing an explicitly allowed set of declared assemblies, complementing namespace/layer allow-only contracts with a compiled-assembly-level directional check.
## Requirements
### Requirement: Evaluate directional allow-only assembly references
The system SHALL verify that a named source assembly in a `strict_assembly_allow_only`/`audit_assembly_allow_only` contract only directly references assemblies listed in the contract's `allowed` list (plus itself).

#### Scenario: All references allowed
- **WHEN** the source assembly's direct references are all in the `allowed` list (or are the source itself)
- **THEN** the contract returns an empty violation list

#### Scenario: Reference outside allowed assemblies
- **WHEN** the source assembly directly references a declared target assembly not in `allowed`
- **THEN** a violation is returned identifying the source assembly, the disallowed assembly, and the contract name/id

### Requirement: Assembly allow-only ignores non-declared assemblies
The system SHALL exclude direct references to assemblies that are not present in `analysis.target_assemblies` from allow-only violation checks.

#### Scenario: Reference to undeclared assembly
- **WHEN** the source assembly directly references an assembly that is not listed in `analysis.target_assemblies`
- **THEN** no violation is reported for that reference

### Requirement: Assembly allow-only detects direct references only
The system SHALL detect only direct assembly-to-assembly references (via the source assembly's referenced-assembly metadata); it SHALL NOT resolve transitive reference paths.

#### Scenario: Transitive reference not flagged
- **WHEN** source assembly A directly references allowed assembly B, and B directly references declared assembly C which is not in `allowed`, but A does not directly reference C
- **THEN** no violation is reported for the A/C pair

### Requirement: Violations are deterministic and sorted
The system SHALL report disallowed references for a source assembly sorted by assembly simple name, with duplicates removed.

#### Scenario: Deterministic ordering across multiple disallowed references
- **WHEN** a source assembly directly references two declared, disallowed assemblies `Z` and `A`
- **THEN** the violation's forbidden references list `A` before `Z`

### Requirement: Assembly allow-only contract accepts optional id
An assembly allow-only contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** an assembly allow-only contract with `id: app-allowed-refs` produces a violation
- **THEN** the violation SHALL have `ContractId == "app-allowed-refs"`

#### Scenario: Violation without explicit ID
- **WHEN** an assembly allow-only contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`

### Requirement: Assembly allow-only contracts support per-pair ignores
An assembly allow-only contract SHALL accept an `ignored_violations` list using the same `source_type`/`forbidden_reference`/`reason` shape as other contract families, where `source_type` and `forbidden_reference` are matched against assembly simple names.

#### Scenario: Ignored pair suppressed
- **WHEN** an assembly allow-only contract has an `ignored_violations` entry matching the source/disallowed assembly pair that would otherwise violate the contract
- **THEN** no violation is reported for that pair

### Requirement: Strict mode fails validation, audit mode reports only
`strict_assembly_allow_only` violations SHALL cause strict-mode validation to fail. `audit_assembly_allow_only` violations SHALL be reported without failing strict-mode validation.

#### Scenario: Strict violation fails validation
- **WHEN** a `strict_assembly_allow_only` contract has a disallowed-reference violation
- **THEN** strict-mode validation reports failure

#### Scenario: Audit violation does not fail strict validation
- **WHEN** an `audit_assembly_allow_only` contract has a disallowed-reference violation
- **THEN** the violation is reported but strict-mode validation does not fail because of it

### Requirement: Every assembly referenced must be resolvable
The system SHALL reject, at policy load time, any `strict_assembly_allow_only`/`audit_assembly_allow_only` contract whose `source` or any `allowed` entry is not present in `analysis.target_assemblies`, with a diagnostic identifying the contract and the unresolvable assembly name.

#### Scenario: Unresolvable source assembly rejected at load time
- **WHEN** a contract's `source` value does not appear in `analysis.target_assemblies`
- **THEN** policy loading fails with an error identifying the contract and the unresolvable assembly name

#### Scenario: Unresolvable allowed assembly rejected at load time
- **WHEN** a contract's `allowed` list contains an assembly name that does not appear in `analysis.target_assemblies`
- **THEN** policy loading fails with an error identifying the contract and the unresolvable assembly name

### Requirement: Namespace/layer allow-only contracts and other assembly-axis contracts remain unaffected
Adding the assembly allow-only contract family SHALL NOT change the behavior of existing `strict_allow_only`/`audit_allow_only` namespace/layer contracts, `strict_assembly_independence`/`audit_assembly_independence` contracts, `strict_assembly_dependency`/`audit_assembly_dependency` contracts, or Unity `strict_asmdef`/`audit_asmdef` contracts.

#### Scenario: Existing allow-only and assembly independence behavior unchanged
- **WHEN** a policy defines namespace/layer allow-only contracts, assembly independence contracts, and assembly allow-only contracts together
- **THEN** the namespace/layer allow-only contracts and assembly independence contracts evaluate exactly as they did before this change was introduced

### Requirement: Assembly allow-only contract accepts an optional dependency_depth field
An assembly allow-only contract SHALL accept an optional `dependency_depth` field, using the same field name as namespace-level dependency contracts, defaulting to `direct`. Only `direct` is currently supported for this family; `transitive` is a recognized value name shared with the namespace-level field but is not yet a valid value here (see the transitive-rejection requirement below).

#### Scenario: Default direct mode
- **WHEN** an assembly allow-only contract has no `dependency_depth` field
- **THEN** the contract behaves as `dependency_depth: direct`

#### Scenario: Explicit direct mode loads successfully
- **WHEN** an assembly allow-only contract declares `dependency_depth: direct`
- **THEN** policy loading succeeds and the contract is evaluated as direct-reference-only

### Requirement: Assembly allow-only rejects transitive depth at load time
The system SHALL reject, at policy load time, any `strict_assembly_allow_only`/`audit_assembly_allow_only` contract that declares `dependency_depth: transitive`, with a diagnostic identifying the contract and stating that transitive assembly-reference-path resolution is not supported yet.

#### Scenario: Transitive depth rejected at load time
- **WHEN** an assembly allow-only contract declares `dependency_depth: transitive`
- **THEN** policy loading fails with an actionable error identifying the contract and stating that only `direct` is currently supported

### Requirement: Assembly allow-only rejects transitive depth defensively at check time
The system SHALL reject `dependency_depth: transitive` on an `ArchitectureAssemblyAllowOnlyContract` when the contract is evaluated, even if the contract was constructed programmatically rather than loaded from YAML, with the same actionable error used at policy load time.

#### Scenario: Programmatically constructed transitive contract rejected at check time
- **WHEN** an `ArchitectureAssemblyAllowOnlyContract` with `DependencyDepth` set to `Transitive` is passed directly to the session check method (bypassing the policy loader)
- **THEN** the check throws an actionable error stating that only `direct` is currently supported

