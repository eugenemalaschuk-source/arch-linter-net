## ADDED Requirements

### Requirement: Snapshot defers runner materialization until a cache miss requires it
The system SHALL construct a snapshot from an immutable preparation plan without CLR assembly loading. Each `Evaluate(mode)` SHALL perform that mode's cache lookup before runner/session materialization; a hit SHALL return without `BuildRunnerFor` or an assembly load context. The first miss SHALL materialize exactly one runner for the snapshot, and later misses SHALL reuse that runner while a hit in one mode SHALL not prevent evaluation of another mode.

#### Scenario: Both modes hit without materialization
- **WHEN** strict and audit entries both match a prepared snapshot
- **THEN** both outcomes return from cache and the snapshot records zero assembly loads

#### Scenario: One mode hits and the other misses
- **WHEN** strict is a cache hit and audit is a cache miss
- **THEN** strict returns without setup, audit materializes one runner, and no second runner is created for later misses

### Requirement: Lazy snapshot disposal is terminal in either state
The system SHALL dispose correctly before or after runner materialization and SHALL not permit evaluation after disposal.

#### Scenario: Unmaterialized snapshot is disposed
- **WHEN** a snapshot with only cache hits is disposed
- **THEN** no assembly load scope is created and later evaluation throws `ObjectDisposedException`
