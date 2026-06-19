## ADDED Requirements

### Requirement: Evaluate strict dependency contracts
The system SHALL evaluate each strict dependency contract by finding all types in the source layer's namespace and checking that none reference types in any forbidden layer's namespace.

#### Scenario: No violations found
- **WHEN** source layer types do not reference any forbidden layer types
- **THEN** the contract returns an empty violation list

#### Scenario: Violations found
- **WHEN** source layer type `A` references forbidden layer type `B`
- **THEN** the contract returns a violation with `ContractName`, `SourceType = "A"`, `ForbiddenNamespace`, and `ForbiddenReferences` containing `"B"`

#### Scenario: Allowed types exempted
- **WHEN** source type references a forbidden type that is listed in `allowed_types`
- **THEN** that reference is excluded from violations

#### Scenario: Ignored violations exempted
- **WHEN** a source+forbidden pair matches an entry in `ignored_violations`
- **THEN** that violation is excluded from results

### Requirement: Evaluate forbidden legacy runtime layers
The system SHALL check source types against all namespaces in `legacy_runtime_layers` when `forbidden_legacy_runtime` is true.

#### Scenario: Legacy runtime violation
- **WHEN** `forbidden_legacy_runtime` is true and source type references a type in a legacy runtime namespace
- **THEN** a violation is returned for that legacy namespace

### Requirement: Multiple forbidden layers per contract
The system SHALL evaluate each forbidden layer independently and return all violations across all forbidden layers.

#### Scenario: Violations across multiple forbidden layers
- **WHEN** source type references types in both forbidden layer A and forbidden layer B
- **THEN** violations are returned for both forbidden namespaces

## ADDED Requirements

### Requirement: Dependency contract accepts optional id
A dependency contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** a dependency contract with `id: my-rule` produces a violation
- **THEN** the violation SHALL have `ContractId == "my-rule"`

#### Scenario: Violation without explicit ID
- **WHEN** a dependency contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`

## ADDED Requirements

### Requirement: Dependency depth mode
A dependency contract SHALL accept an optional `dependency_depth` field with values `"direct"` (default) or `"transitive"`.

#### Scenario: Default direct mode
- **WHEN** a dependency contract has no `dependency_depth` field
- **THEN** only direct type references (1 level) are checked

#### Scenario: Explicit direct mode
- **WHEN** a dependency contract has `dependency_depth: direct`
- **THEN** only direct type references are checked

#### Scenario: Transitive mode
- **WHEN** a dependency contract has `dependency_depth: transitive`
- **THEN** the system follows the type dependency graph via BFS and reports violations at any depth

### Requirement: Transitive dependency path diagnostics
When `dependency_depth: transitive`, each violation SHALL include a `DependencyPaths` collection parallel to `ForbiddenReferences`.

#### Scenario: Transitive violation with path
- **WHEN** source type A references intermediate type B which references forbidden type C
- **THEN** the violation has `ForbiddenReferences` containing `"C"` and `DependencyPaths` containing `[["A", "B", "C"]]`

#### Scenario: Path starts with source type
- **WHEN** a transitive violation is produced
- **THEN** each path's first element is the `SourceType`

#### Scenario: Path ends with forbidden reference
- **WHEN** a transitive violation is produced
- **THEN** each path's last element equals the corresponding entry in `ForbiddenReferences`

### Requirement: Transitive mode respects allowed types and ignored violations
Transitive mode SHALL apply the same `allowed_types` and `ignored_violations` filters as direct mode.

#### Scenario: Allowed types in transitive mode
- **WHEN** `dependency_depth: transitive` and a forbidden terminal type is in `allowed_types`
- **THEN** that terminal type is excluded from violations

#### Scenario: Ignored violations in transitive mode
- **WHEN** `dependency_depth: transitive` and a source+terminal pair matches `ignored_violations`
- **THEN** that violation is excluded from results

### Requirement: Transitive mode determinism
Transitive violation results SHALL be deterministic and sorted.

#### Scenario: Deterministic output
- **WHEN** the same contract is evaluated twice
- **THEN** identical violations with identical paths are returned in the same order
