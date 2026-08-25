## ADDED Requirements

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
