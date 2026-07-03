# assembly-independence-contracts Specification

## Purpose
Evaluates mutual independence across a set of named .NET assemblies based on direct compiled-assembly references, complementing namespace/layer independence with a check that doesn't depend on namespace ownership matching assembly boundaries.

## Requirements
### Requirement: Evaluate mutual independence across named assemblies
The system SHALL verify that for each pair of assemblies in a `strict_assembly_independence`/`audit_assembly_independence` contract, the source assembly does not directly reference the other assembly AND the other assembly does not directly reference the source assembly.

#### Scenario: Independent assemblies
- **WHEN** assemblies `[A, B]` have no direct assembly reference in either direction
- **THEN** the contract returns an empty violation list

#### Scenario: One-directional assembly reference
- **WHEN** assembly A directly references assembly B (but not vice versa)
- **THEN** a violation is returned identifying A as the source and B as the forbidden target

#### Scenario: Bidirectional assembly reference
- **WHEN** assembly A directly references assembly B and assembly B directly references assembly A
- **THEN** violations are returned for both directions

### Requirement: Assembly independence contracts ignore self-references
The system SHALL not report a violation for an assembly appearing paired with itself.

#### Scenario: Same assembly listed once
- **WHEN** an assembly independence contract lists a given assembly name only once in `assemblies`
- **THEN** that assembly is never compared against itself

### Requirement: Assembly independence detects direct references only
The system SHALL detect only direct assembly-to-assembly references (via the source assembly's referenced-assembly metadata); it SHALL NOT resolve transitive reference paths.

#### Scenario: Transitive reference not flagged
- **WHEN** assembly A references assembly B, and assembly B references assembly C, and a contract lists `[A, C]` with no direct reference from A to C
- **THEN** no violation is reported for the A/C pair

### Requirement: Violations are ordered by declaration order
The system SHALL report assembly independence violations in the order the assemblies are declared in the contract's `assemblies` list, not in the order returned by runtime assembly-reference reflection APIs.

#### Scenario: Deterministic ordering across multiple assemblies
- **WHEN** a contract lists `[A, B, C]` and both A→B and A→C are direct violations
- **THEN** the violation for A→B is reported before the violation for A→C

### Requirement: Assembly independence contract accepts optional id
An assembly independence contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** an assembly independence contract with `id: no-cross-talk` produces a violation
- **THEN** the violation SHALL have `ContractId == "no-cross-talk"`

#### Scenario: Violation without explicit ID
- **WHEN** an assembly independence contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`

### Requirement: Assembly independence contracts support per-pair ignores
An assembly independence contract SHALL accept an `ignored_violations` list using the same `source_type`/`forbidden_reference`/`reason` shape as other contract families, where `source_type` and `forbidden_reference` are matched against assembly simple names.

#### Scenario: Ignored pair suppressed
- **WHEN** an assembly independence contract has an `ignored_violations` entry matching a source/forbidden assembly pair that would otherwise violate the contract
- **THEN** no violation is reported for that pair

### Requirement: Strict mode fails validation, audit mode reports only
`strict_assembly_independence` violations SHALL cause strict-mode validation to fail. `audit_assembly_independence` violations SHALL be reported without failing strict-mode validation.

#### Scenario: Strict violation fails validation
- **WHEN** a `strict_assembly_independence` contract has a direct-reference violation
- **THEN** strict-mode validation reports failure

#### Scenario: Audit violation does not fail strict validation
- **WHEN** an `audit_assembly_independence` contract has a direct-reference violation
- **THEN** the violation is reported but strict-mode validation does not fail because of it

### Requirement: Every listed assembly must be resolvable
The system SHALL reject, at policy load time, any `strict_assembly_independence`/`audit_assembly_independence` contract that lists an assembly name not present in `analysis.target_assemblies`, with a diagnostic identifying the contract and the unresolvable assembly name.

#### Scenario: Unresolvable assembly name rejected at load time
- **WHEN** a contract lists an assembly name that does not appear in `analysis.target_assemblies`
- **THEN** policy loading fails with an error identifying the contract and the unresolvable assembly name, rather than silently skipping the assembly at check time

### Requirement: Namespace/layer independence and Unity asmdef contracts remain unaffected
Adding the assembly independence contract family SHALL NOT change the behavior of existing `strict_independence`/`audit_independence` namespace/layer contracts or Unity `strict_asmdef`/`audit_asmdef` contracts.

#### Scenario: Existing independence behavior unchanged
- **WHEN** a policy defines both namespace/layer independence contracts and assembly independence contracts
- **THEN** the namespace/layer independence contracts evaluate exactly as they did before this change was introduced

