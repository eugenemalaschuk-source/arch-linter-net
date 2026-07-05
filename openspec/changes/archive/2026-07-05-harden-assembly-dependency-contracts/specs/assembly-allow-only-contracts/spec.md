## ADDED Requirements

### Requirement: Assembly allow-only contract accepts an optional dependency_depth field
An assembly allow-only contract SHALL accept an optional `dependency_depth` field with the same values as namespace-level dependency contracts (`direct` or `transitive`), defaulting to `direct`.

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
