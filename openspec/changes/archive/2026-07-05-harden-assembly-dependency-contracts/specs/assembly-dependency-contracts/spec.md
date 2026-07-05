## ADDED Requirements

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
