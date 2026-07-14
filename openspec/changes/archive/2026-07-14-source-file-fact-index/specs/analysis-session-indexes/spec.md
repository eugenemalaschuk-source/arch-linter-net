## ADDED Requirements

### Requirement: Session exposes a lazily-scoped source file fact index
The system SHALL expose an `ArchitectureSourceFileFactIndex` from `ArchitectureAnalysisSession`, constructed for the session and computed on first access, following the same one-session-per-run, cache-on-first-access pattern established by `ArchitectureTypeIndex` and `ArchitectureRoleIndex`.

#### Scenario: Source file fact index is available alongside the type index and role index
- **WHEN** an `ArchitectureAnalysisSession` is constructed for a validation run
- **THEN** the session exposes `SourceFileFactIndex` as a property usable by contract checks and diagnostics, scoped to that session's lifetime

#### Scenario: Source file fact index computation does not block session construction
- **WHEN** an `ArchitectureAnalysisSession` is constructed
- **THEN** the source file fact index's build pass has not yet executed, and only executes on first access to `SourceFileFactIndex`'s lookup or data properties
