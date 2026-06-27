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

