## ADDED Requirements

### Requirement: Session exposes a lazily-scoped role index
The system SHALL expose an `ArchitectureRoleIndex` from `ArchitectureAnalysisSession`, constructed for the session and computed on first access, following the same one-session-per-run, cache-on-first-access pattern established by `ArchitectureTypeIndex` and `ArchitectureReferenceGraph`.

#### Scenario: Role index is available alongside the type index and reference graph
- **WHEN** an `ArchitectureAnalysisSession` is constructed for a validation run
- **THEN** the session exposes `RoleIndex` as a property usable by contract checks and diagnostics, scoped to that session's lifetime

#### Scenario: Role index computation does not block session construction
- **WHEN** an `ArchitectureAnalysisSession` is constructed
- **THEN** the role index's extraction pass has not yet executed, and only executes on first access to `RoleIndex`'s lookup or diagnostics APIs
