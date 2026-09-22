## ADDED Requirements

### Requirement: Unified PR report renders a bounded repository metrics delta

The existing Core-owned architecture PR report SHALL optionally consume compatible absolute base and head `repository-metrics/v1` snapshots from the canonical change-report artifact and render one bounded `Repository metrics delta` section. The section SHALL show base, head, and delta for meaningful repository-level rows, SHALL omit unchanged noise by default, SHALL use neutral signs/arrows, and SHALL never affect architecture acceptance or health.

#### Scenario: Compatible metrics show change rather than a dashboard
- **WHEN** the base and head change snapshots contain compatible repository metrics
- **THEN** the unified PR report renders one bounded Repository metrics delta section with base, head, and delta values
- **AND** unchanged low-value rows are omitted or collapsed
- **AND** no second PR report or publisher is created

#### Scenario: Missing or incompatible base evidence is explicit
- **WHEN** the base snapshot is missing, partial, unavailable, or has an incompatible metrics schema
- **THEN** the report states that the repository metrics delta is unavailable
- **AND** it does not substitute zero or infer a delta from unrelated architecture entries
