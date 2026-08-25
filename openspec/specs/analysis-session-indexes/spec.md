# analysis-session-indexes Specification

## Purpose
Provide a per-validation-run analysis session with lazy type, reference, and layer indexes so contract handlers can reuse resolved assemblies and reflection-derived type/reference lookups instead of repeatedly invoking static scanners on every contract check.

## Requirements

### Requirement: Session created once per validation run
The system SHALL construct exactly one `ArchitectureAnalysisSession` per validation run, created during `ArchitectureContractRunner` construction from the per-run `ArchitectureAnalysisContext` (itself built once in `ArchitectureRunnerFactory.BuildRunner`), and reused by every contract check executed within that run.

#### Scenario: Single session for a multi-contract run
- **WHEN** a validation run executes multiple contracts across the dependency, layer, and cycle families
- **THEN** all of those contract checks read from the same `ArchitectureAnalysisSession` instance, and no new session is constructed mid-run

### Requirement: Lazy type index memoizes loadable types
The system SHALL provide an `ArchitectureTypeIndex` that computes the full set of loadable types across the session's target assemblies on first access and reuses that cached set for all subsequent layer/namespace lookups within the same session.

#### Scenario: Repeated layer lookups reuse the cached type set
- **WHEN** two different contract checks within the same session both query types for layers backed by the same target assemblies
- **THEN** the underlying assembly type enumeration (`Assembly.GetTypes`) executes once per assembly for the session, not once per query

#### Scenario: Layer lookup results match direct scanner output
- **WHEN** `ArchitectureTypeIndex.FindTypesInLayer` is queried for a given layer
- **THEN** it returns the same set of types that `ArchitectureTypeScanner.FindTypesInLayer` would return for the same assemblies and layer

### Requirement: Lazy reference graph memoizes per-type reference lookups
The system SHALL provide an `ArchitectureReferenceGraph` that computes a type's referenced types on first lookup and returns the cached result for any subsequent lookup of the same type within the same session.

#### Scenario: Repeated reference lookups for the same type are cached
- **WHEN** the same `Type` is queried for referenced types more than once within a session (e.g. by both a dependency contract and a cycle contract)
- **THEN** the reflection-based reference scan executes once for that type for the session, and subsequent lookups return the cached list

#### Scenario: Reference lookup results match direct scanner output
- **WHEN** `ArchitectureReferenceGraph` is queried for a type's referenced types
- **THEN** it returns the same set of types, in the same order, that `ArchitectureReferenceScanner.GetReferencedTypes` would return for that type

### Requirement: Migrated handlers use session-backed lookups
The system SHALL route the `dependency`, `layer`, and `cycle` contract-family checks through `ArchitectureAnalysisSession`'s type index and reference graph instead of invoking `ArchitectureTypeScanner` or `ArchitectureReferenceScanner` directly.

#### Scenario: Dependency contract check uses the session
- **WHEN** `ArchitectureContractRunner.CheckContract` evaluates an `ArchitectureDependencyContract`
- **THEN** it resolves source-layer types and referenced types via the session's type index and reference graph

#### Scenario: Layer contract check uses the session
- **WHEN** `ArchitectureContractRunner.CheckLayerContract` evaluates an `ArchitectureLayerContract`
- **THEN** it resolves layer types via the session's type index

#### Scenario: Cycle contract check uses the session
- **WHEN** `ArchitectureContractRunner.CheckCycleContract` evaluates an `ArchitectureCycleContract`
- **THEN** it resolves layer types and referenced types via the session's type index and reference graph

### Requirement: Validation results remain unchanged
The system SHALL produce identical violations, cycles, and pass/fail outcomes for the migrated contract families before and after introducing the session, for the same policy and assemblies.

#### Scenario: Dependency/layer/cycle results are unchanged
- **WHEN** an existing policy with dependency, layer, and cycle contracts is validated against unchanged target assemblies
- **THEN** the resulting violations and cycles are identical to those produced before the session was introduced

### Requirement: Session exposes a lazily-scoped role index
The system SHALL expose an `ArchitectureRoleIndex` from `ArchitectureAnalysisSession`, constructed for the session and computed on first access, following the same one-session-per-run, cache-on-first-access pattern established by `ArchitectureTypeIndex` and `ArchitectureReferenceGraph`.

#### Scenario: Role index is available alongside the type index and reference graph
- **WHEN** an `ArchitectureAnalysisSession` is constructed for a validation run
- **THEN** the session exposes `RoleIndex` as a property usable by contract checks and diagnostics, scoped to that session's lifetime

#### Scenario: Role index computation does not block session construction
- **WHEN** an `ArchitectureAnalysisSession` is constructed
- **THEN** the role index's extraction pass has not yet executed, and only executes on first access to `RoleIndex`'s lookup or diagnostics APIs

### Requirement: Session exposes a lazily-scoped source file fact index
The system SHALL expose an `ArchitectureSourceFileFactIndex` from `ArchitectureAnalysisSession`, constructed for the session and computed on first access, following the same one-session-per-run, cache-on-first-access pattern established by `ArchitectureTypeIndex` and `ArchitectureRoleIndex`.

#### Scenario: Source file fact index is available alongside the type index and role index
- **WHEN** an `ArchitectureAnalysisSession` is constructed for a validation run
- **THEN** the session exposes `SourceFileFactIndex` as a property usable by contract checks and diagnostics, scoped to that session's lifetime

#### Scenario: Source file fact index computation does not block session construction
- **WHEN** an `ArchitectureAnalysisSession` is constructed
- **THEN** the source file fact index's build pass has not yet executed, and only executes on first access to `SourceFileFactIndex`'s lookup or data properties

### Requirement: Session memoizes exported public-API facts per resolved artifact

The system SHALL lazily materialize and retain one immutable complete exported public-API surface per resolved assembly object identity for the lifetime of an `ArchitectureAnalysisSession`. The materialized surface SHALL include the exported entries and the exported type set required to apply a public-API surface selector. A session SHALL reuse those facts for each public-API contract and any capture, diff, update, or migrate operation it performs; a new session or a distinct resolved assembly object SHALL materialize a fresh surface.

#### Scenario: Multiple contracts reuse one base surface

- **WHEN** strict or audit public-API contracts with and without selectors target the same resolved assembly in one analysis session
- **THEN** the session SHALL materialize that assembly's complete exported public-API surface once and apply each contract's own selector, snapshot, comparison, and ignore rules to the shared base facts

#### Scenario: Capture lifecycle reuses the session surface

- **WHEN** a public-API capture, diff, update, or migrate operation and public-API contract evaluation access the same resolved assembly through one analysis session
- **THEN** they SHALL reuse that session's materialized complete exported public-API surface

#### Scenario: Cache scope does not cross session or artifact boundaries

- **WHEN** a separate analysis session is created or a session resolves a distinct assembly object
- **THEN** no exported public-API facts from a prior session or different assembly object SHALL be reused

#### Scenario: Public-API evaluation semantics are preserved

- **WHEN** the same policy and resolved assemblies are evaluated before and after session surface reuse
- **THEN** canonical entries, selector-safety verdicts, findings, identities, ordering, report projections, Testing behavior, and exit categories SHALL remain equivalent
