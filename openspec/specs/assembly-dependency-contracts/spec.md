# assembly-dependency-contracts Specification

## Purpose
Evaluates strict and audit directional assembly dependency contracts that forbid a named source .NET assembly from directly referencing one or more forbidden assemblies, complementing namespace/layer dependency contracts with a compiled-assembly-level directional check.
## Requirements
### Requirement: Evaluate directional forbidden assembly references
The system SHALL verify that a named source assembly in a `strict_assembly_dependency`/`audit_assembly_dependency` contract does not directly reference any assembly listed in the contract's `forbidden` list.

#### Scenario: No violations found
- **WHEN** the source assembly does not directly reference any assembly in `forbidden`
- **THEN** the contract returns an empty violation list

#### Scenario: Violation found
- **WHEN** the source assembly directly references a forbidden assembly `B`
- **THEN** the contract returns a violation identifying the source assembly, `B`, and the contract name/id

#### Scenario: Multiple forbidden assemblies
- **WHEN** the source assembly directly references two assemblies both listed in `forbidden`
- **THEN** the contract returns a violation for each forbidden assembly directly referenced

### Requirement: Assembly dependency detects direct references only
The system SHALL detect only direct assembly-to-assembly references (via the source assembly's referenced-assembly metadata); it SHALL NOT resolve transitive reference paths.

#### Scenario: Transitive reference not flagged
- **WHEN** source assembly A references assembly B, and B references forbidden assembly C, but A does not directly reference C
- **THEN** no violation is reported for the A/C pair

### Requirement: Assembly dependency contracts ignore self-references
The system SHALL not report a violation when the source assembly appears in its own `forbidden` list.

#### Scenario: Source listed in its own forbidden list
- **WHEN** an assembly dependency contract's `source` value also appears in that contract's `forbidden` list
- **THEN** no violation is reported for the source referencing itself

### Requirement: Violations are ordered by declaration order
The system SHALL report assembly dependency violations in the order the assemblies are declared in the contract's `forbidden` list, not in the order returned by runtime assembly-reference reflection APIs.

#### Scenario: Deterministic ordering across multiple forbidden assemblies
- **WHEN** a contract declares `forbidden: [B, C]` and the source directly references both
- **THEN** the violation for the source/B pair is reported before the violation for the source/C pair

### Requirement: Assembly dependency contract accepts optional id
An assembly dependency contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** an assembly dependency contract with `id: domain-no-infra` produces a violation
- **THEN** the violation SHALL have `ContractId == "domain-no-infra"`

#### Scenario: Violation without explicit ID
- **WHEN** an assembly dependency contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`

### Requirement: Assembly dependency contracts support per-pair ignores
An assembly dependency contract SHALL accept an `ignored_violations` list using the same `source_type`/`forbidden_reference`/`reason` shape as other contract families, where `source_type` and `forbidden_reference` are matched against assembly simple names.

#### Scenario: Ignored pair suppressed
- **WHEN** an assembly dependency contract has an `ignored_violations` entry matching the source/forbidden assembly pair that would otherwise violate the contract
- **THEN** no violation is reported for that pair

### Requirement: Strict mode fails validation, audit mode reports only
`strict_assembly_dependency` violations SHALL cause strict-mode validation to fail. `audit_assembly_dependency` violations SHALL be reported without failing strict-mode validation.

#### Scenario: Strict violation fails validation
- **WHEN** a `strict_assembly_dependency` contract has a direct-reference violation
- **THEN** strict-mode validation reports failure

#### Scenario: Audit violation does not fail strict validation
- **WHEN** an `audit_assembly_dependency` contract has a direct-reference violation
- **THEN** the violation is reported but strict-mode validation does not fail because of it

### Requirement: Every assembly referenced must be resolvable
The system SHALL reject, at policy load time, any `strict_assembly_dependency`/`audit_assembly_dependency` contract whose `source` or any `forbidden` entry is not present in `analysis.target_assemblies`, with a diagnostic identifying the contract and the unresolvable assembly name.

#### Scenario: Unresolvable source assembly rejected at load time
- **WHEN** a contract's `source` value does not appear in `analysis.target_assemblies`
- **THEN** policy loading fails with an error identifying the contract and the unresolvable assembly name

#### Scenario: Unresolvable forbidden assembly rejected at load time
- **WHEN** a contract's `forbidden` list contains an assembly name that does not appear in `analysis.target_assemblies`
- **THEN** policy loading fails with an error identifying the contract and the unresolvable assembly name

### Requirement: Namespace/layer dependency contracts and other assembly-axis contracts remain unaffected
Adding the assembly dependency contract family SHALL NOT change the behavior of existing `strict`/`audit` namespace/layer dependency contracts, `strict_assembly_independence`/`audit_assembly_independence` contracts, or Unity `strict_asmdef`/`audit_asmdef` contracts.

#### Scenario: Existing dependency and assembly independence behavior unchanged
- **WHEN** a policy defines namespace/layer dependency contracts, assembly independence contracts, and assembly dependency contracts together
- **THEN** the namespace/layer dependency contracts and assembly independence contracts evaluate exactly as they did before this change was introduced

### Requirement: Assembly dependency violation evidence is deterministic
The system SHALL report `assembly_dependency` violation evidence as a deterministic `"{Source} -> {Forbidden}"` string identifying the source and forbidden assembly simple names, not a filesystem path.

#### Scenario: Evidence identifies source and forbidden assembly
- **WHEN** an `assembly_dependency` contract with `source: A` and `forbidden: [B]` produces a violation
- **THEN** the violation's evidence collection contains the string `"A -> B"`

### Requirement: Assembly dependency contract accepts an optional dependency_depth field
An assembly dependency contract SHALL accept an optional `dependency_depth` field with the same values as namespace-level dependency contracts (`direct` or `transitive`), defaulting to `direct`.

#### Scenario: Default direct mode
- **WHEN** an assembly dependency contract has no `dependency_depth` field
- **THEN** the contract behaves as `dependency_depth: direct`

#### Scenario: Explicit direct mode loads successfully
- **WHEN** an assembly dependency contract declares `dependency_depth: direct`
- **THEN** policy loading succeeds and the contract is evaluated as direct-reference-only

### Requirement: Assembly dependency rejects transitive depth at load time
The system SHALL reject, at policy load time, any `strict_assembly_dependency`/`audit_assembly_dependency` contract that declares `dependency_depth: transitive`, with a diagnostic identifying the contract and stating that transitive assembly-reference-path resolution is not supported yet.

#### Scenario: Transitive depth rejected at load time
- **WHEN** an assembly dependency contract declares `dependency_depth: transitive`
- **THEN** policy loading fails with an actionable error identifying the contract and stating that only `direct` is currently supported

### Requirement: Assembly dependency rejects transitive depth defensively at check time
The system SHALL reject `dependency_depth: transitive` on an `ArchitectureAssemblyDependencyContract` when the contract is evaluated, even if the contract was constructed programmatically rather than loaded from YAML, with the same actionable error used at policy load time.

#### Scenario: Programmatically constructed transitive contract rejected at check time
- **WHEN** an `ArchitectureAssemblyDependencyContract` with `DependencyDepth` set to `Transitive` is passed directly to the session check method (bypassing the policy loader)
- **THEN** the check throws an actionable error stating that only `direct` is currently supported

