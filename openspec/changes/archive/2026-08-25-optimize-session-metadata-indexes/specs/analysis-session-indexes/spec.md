## ADDED Requirements

### Requirement: Session metadata indexes reuse immutable project and assembly facts

The system SHALL provide lazy, session-owned immutable projections over the retained assemblies and
discovered-project inventory: assembly name to loaded assembly; assembly name to discovered project;
normalized project path to discovered project; and assembly name to discovered package references.
Each projection set SHALL materialize at most once for one analysis session and SHALL preserve the
first retained/discovered value for duplicate keys, matching the prior lookup behavior.

#### Scenario: Many contracts share one metadata projection
- **WHEN** one analysis session evaluates multiple package, framework-reference, assembly-dependency,
  and project-metadata contracts against a many-project inventory
- **THEN** each required project or assembly index materializes no more than once for that session
- **AND** every later covered lookup uses its session projection rather than rebuilding or linearly
  scanning the whole inventory

#### Scenario: Normalized project lookup preserves the current owner
- **WHEN** two discovered entries normalize to the same project path or share an assembly name
- **THEN** the projection returns the first entry in discovery order, as the previous grouping and
  linear lookup behavior did

### Requirement: Covered metadata families consume session indexes without behavior drift

The package-dependency, framework-reference, assembly-dependency, and project-metadata check paths SHALL
consume the session metadata indexes for their repository-wide lookup needs. Their canonical
findings, identities, ordering, strict/audit behavior, baseline behavior, and human/JSON/SARIF/
Testing output SHALL remain equivalent for the same policy and analysis inputs.

#### Scenario: Multi-contract fixture preserves findings while bounding index work
- **WHEN** a synthetic many-project fixture evaluates repeated contracts from each covered family
- **THEN** its findings and pass/fail outcomes equal the established behavior
- **AND** deterministic materialization counters are bounded independently of contract fan-out
